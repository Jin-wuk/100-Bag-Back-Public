using UnityEngine;
using BagBack100.Data.Models;

namespace BagBack100.Monster
{
    /// <summary>
    /// Role: 몬스터가 바라보는 방향에 맞춰 SpriteRenderer의 4방향 스프라이트 프레임만 바꾼다 (대기 루프, 공격 = 예고 구간 + 타격 구간). 색·스케일·표시 여부(피격·스턴·안개 연출)는 건드리지 않는다.
    ///       방향은 플레이어처럼 마지막 행동 기준: 이동 중 = 이동 방향, 패턴(공격)을 시작하면 그 순간의 플레이어 쪽, 대치·고정형 대기 중 = 플레이어 쪽, 그 외에는 그대로 유지.
    ///       이동 프레임이 없으면 칸 사이를 이동하는 동안에도 대기 프레임을 재생하고, 있으면(나무 walk) 이동 프레임을 반복한다. 공격 애니는 패턴별로 고를 수 있다.
    /// Dependencies: SpriteAnimSetSO(대기·이동·공격·패턴별 공격 프레임), DirectionalSpriteSO(애니 SO가 없을 때 정지 이미지), MonsterController(이동 상태·이동 방향·마지막 행동 방향·대치 시선·패턴 실행 여부)
    /// Events: 없음 (구독: MonsterController.OnAttackWindup)
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class MonsterFacingVisual : MonoBehaviour
    {
        private enum AnimPhase
        {
            Idle,
            Windup,
            Strike
        }

        [Tooltip("대기·공격 애니메이션. 비어 있으면 아래 정지 이미지를 쓴다")]
        [SerializeField] private SpriteAnimSetSO _animSet;
        [Tooltip("애니메이션 SO가 없거나 대기 프레임이 빈 방향에 쓰는 4방향 정지 이미지")]
        [SerializeField] private DirectionalSpriteSO _sprites;

        private SpriteRenderer _renderer;
        private MonsterController _monster;
        private FacingDir4 _facing = FacingDir4.South;
        private int _lastActionCount;
        private Sprite _lastSprite;

        private AnimPhase _phase = AnimPhase.Idle;
        private float _phaseTime;
        private float _windupDuration;
        private float _idleTime;
        private float _moveTime;

        // 이번 공격에 쓸 프레임 세트. 예고 시작 때 패턴으로 골라 고정한다(목록에 없으면 기본 공격)
        private DirectionalFrames _curWindup;
        private DirectionalFrames _curAttack;
        private int _curHitFrame;
        private bool _curLoopStrike;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _monster = GetComponentInParent<MonsterController>();

            if ((_animSet == null && _sprites == null) || _monster == null)
            {
                Debug.LogError($"[MonsterFacingVisual] {name}: 애니메이션 SO·방향 스프라이트 SO가 모두 없거나 MonsterController가 없어 비활성화합니다.", this);
                enabled = false;
                return;
            }
            _lastActionCount = _monster.ActionCount;
        }

        private void OnEnable()
        {
            if (_monster == null) return;
            _monster.OnAttackWindup += HandleAttackWindup;
        }

        private void OnDisable()
        {
            if (_monster == null) return;
            _monster.OnAttackWindup -= HandleAttackWindup;
            _phase = AnimPhase.Idle;
            _moveTime = 0f;
            _curWindup = null;
            _curAttack = null;
        }

        private void HandleAttackWindup(float duration)
        {
            if (_animSet != null)
            {
                PatternAttackAnim patternAnim = _animSet.FindPatternAttack(_monster.CurrentPattern);
                if (patternAnim != null)
                {
                    _curWindup = patternAnim.Windup;
                    _curAttack = patternAnim.Attack;
                    _curHitFrame = patternAnim.HitFrame;
                    _curLoopStrike = patternAnim.LoopStrikeWhileExecuting;
                }
                else
                {
                    _curWindup = _animSet.AttackWindup;
                    _curAttack = _animSet.Attack;
                    _curHitFrame = _animSet.AttackHitFrame;
                    _curLoopStrike = _animSet.LoopStrikeWhileExecuting;
                }
            }
            _phase = AnimPhase.Windup;
            _phaseTime = 0f;
            _windupDuration = Mathf.Max(0.01f, duration);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;

            // 기절·회피·사망은 예고든 타격이든 공격 동작을 바로 끊는다.
            // 패턴 실행 여부(IsExecutingPattern)로는 판단하지 않는다: 근접·원거리 패턴은 예고가 끝난 프레임에 판정하고 바로 끝나 정상 타격도 끊김으로 보인다
            if (_phase != AnimPhase.Idle && IsInterrupted())
            {
                _phase = AnimPhase.Idle;
            }

            // 공격 동작 중에는 패턴 시작 때 잡은 방향을 유지한다. 사마귀 치고 빠지기는 타격 직후 뒤로 물러나므로, 이동 방향을 따르면 등을 돌린 채 베게 된다
            if (_phase != AnimPhase.Idle)
            {
                if (_monster.ActionCount != _lastActionCount)
                {
                    _facing = DirectionalSpriteSO.FromVector(_monster.LastActionDirection, _facing);
                }
            }
            else if (_monster.IsMovingToTile)
            {
                _facing = DirectionalSpriteSO.FromVector(_monster.CurrentMoveDirection, _facing);
            }
            else if (_monster.ActionCount != _lastActionCount)
            {
                // 패턴 시작 순간에만 방향을 잡는다. 예고·공격 중에 플레이어를 따라 돌지 않는다
                _facing = DirectionalSpriteSO.FromVector(_monster.LastActionDirection, _facing);
            }
            else if (_monster.IsHoldingPosition)
            {
                // 대치·고정형 대기 중에는 플레이어를 따라 돈다(공격 애니 없이 시선만)
                _facing = DirectionalSpriteSO.FromVector(_monster.LookTargetDirection, _facing);
            }
            _lastActionCount = _monster.ActionCount;

            Sprite next = _animSet != null ? PickAnimFrame(dt) : null;
            if (next == null && _sprites != null) next = _sprites.Get(_facing);

            if (next != null && next != _lastSprite)
            {
                _renderer.sprite = next;
                _lastSprite = next;
            }
        }

