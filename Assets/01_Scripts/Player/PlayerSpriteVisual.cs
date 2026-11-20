using UnityEngine;
using BagBack100.Combat;
using BagBack100.Data.Models;

namespace BagBack100.Player
{
    /// <summary>
    /// Role: 플레이어 상태와 방향에 맞춰 SpriteRenderer의 스프라이트 프레임만 바꾼다 (서기·이동·대시·공격). 색·스케일 연출은 건드리지 않는다.
    /// Dependencies: SpriteAnimSetSO, PlayerController(상태·조준 방향), PlayerMovement(걸음 방향·이동 시간), EntityCombat(공격 시작 이벤트)
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

        private FacingDir4 _moveDir = FacingDir4.South;
        private FacingDir4 _attackDir = FacingDir4.South;
        private float _moveTime;
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
                _moveDir = ToDir4(_movement.CurrentStepDirection, _moveDir);
                Sprite[] frames = _animSet.Move.Get(_moveDir);
                if (state == PlayerState.Dashing)
                {
                    next = PickFrame(frames, _animSet.DashFrame);
                }
                else
                {
                    float speedRatio = _movement.BaseMoveDurationPerTile / Mathf.Max(0.01f, _movement.GetCurrentMoveDuration());
                    _moveTime += dt * _animSet.MoveFps * speedRatio;
                    next = PickLoopFrame(frames, _moveTime);
                }
            }
            else
            {
                _moveTime = 0f;
                Sprite[] attackFrames = _animSet.Attack.Get(_attackDir);
                int attackLength = attackFrames != null ? attackFrames.Length : 0;
                float attackDuration = attackLength / _animSet.AttackFps;

                if (_attackElapsed < float.MaxValue) _attackElapsed += dt;

                if (attackLength > 0 && (_attackElapsed < attackDuration || (state == PlayerState.Attacking && _attackElapsed < float.MaxValue)))
                {
                    // 재생이 끝난 뒤에도 공격 락이 남아 있으면 마지막 프레임을 유지한다
                    next = PickFrame(attackFrames, (int)(_attackElapsed * _animSet.AttackFps));
                }
                else
                {
                    _attackElapsed = float.MaxValue;
                    FacingDir4 idleDir = ToDir4(_controller.FacingDirection, _moveDir);
                    next = PickFrame(_animSet.Move.Get(idleDir), 0);
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
