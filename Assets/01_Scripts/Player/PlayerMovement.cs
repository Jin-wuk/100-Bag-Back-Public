using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BagBack100.Core;

namespace BagBack100.Player
{
    /// <summary>
    /// Role: 플레이어 타일 경로 이동(Pathfinder), 타일 점유 예약, 걸음 연출, 대시 이동(착지 칸 점유·직선 보간). 층 전환 순간이동 시 StopImmediately로 즉시 끊는다.
    /// Dependencies: Pathfinder, TileSystem, PlayerStatManager
    /// Events: OnMovementFinished, OnTileReached, OnDashFinished
    /// </summary>
    public class PlayerMovement : MonoBehaviour
    {
        public event Action OnMovementFinished;
        public event Action<Vector3> OnTileReached;
        public event Action OnDashFinished;

        [Header("Movement Settings")]
        [Tooltip("기본 이동 속도(10) 기준 타일 1칸 이동 시 걸리는 시간")]
        [SerializeField] private float _moveDurationPerTile = 0.3f;
        [Tooltip("이동 시 통통 튀는 최대 높이 (과하지 않은 0.08~0.12 권장)")]
        [SerializeField] private float _hopHeight = 0.1f;
        [Tooltip("경로 탐색 시 장애물로 인식할 레이어")]
        [SerializeField] private LayerMask _obstacleLayerMask;

        [Header("Locomotion Feel (Bouncy Walk & Wobble)")]
        [Tooltip("이동 중 발걸음 좌우 흔들림(Tilt) 각도 (자연스러운 2.5~3.5도 권장)")]
        [SerializeField] private float _wobbleAngle = 3.0f;
        [Tooltip("이동 중 스쿼시 & 스트레치 탄성 연출 활성화")]
        [SerializeField] private bool _enableSquashAndStretch = true;
        [Tooltip("도약 시 세로 늘어남(Stretch) 비율")]
        [SerializeField] private float _stretchAmount = 0.06f;
        [Tooltip("착지 시 세로 눌림(Squash) 비율")]
        [SerializeField] private float _squashAmount = 0.05f;

        private Coroutine _moveCoroutine;
        private List<Vector3> _currentPath = new List<Vector3>();
        private bool _isMoving = false;
        private Vector3 _pendingTarget;
        private bool _hasPendingTarget = false;
        private Grid _grid;
        private PlayerStatManager _stats;
        private Animator _animator;

        private Vector3 _baseScale = Vector3.one;
        private Quaternion _baseRotation = Quaternion.identity;
        private int _stepIndex = 0;

        // 걷기는 시작 순간 목적지 칸을 점유하는데 _currentPath에서는 이미 빠지므로, 대시·파괴 시 해제할 칸을 따로 기억한다
        private Vector3Int _reservedCell;
        private bool _hasReservedCell = false;
        private bool _isDashing = false;
        private Vector3 _dashTargetWorld;

        public bool IsMoving => _isMoving;
        public bool IsDashing => _isDashing;
        // 지금 걷는 칸의 진행도(0~1). 걷기 프레임을 칸 하나에 한 번 재생하는 기준
        public float CurrentStepProgress { get; private set; }
        // 걸음을 시작할 때마다 1씩 늘어나는 번호. 새 칸 걸음이 시작됐는지 판정하는 데 쓴다
        public int StepCount => _stepIndex;
        // 지금 걷는 칸·대시의 시작→도착 방향(z=0). _currentPath는 보간 전에 비워지고 걸음 홉이 위치에 섞이므로 따로 기록한다
        public Vector3 CurrentStepDirection { get; private set; }
        public LayerMask ObstacleLayerMask => _obstacleLayerMask;
        // 곡사 포격·투사체의 위치 예측이 읽는다. 대시 중에는 착지 칸을 돌려준다
        public Vector3 CurrentTargetTileWorldPos => _isDashing
            ? _dashTargetWorld
            : ((_isMoving && _currentPath.Count > 0) ? _currentPath[0] : transform.position);

        public Vector3Int ReservedCell
        {
            get
            {
                if (_hasReservedCell) return _reservedCell;
                if (_grid == null) return Vector3Int.zero;
                Vector3Int cell = _grid.WorldToCell(transform.position);
                cell.z = 0;
                return cell;
            }
        }

        private void Awake()
        {
            _stats = GetComponent<PlayerStatManager>();
            _animator = GetComponentInChildren<Animator>();
            _baseScale = transform.localScale;
            _baseRotation = transform.localRotation;
        }

        private void Start()
        {
            _grid = FindObjectOfType<Grid>();
        }

        public float GetCurrentMoveDuration()
        {
            // 기본 Speed = 10f 일 때 _moveDurationPerTile(0.3초) 소요. Speed가 높을수록 이동 시간이 단축됨.
            float currentSpeed = (_stats != null && _stats.CurrentSpeed > 0.1f) ? _stats.CurrentSpeed : 10f;
            return Mathf.Clamp(_moveDurationPerTile * (10f / currentSpeed), 0.08f, 1.0f);
        }

