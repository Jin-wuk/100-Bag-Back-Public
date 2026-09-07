using System;
using UnityEngine;
using UnityEngine.EventSystems;
using DimensionLooter.Core;
using DimensionLooter.UI;

namespace DimensionLooter.Player
{
    public class PlayerInput : MonoBehaviour
    {
        public static event Action<PlayerInput> OnInputCreated;

        public event Action<Vector3> OnTileClicked;
        public event Action<Vector2Int> OnDirectionalInput;
        public event Action OnSpecialAttackKeyPressed;
        public event Action OnInventoryToggle;
        
        public bool IsMovementBlocked { get; set; } = false;
        
        [Header("Settings")]
        [Tooltip("경로 탐색 시 장애물로 인식할 레이어")]
        [SerializeField] private LayerMask _obstacleLayerMask;

        private Camera _mainCamera;
        private MovementPreview _movementPreview;
        private Grid _grid;

        private void Awake()
        {
            OnInputCreated?.Invoke(this);
        }

        private void Start()
        {
            _mainCamera = Camera.main;
            _grid = FindObjectOfType<Grid>();
            _movementPreview = FindObjectOfType<MovementPreview>(true);
            
            if (_movementPreview == null)
            {
                Debug.LogWarning("씬에 MovementPreview 오브젝트가 없습니다. 미리보기가 동작하지 않습니다.");
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.I))
            {
                OnInventoryToggle?.Invoke();
            }

            if (IsMovementBlocked || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            {
                if (_movementPreview != null) _movementPreview.Hide();
                return;
            }

            HandleKeyboardInput();
            HandleMouseInput();
        }

        private void HandleKeyboardInput()
        {
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                OnDirectionalInput?.Invoke(Vector2Int.up);
            else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                OnDirectionalInput?.Invoke(Vector2Int.down);
            else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                OnDirectionalInput?.Invoke(Vector2Int.left);
            else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                OnDirectionalInput?.Invoke(Vector2Int.right);

            if (Input.GetKeyDown(KeyCode.Z))
            {
                OnSpecialAttackKeyPressed?.Invoke();
            }
        }

        private void HandleMouseInput()
        {
            Vector3 mouseWorldPos = GetMouseWorldPosition();
            if (_grid == null) return;
            
            Vector3Int cellPos = _grid.WorldToCell(mouseWorldPos);
            Vector2Int tilePos = new Vector2Int(cellPos.x, cellPos.y);

            UpdatePreview(tilePos, cellPos);

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
            if (float.IsInfinity(mouseScreenPos.x) || float.IsInfinity(mouseScreenPos.y))
            {
                return transform.position;
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
