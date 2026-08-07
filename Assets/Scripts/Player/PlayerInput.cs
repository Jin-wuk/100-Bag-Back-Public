using System;
using UnityEngine;
using UnityEngine.EventSystems;
using DimensionLooter.Core;
using DimensionLooter.UI;

namespace DimensionLooter.Player
{
    public class PlayerInput : MonoBehaviour
    {
        public event Action<Vector3> OnTileClicked;
        public event Action OnInventoryToggle;
        
        public bool IsMovementBlocked { get; set; } = false;
        
        [Header("Settings")]
        [Tooltip("경로 탐색 시 장애물로 인식할 레이어")]
        [SerializeField] private LayerMask _obstacleLayerMask;

        private Camera _mainCamera;
        private MovementPreview _movementPreview;
        private Grid _grid;

        private void Start()
        {
            _mainCamera = Camera.main;
            _grid = FindObjectOfType<Grid>();

            // Awake에서 비활성화(Hide)될 수 있으므로, 비활성화된 객체도 찾도록 true 파라미터 추가
            _movementPreview = FindObjectOfType<MovementPreview>(true);
            
            if (_movementPreview == null)
            {
                Debug.LogWarning("씬에 MovementPreview 오브젝트가 없습니다. 미리보기가 동작하지 않습니다.");
            }
        }

        private void Update()
        {
            // 인벤토리 토글 키
            if (Input.GetKeyDown(KeyCode.I))
            {
                OnInventoryToggle?.Invoke();
            }

            // 이동 차단 상태이거나 UI 위를 클릭하거나 호버링 중일 때는 무시
            if (IsMovementBlocked || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            {
                if (_movementPreview != null)
                    _movementPreview.Hide();
                return;
            }

            Vector3 mouseWorldPos = GetMouseWorldPosition();
            if (_grid == null) return;
            
            Vector3Int cellPos = _grid.WorldToCell(mouseWorldPos);
            Vector2Int tilePos = new Vector2Int(cellPos.x, cellPos.y);

            // 프리뷰 업데이트
            UpdatePreview(tilePos, cellPos);

            // 마우스 좌클릭 시 이동 명령
            if (Input.GetMouseButtonDown(0))
            {
                bool isObstacle = Pathfinder.IsObstacle(_grid, tilePos, _obstacleLayerMask);
                if (!isObstacle)
                {
                    Vector3 targetPos = _grid.GetCellCenterWorld(cellPos);
                    OnTileClicked?.Invoke(targetPos);
                }
            }
        }

        private Vector3 GetMouseWorldPosition()
        {
            if (_mainCamera == null) return Vector3.zero;
            
            Vector3 mouseScreenPos = Input.mousePosition;
            
            // Unity 에디터 밖으로 마우스가 나갔을 때 무한대 값이 들어오는 예외 방지 (Guard Clause)
            if (float.IsInfinity(mouseScreenPos.x) || float.IsInfinity(mouseScreenPos.y))
            {
                return transform.position; // 안전한 기본값 반환
            }

            mouseScreenPos.z = -_mainCamera.transform.position.z;
            return _mainCamera.ScreenToWorldPoint(mouseScreenPos);
        }

        private void UpdatePreview(Vector2Int tilePos, Vector3Int cellPos)
        {
            if (_movementPreview == null || _grid == null) return;

            bool isObstacle = Pathfinder.IsObstacle(_grid, tilePos, _obstacleLayerMask);
            _movementPreview.ShowPreview(_grid.GetCellCenterWorld(cellPos), !isObstacle);
        }
    }
}