        public void MoveTo(Vector3 targetPos)
        {
            // 이동 중일 경우, 현재 이동 중인 타일까지만 도착한 후 새로운 경로를 계산하도록 예약 (제약 조건)
            if (_isMoving)
            {
                _pendingTarget = targetPos;
                _hasPendingTarget = true;
                return;
            }

            StartPath(targetPos);
        }

        private void StartPath(Vector3 targetPos)
        {
            if (_grid == null) return;
            
            // Grid API를 통해 완벽한 타일 중앙값 획득
            Vector3 startPos = _grid.GetCellCenterWorld(_grid.WorldToCell(transform.position));
            
            _currentPath = Pathfinder.FindPath(_grid, startPos, targetPos, _obstacleLayerMask, false);
            if (_currentPath.Count > 0)
            {
                if (_moveCoroutine != null)
                {
                    StopCoroutine(_moveCoroutine);
                }
                _moveCoroutine = StartCoroutine(MoveAlongPathCoroutine());
            }
            else
            {
                OnMovementFinished?.Invoke();
            }
        }

        public void CancelPath()
        {
            if (_isMoving)
            {
                _currentPath.Clear();
                _hasPendingTarget = false;
                ResetVisuals();
            }
        }

        /// <summary>
        /// 층 전환 순간이동 전용: 진행 중인 타일 보간 코루틴까지 즉시 끊는다.
        /// CancelPath는 현재 칸 이동을 끝까지 마치므로, 그대로 두면 이전 층 좌표로 위치를 덮어쓴다.
        /// OnMovementFinished는 보내지 않는다(호출한 쪽이 상태를 직접 Idle로 되돌린다).
        /// </summary>
        public void StopImmediately()
        {
            if (_moveCoroutine != null)
            {
                StopCoroutine(_moveCoroutine);
                _moveCoroutine = null;
            }
            _currentPath.Clear();
            _hasPendingTarget = false;
            _isMoving = false;
            _isDashing = false;
            _hasReservedCell = false;
            if (_animator != null) _animator.speed = 1f;
            ResetVisuals();
        }

        /// <summary>
        /// 착지 칸은 호출한 쪽(PlayerController)이 비어 있음을 확인한 뒤 넘긴다.
        /// 걷던 중이면 걷기를 끊고, 착지 칸을 먼저 점유한 뒤 자신이 잡고 있던 칸(걷기 예약 칸·현재 위치 칸·시작 칸)을 놓는다.
        /// </summary>
        public void Dash(Vector3Int startCell, Vector3Int landingCell, float duration)
        {
            if (_grid == null) return;

            if (_moveCoroutine != null)
            {
                StopCoroutine(_moveCoroutine);
                _moveCoroutine = null;
            }
            _currentPath.Clear();
            _hasPendingTarget = false;
            ResetVisuals();

            landingCell.z = 0;
            startCell.z = 0;
            if (TileSystem.Instance != null)
            {
                Vector3Int positionCell = _grid.WorldToCell(transform.position);
                positionCell.z = 0;

                TileSystem.Instance.RegisterInitialTile(landingCell, gameObject);
                if (_hasReservedCell && _reservedCell != landingCell) TileSystem.Instance.ReleaseTile(_reservedCell, gameObject);
                if (positionCell != landingCell) TileSystem.Instance.ReleaseTile(positionCell, gameObject);
                if (startCell != landingCell) TileSystem.Instance.ReleaseTile(startCell, gameObject);
            }
            _reservedCell = landingCell;
            _hasReservedCell = true;

            _dashTargetWorld = _grid.GetCellCenterWorld(landingCell);
            CurrentStepDirection = new Vector3(_dashTargetWorld.x - transform.position.x, _dashTargetWorld.y - transform.position.y, 0f);
            _isMoving = true;
            _isDashing = true;
            if (_animator != null) _animator.speed = 1f;
            _moveCoroutine = StartCoroutine(DashCoroutine(Mathf.Max(0.01f, duration)));
        }

