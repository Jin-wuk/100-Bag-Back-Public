// File: Assets/Scripts/Gameplay/PlayerController.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using DimensionLooter.Core;

namespace DimensionLooter.Gameplay
{
    /// <summary>
    /// Tilemap 그리드 기반 A* 4방향 이동 및 상호작용 처리를 담당합니다.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("타일 간 이동 속도")]
        [SerializeField] private float _moveSpeed = 5f;
        [Tooltip("장애물(벽)로 인식할 레이어")]
        [SerializeField] private LayerMask _obstacleMask;

        private List<Vector2Int> _currentPath;
        private int _pathIndex;
        private bool _isMoving = false;

        private Vector2Int CurrentGridPos => new Vector2Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.y));

        private void Update()
        {
            // UI 위를 클릭한 경우 무시
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            HandleInput();
            HandleMovement();
        }

        private void HandleInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                Vector2Int targetGridPos = new Vector2Int(Mathf.RoundToInt(mousePos.x), Mathf.RoundToInt(mousePos.y));

                if (targetGridPos == CurrentGridPos) return;

                // A* 경로 탐색
                List<Vector2Int> path = Pathfinder.FindPath(CurrentGridPos, targetGridPos, _obstacleMask);
                
                if (path != null && path.Count > 0)
                {
                    _currentPath = path;
                    _pathIndex = 0;
                    _isMoving = true;
                }
            }
        }

        private void HandleMovement()
        {
            if (!_isMoving || _currentPath == null || _pathIndex >= _currentPath.Count)
            {
                _isMoving = false;
                return;
            }

            Vector2Int nextTile = _currentPath[_pathIndex];
            Vector2 targetPos = new Vector2(nextTile.x, nextTile.y);

            // 다음 타일에 몬스터 등의 상호작용 객체가 있는지 확인
            Collider2D hit = Physics2D.OverlapCircle(targetPos, 0.2f);
            if (hit != null && hit.TryGetComponent<IInteractable>(out var interactable))
            {
                // 타겟이 상호작용 객체면 더 이상 전진하지 않고 이동을 멈춘 후 상호작용 실행
                _isMoving = false;
                interactable.Interact(gameObject);
                _currentPath = null;
                return;
            }

            // 타일 중앙을 향해 이동
            transform.position = Vector2.MoveTowards(transform.position, targetPos, _moveSpeed * Time.deltaTime);

            // 타일 중앙에 도착했으면 다음 타일로 인덱스 증가
            if (Vector2.Distance(transform.position, targetPos) <= 0.01f)
            {
                transform.position = targetPos; // 완벽한 정수 좌표 보정
                _pathIndex++;
            }
        }
    }
}
