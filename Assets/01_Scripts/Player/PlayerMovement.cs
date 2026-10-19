using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BagBack100.Core;

namespace BagBack100.Player
{
    public class PlayerMovement : MonoBehaviour
    {
        public event Action OnMovementFinished;
        public event Action<Vector3> OnTileReached;

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

        public bool IsMoving => _isMoving;
        public Vector3 CurrentTargetTileWorldPos => (_isMoving && _currentPath.Count > 0) ? _currentPath[0] : transform.position;

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
            
            _currentPath = Pathfinder.FindPath(_grid, startPos, targetPos, _obstacleLayerMask);
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

        private IEnumerator MoveAlongPathCoroutine()
        {
            _isMoving = true;

            while (_currentPath.Count > 0)
            {
                Vector3 startPos = transform.position;
                Vector3 endPos = _currentPath[0];
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
                _stepIndex++;
                float tiltSign = (_stepIndex % 2 == 1) ? 1f : -1f;

                while (elapsedTime < duration)
                {
                    elapsedTime += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsedTime / duration);
                    
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
                    _currentPath = Pathfinder.FindPath(_grid, endPos, _pendingTarget, _obstacleLayerMask);
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
            _hasPendingTarget = false;
            ResetVisuals();
        }
    }
}