        private Sprite PickAnimFrame(float dt)
        {
            // 예고용 프레임이 따로 있으면(고슴도치 몸 말기) 예고 = 그 프레임 전체, 타격 = 공격 프레임 전체.
            // 없으면 공격 프레임 하나를 타격 프레임 번호에서 나눠 앞은 예고, 뒤는 타격으로 쓴다
            if (_phase == AnimPhase.Idle || _curAttack == null) return PickIdleOrMoveFrame(dt);

            Sprite[] attack = _curAttack.Get(_facing);
            int attackCount = attack != null ? attack.Length : 0;
            Sprite[] windupFrames = _curWindup != null ? _curWindup.Get(_facing) : null;
            int windupStart = 0;
            int windupLength;
            int strikeStart;
            if (windupFrames != null && windupFrames.Length > 0)
            {
                windupLength = windupFrames.Length;
                strikeStart = 0;
            }
            else
            {
                windupFrames = attack;
                windupLength = Mathf.Min(_curHitFrame, attackCount);
                strikeStart = windupLength;
            }
            int strikeLength = attackCount - strikeStart;

            if (_phase == AnimPhase.Windup)
            {
                _phaseTime += dt;
                if (_phaseTime < _windupDuration)
                {
                    if (windupLength <= 0) return PickIdleFrame(dt);
                    int index = Mathf.Min((int)(_phaseTime / _windupDuration * windupLength), windupLength - 1);
                    return windupFrames[windupStart + index];
                }
                _phase = AnimPhase.Strike;
                _phaseTime -= _windupDuration;
                dt = 0f;
            }

            if (_phase == AnimPhase.Strike)
            {
                _phaseTime += dt;
                int index = (int)(_phaseTime * _animSet.AttackFps);
                if (strikeLength > 0 && index >= strikeLength && _curLoopStrike && _monster.IsExecutingPattern)
                {
                    index %= strikeLength;
                }
                if (strikeLength > 0 && index < strikeLength)
                {
                    return attack[strikeStart + index];
                }
                _phase = AnimPhase.Idle;
                _idleTime = 0f;
            }

            return PickIdleOrMoveFrame(dt);
        }

        // 이동 프레임이 있는 몬스터(나무 walk)는 칸을 건너는 동안 이동 프레임을 반복한다.
        // 칸 사이 대기(moveInterval)에도 _moveTime을 되감지 않아 다음 칸에서 이어 재생된다(짧은 이동마다 앞 프레임만 도는 것 방지)
        private Sprite PickIdleOrMoveFrame(float dt)
        {
            if (_monster.IsMovingToTile)
            {
                Sprite[] move = _animSet.Move.Get(_facing);
                if (move != null && move.Length > 0)
                {
                    _moveTime += dt * _animSet.MoveFps;
                    if (_moveTime >= move.Length) _moveTime -= move.Length * (int)(_moveTime / move.Length);
                    return move[(int)_moveTime];
                }
            }
            return PickIdleFrame(dt);
        }

        private bool IsInterrupted()
        {
            MonsterAIState state = _monster.CurrentState;
            return state == MonsterAIState.Stunned || state == MonsterAIState.Evading || state == MonsterAIState.Dead;
        }

        private Sprite PickIdleFrame(float dt)
        {
            Sprite[] idle = _animSet.Idle.Get(_facing);
            if (idle == null || idle.Length == 0) return null;
            _idleTime += dt * _animSet.IdleFps;
            // 오래 켜 둔 몬스터의 float 정밀도 손실을 막으려고 한 바퀴마다 되감는다
            if (_idleTime >= idle.Length) _idleTime -= idle.Length * (int)(_idleTime / idle.Length);
            return idle[(int)_idleTime];
        }
    }
}
