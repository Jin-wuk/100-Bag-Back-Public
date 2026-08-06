using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DimensionLooter.Core;

namespace DimensionLooter.Player
{
    public class PlayerMovement : MonoBehaviour
    {
        public event Action OnMovementFinished;

        [Header("Movement Settings")]
        [Tooltip("타일 1칸 이동 시 걸리는 시간")]
        [SerializeField] private float _moveDurationPerTile = 0.3f;
        [Tooltip("이동 시 통통 튀는 최대 높이")]
        [SerializeField] private float _hopHeight = 0.5f;
        [Tooltip("경로 탐색 시 장애물로 인식할 레이어")]
        [SerializeField] private LayerMask _obstacleLayerMask;

        private Coroutine _moveCoroutine;
        private List<Vector3> _currentPath = new List<Vector3>();
        private bool _isMoving = false;
        private Vector3 _pendingTarget;
        private bool _hasPendingTarget = false;

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
            // 스냅된 현재 타일 위치 계산
            Vector3 startPos = new Vector3(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.y), 0f);
            
            _currentPath = Pathfinder.FindPath(startPos, targetPos, _obstacleLayerMask);
            if (_currentPath.Count > 0)
            {
                if (_moveCoroutine != null)
                {
                    StopCoroutine(_moveCoroutine);
                }
                _moveCoroutine = StartCoroutine(MoveAlongPathCoroutine());
            }
        }

        private IEnumerator MoveAlongPathCoroutine()
        {
            _isMoving = true;

            while (_currentPath.Count > 0)
            {
                Vector3 startPos = transform.position;
                Vector3 endPos = _currentPath[0];
                _currentPath.RemoveAt(0);

                float elapsedTime = 0f;

                while (elapsedTime < _moveDurationPerTile)
                {
                    elapsedTime += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsedTime / _moveDurationPerTile);
                    
                    // 선형 이동(X, Y)
                    Vector3 lerpedPos = Vector3.Lerp(startPos, endPos, t);
                    
                    // 통통 튀는(Hopping) Y축 오프셋 추가 (Mathf.Sin 활용: 0 -> 1 -> 0)
                    float hopOffset = Mathf.Sin(t * Mathf.PI) * _hopHeight;
                    lerpedPos.y += hopOffset;

                    transform.position = lerpedPos;
                    yield return null; // Update 내 GC 방지를 위해 yield return null 만 사용
                }

                // 타일 이동 종료 시점 스냅
                transform.position = endPos;

                // 다음 타일 이동 전, 새로운 목적지가 예약되어 있다면 경로 재계산
                if (_hasPendingTarget)
                {
                    _hasPendingTarget = false;
                    _currentPath = Pathfinder.FindPath(endPos, _pendingTarget, _obstacleLayerMask);
                }
            }

            _isMoving = false;
            OnMovementFinished?.Invoke();
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
        }
    }
}