        private IEnumerator DashCoroutine(float duration)
        {
            Vector3 startPos = transform.position;
            float elapsed = 0f;

            // 걷기와 같은 Time.deltaTime 기준: 일시정지·히트스톱에 함께 멈춘다
            while (elapsed < duration)
            {
                // 대시 중 사망하면 착지 처리(줍기·계단·탈출구)를 하지 않는다
                if (_stats != null && _stats.IsDead)
                {
                    _isDashing = false;
                    _isMoving = false;
                    _moveCoroutine = null;
                    yield break;
                }

                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(startPos, _dashTargetWorld, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            transform.position = _dashTargetWorld;
            ResetVisuals();
            _isDashing = false;
            _isMoving = false;
            _moveCoroutine = null;

            OnTileReached?.Invoke(_dashTargetWorld);
            OnDashFinished?.Invoke();
        }

        private IEnumerator MoveAlongPathCoroutine()
        {
            _isMoving = true;

            while (_currentPath.Count > 0)
            {
                Vector3 startPos = transform.position;
                Vector3 endPos = _currentPath[0];
                CurrentStepDirection = new Vector3(endPos.x - startPos.x, endPos.y - startPos.y, 0f);
                Vector3Int currentCell = _grid.WorldToCell(startPos);
                Vector3Int targetCell = _grid.WorldToCell(endPos);

                // 타일 점유 (Race condition 방지)
                if (TileSystem.Instance != null)
                {
                    if (!TileSystem.Instance.TryOccupyTile(targetCell, gameObject, currentCell))
                    {
                        // 점유 실패 시 이동 멈춤
                        _currentPath.Clear();
                        break;
                    }
                    _reservedCell = targetCell;
                    _reservedCell.z = 0;
                    _hasReservedCell = true;
                }
                else
                {
                    // Fallback
                    if (Physics2D.OverlapPoint(endPos, LayerMask.GetMask("Monster")) != null)
                    {
                        _currentPath.Clear();
                        break;
                    }
                }
                
                _currentPath.RemoveAt(0);

                float duration = GetCurrentMoveDuration();
                if (_animator != null)
                {
                    _animator.speed = _moveDurationPerTile / Mathf.Max(0.05f, duration);
                }

                float elapsedTime = 0f;
                CurrentStepProgress = 0f;
                _stepIndex++;
                float tiltSign = (_stepIndex % 2 == 1) ? 1f : -1f;

                while (elapsedTime < duration)
                {
                    elapsedTime += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsedTime / duration);
                    CurrentStepProgress = t;
                    
                    // 1. 기본 선형 보간 위치
                    Vector3 lerpedPos = Vector3.Lerp(startPos, endPos, t);
                    
                    // 2. 미세 상하 바운스 (Parabolic Arc: 0 -> 1 -> 0)
                    if (_hopHeight > 0.001f)
                    {
                        lerpedPos.y += Mathf.Sin(t * Mathf.PI) * _hopHeight;
                    }
                    transform.position = lerpedPos;

                    // 3. 발걸음 좌우 틸트 (Wobble: 0 -> peak -> 0)
                    if (_wobbleAngle > 0.001f)
                    {
                        float currentTilt = Mathf.Sin(t * Mathf.PI) * _wobbleAngle * tiltSign;
                        transform.localRotation = Quaternion.Euler(0f, 0f, currentTilt);
                    }

                    // 4. 스쿼시 & 스트레치 (Squash & Stretch)
                    if (_enableSquashAndStretch)
                    {
                        Vector3 currentScale = _baseScale;
                        if (t < 0.75f)
                        {
                            // 상승 및 정점 구간: 세로 스트레치
                            float stretchProgress = Mathf.Sin((t / 0.75f) * Mathf.PI);
                            currentScale.x *= (1f - stretchProgress * (_stretchAmount * 0.5f));
                            currentScale.y *= (1f + stretchProgress * _stretchAmount);
                        }
                        else
                        {
                            // 착지 쿠션 구간: 가로 스쿼시
                            float squashProgress = Mathf.Sin(((t - 0.75f) / 0.25f) * Mathf.PI);
                            currentScale.x *= (1f + squashProgress * (_squashAmount * 0.5f));
                            currentScale.y *= (1f - squashProgress * _squashAmount);
                        }
                        transform.localScale = currentScale;
                    }

                    yield return null; // Update 내 GC 방지를 위해 yield return null 만 사용
                }

                // 타일 이동 종료 시점 스냅 및 비주얼 원복
                transform.position = endPos;
                transform.localRotation = _baseRotation;
                transform.localScale = _baseScale;
                OnTileReached?.Invoke(endPos);

                // 다음 타일 이동 전, 새로운 목적지가 예약되어 있다면 경로 재계산
                if (_hasPendingTarget)
                {
                    _hasPendingTarget = false;
                    _currentPath = Pathfinder.FindPath(_grid, endPos, _pendingTarget, _obstacleLayerMask, false);
                }
            }

            if (_animator != null)
            {
                _animator.speed = 1f;
            }
            ResetVisuals();
            _isMoving = false;
            OnMovementFinished?.Invoke();
        }

        private void ResetVisuals()
        {
            _stepIndex = 0;
            transform.localRotation = _baseRotation;
            transform.localScale = _baseScale;
        }

        private void OnDisable()
        {
            // Task Cleanup 제약 조건: 비활성화 시 코루틴 정지
            if (_moveCoroutine != null)
            {
                StopCoroutine(_moveCoroutine);
                _moveCoroutine = null;
            }
            _isMoving = false;
            _isDashing = false;
            _hasPendingTarget = false;
            ResetVisuals();
        }
    }
}
