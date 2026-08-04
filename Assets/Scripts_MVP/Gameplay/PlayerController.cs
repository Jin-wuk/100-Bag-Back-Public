// File: Assets/Scripts/Gameplay/PlayerController.cs
using System;
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
        private Vector2Int? _pendingTargetGridPos;

        private Camera _mainCam;
        private Collider2D[] _interactOverlapResults = new Collider2D[4];
        private Collider2D[] _zocOverlapResults = new Collider2D[20];

        // [Observer] 시청각 매니저 연동용 이벤트 (VFX/SFX Decoupling)
        public event Action OnMoveStep;

        private Vector2Int CurrentGridPos => new Vector2Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.y));

        private void Start()
        {
            _mainCam = Camera.main;
        }

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
                Vector2 mousePos = _mainCam.ScreenToWorldPoint(Input.mousePosition);
                Vector2Int targetGridPos = new Vector2Int(Mathf.RoundToInt(mousePos.x), Mathf.RoundToInt(mousePos.y));

                if (targetGridPos == CurrentGridPos) return;

                if (!_isMoving)
                {
                    CalculatePath(CurrentGridPos, targetGridPos);
                }
                else
                {
                    // 이동 중 선회: 현재 타일 이동이 완료된 후 계산되도록 예약
                    _pendingTargetGridPos = targetGridPos;
                }
            }
        }

        private void CalculatePath(Vector2Int startPos, Vector2Int targetPos)
        {
            // A* 경로 탐색
            List<Vector2Int> path = Pathfinder.FindPath(startPos, targetPos, _obstacleMask);
            
            // 경로가 없으면(벽 클릭 등) 무시됨
            if (path != null && path.Count > 0)
            {
                _currentPath = path;
                _pathIndex = 0;
                _isMoving = true;
            }
            else
            {
                _isMoving = false;
                _currentPath = null;
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

            // 다음 타일에 상호작용 객체가 있는지 확인 (Zero GC)
            int hitCount = Physics2D.OverlapCircleNonAlloc(targetPos, 0.2f, _interactOverlapResults);
            for (int i = 0; i < hitCount; i++)
            {
                if (_interactOverlapResults[i].TryGetComponent<IInteractable>(out var interactable))
                {
                    _isMoving = false;
                    _currentPath = null;
                    interactable.Interact(gameObject);
                    return;
                }
            }

            // 타일 중앙을 향해 이동
            transform.position = Vector2.MoveTowards(transform.position, targetPos, _moveSpeed * Time.deltaTime);

            // 타일 중앙에 도착했으면 다음 타일로 인덱스 증가 및 ZOC 체크
            if (Vector2.Distance(transform.position, targetPos) <= 0.01f)
            {
                transform.position = targetPos; // 완벽한 정수 좌표 보정
                _pathIndex++;
                
                OnMoveStep?.Invoke();
                CheckZOC();

                // 이동 예약이 있다면 새 경로 탐색 (단, 전투 진입 등으로 _isMoving이 false가 되지 않았을 때만)
                if (_isMoving && _pendingTargetGridPos.HasValue)
                {
                    CalculatePath(CurrentGridPos, _pendingTargetGridPos.Value);
                    _pendingTargetGridPos = null;
                }
            }
        }

        private void CheckZOC()
        {
            // 전투 중이면 스킵
            if (Managers.CombatManager.Instance != null && Managers.CombatManager.Instance.IsInCombat)
            {
                _isMoving = false;
                _currentPath = null;
                return;
            }

            DimensionLooter.Gameplay.Combat.MonsterHealth bestMonster = null;
            float bestDist = float.MaxValue;
            string bestId = "";

            // 반경 10f 내의 모든 콜라이더 검색 (Zero GC)
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, 10f, _zocOverlapResults);
            
            for (int i = 0; i < count; i++)
            {
                if (_zocOverlapResults[i].TryGetComponent<DimensionLooter.Gameplay.Combat.MonsterHealth>(out var m))
                {
                    if (!m.IsAlive) continue;
                    
                    float dist = Vector2.Distance(transform.position, m.transform.position);
                    if (dist <= m.zocRadius)
                    {
                        string id = m.Data != null ? m.Data.Id : "";
                        
                        if (bestMonster == null)
                        {
                            bestMonster = m;
                            bestDist = dist;
                            bestId = id;
                        }
                        else
                        {
                            // 다중 충돌 중재: 1순위 거리(가까운순), 2순위 ID(알파벳순)
                            if (Mathf.Abs(dist - bestDist) > 0.1f)
                            {
                                if (dist < bestDist)
                                {
                                    bestMonster = m;
                                    bestDist = dist;
                                    bestId = id;
                                }
                            }
                            else if (id.CompareTo(bestId) < 0)
                            {
                                bestMonster = m;
                                bestDist = dist;
                                bestId = id;
                            }
                        }
                    }
                }
            }

            if (bestMonster != null)
            {
                // 최우선순위 1마리와 전투 진입, 이동 즉시 정지
                _isMoving = false;
                _currentPath = null;
                _pendingTargetGridPos = null;
                bestMonster.Interact(gameObject);
            }
        }
    }
}
