using UnityEngine;
using BagBack100.Combat;
using BagBack100.Data.Models;

namespace BagBack100.Player
{
    /// <summary>
    /// Role: 플레이어 상태와 방향에 맞춰 SpriteRenderer의 스프라이트 프레임만 바꾼다 (대기·이동·대시·공격). 색·스케일 연출은 건드리지 않는다.
    ///       대기 방향은 마우스 조준이 아니라 마지막 행동(걸음·대시·공격) 방향을 따른다.
    /// Dependencies: SpriteAnimSetSO, PlayerController(상태), PlayerMovement(걸음 방향·이동 시간), EntityCombat(공격 시작 이벤트)
    /// Events: 없음 (구독: EntityCombat.OnMeleeAttack, EntityCombat.OnRangedAttack)
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerSpriteVisual : MonoBehaviour
    {
        [SerializeField] private SpriteAnimSetSO _animSet;

        private SpriteRenderer _renderer;
        private PlayerController _controller;
        private PlayerMovement _movement;
        private EntityCombat _combat;

        private FacingDir4 _lookDir = FacingDir4.South;
        private FacingDir4 _attackDir = FacingDir4.South;
        private float _idleTime;
        private int _lastStepCount = -1;
        private float _lastStepProgress;
        private FacingDir4 _lastStepDir = FacingDir4.South;
        private bool _altFoot;
        private float _notWalkingTime = float.MaxValue;
        private float _attackElapsed = float.MaxValue;
        private Sprite _lastSprite;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _controller = GetComponentInParent<PlayerController>();
            _movement = GetComponentInParent<PlayerMovement>();
            _combat = GetComponentInParent<EntityCombat>();

            if (_animSet == null || _controller == null || _movement == null || _combat == null)
            {
                Debug.LogError($"[PlayerSpriteVisual] {name}: 애니메이션 SO 또는 PlayerController·PlayerMovement·EntityCombat이 없어 비활성화합니다.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_combat == null) return;
            _combat.OnMeleeAttack += HandleAttackStarted;
            _combat.OnRangedAttack += HandleAttackStarted;
        }

        private void OnDisable()
        {
            if (_combat == null) return;
            _combat.OnMeleeAttack -= HandleAttackStarted;
            _combat.OnRangedAttack -= HandleAttackStarted;
        }

        // 선입력 연속 공격은 Attacking → Idle → Attacking이 한 프레임 안에 일어나 상태 폴링으로는 다음 타격을 놓친다. 공격 이벤트로 매 타격을 받는다
        private void HandleAttackStarted(Vector3 direction)
        {
            _attackElapsed = 0f;
            _attackDir = ToDir4(direction, _attackDir);
            _lookDir = _attackDir;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            PlayerState state = _controller.CurrentState;
            Sprite next;

            // 이동·대시·차징이 공격 락을 끊으면 남은 공격 프레임을 이어 재생하지 않는다
            if (state == PlayerState.Moving || state == PlayerState.Dashing || state == PlayerState.Charging)
            {
                _attackElapsed = float.MaxValue;
            }

            if (state == PlayerState.Moving || state == PlayerState.Dashing)
            {
                _idleTime = 0f;
                _lookDir = ToDir4(_movement.CurrentStepDirection, _lookDir);
                Sprite[] frames = _animSet.Move.Get(_lookDir);
                if (state == PlayerState.Dashing)
                {
                    _notWalkingTime = float.MaxValue;
                    next = PickFrame(frames, _animSet.DashFrame);
                }
                else
                {
                    // 같은 방향으로 이어 걸으면 왼발·오른발을 번갈아 쓰고, 방향이 바뀌거나 멈췄다 걸으면 왼발부터 다시.
                    // 키를 누르고 있어도 칸 사이에 Idle 프레임이 끼어들 수 있어, 쉰 시간이 짧으면 이어 걷기로 본다
                    // 멈추면 걸음 번호가 0으로 돌아가 같은 번호가 다시 나올 수 있으므로, 진행도가 줄어든 것도 새 걸음으로 본다
                    int stepCount = _movement.StepCount;
                    float progress = _movement.CurrentStepProgress;
                    if (stepCount != _lastStepCount || progress < _lastStepProgress)
                    {
                        bool chained = _notWalkingTime <= _animSet.StepChainGap && _lastStepDir == _lookDir;
                        _altFoot = chained && !_altFoot;
                        _lastStepCount = stepCount;
                        _lastStepDir = _lookDir;
                    }
                    _notWalkingTime = 0f;
                    _lastStepProgress = progress;
                    if (_altFoot)
                    {
                        Sprite[] altFrames = _animSet.MoveAlt.Get(_lookDir);
                        if (altFrames != null && altFrames.Length > 0) frames = altFrames;
                    }

                    // 칸 하나를 건너는 동안 프레임을 한 번 재생해 걸음이 칸마다 한 번 뛰는 동작이 되게 한다. 이동 속도가 바뀌어도 칸 진행도를 따르므로 맞춰진다
                    int count = frames != null ? frames.Length : 0;
                    next = PickFrame(frames, (int)(progress * count));
                }
            }
            else
            {
                if (_notWalkingTime < float.MaxValue) _notWalkingTime += dt;
                Sprite[] attackFrames = _animSet.Attack.Get(_attackDir);
                int attackLength = attackFrames != null ? attackFrames.Length : 0;
                float attackDuration = attackLength / _animSet.AttackFps;

                if (_attackElapsed < float.MaxValue) _attackElapsed += dt;

                if (attackLength > 0 && (_attackElapsed < attackDuration || (state == PlayerState.Attacking && _attackElapsed < float.MaxValue)))
                {
                    // 재생이 끝난 뒤에도 공격 락이 남아 있으면 마지막 프레임을 유지한다
                    _idleTime = 0f;
                    next = PickFrame(attackFrames, (int)(_attackElapsed * _animSet.AttackFps));
                }
                else
                {
                    _attackElapsed = float.MaxValue;
                    Sprite[] idleFrames = _animSet.Idle.Get(_lookDir);
                    if (idleFrames != null && idleFrames.Length > 0)
                    {
                        _idleTime += dt * _animSet.IdleFps;
                        next = PickLoopFrame(idleFrames, _idleTime);
                    }
                    else
                    {
                        next = PickFrame(_animSet.Move.Get(_lookDir), 0);
                    }
                }
            }

            if (next != null && next != _lastSprite)
            {
                _renderer.sprite = next;
                _lastSprite = next;
            }
        }

        // 대각선은 좌우 방향을 우선한다
        private static FacingDir4 ToDir4(Vector3 v, FacingDir4 fallback)
        {
            float ax = Mathf.Abs(v.x);
            float ay = Mathf.Abs(v.y);
            if (ax < 0.001f && ay < 0.001f) return fallback;
            if (ax >= ay) return v.x > 0f ? FacingDir4.East : FacingDir4.West;
            return v.y > 0f ? FacingDir4.North : FacingDir4.South;
        }

        private static Sprite PickFrame(Sprite[] frames, int index)
        {
            if (frames == null || frames.Length == 0) return null;
            return frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        }

        private static Sprite PickLoopFrame(Sprite[] frames, float time)
        {
            if (frames == null || frames.Length == 0) return null;
            return frames[(int)time % frames.Length];
        }
    }
}
