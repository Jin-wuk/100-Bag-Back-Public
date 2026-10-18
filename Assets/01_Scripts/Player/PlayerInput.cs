using System;
using UnityEngine;
using UnityEngine.EventSystems;
using BagBack100.Core;
using BagBack100.UI;

namespace BagBack100.Player
{
    public class PlayerInput : MonoBehaviour
    {
        public static event Action<PlayerInput> OnInputCreated;

        public event Action<Vector2Int> OnMoveInput;
        public event Action<Vector3> OnAimInput;
        public event Action<Vector3Int> OnAttackInput;
        public event Action OnChargeStartInput;
        public event Action OnChargeReleaseInput;
        public event Action OnSpecialAttackKeyPressed;
        public event Action OnInventoryToggle;
        
        public bool IsMovementBlocked { get; set; } = false;
        
        private Camera _mainCamera;
        private Grid _grid;
        private BagBack100.UI.MovementPreview _combatPreview;
        private PlayerStatManager _stats;
        private PlayerController _playerController;
        
        private float _lastMoveInputTime;
        private float _moveInputDelay = 0.15f; // 연속 이동 입력 딜레이 방지

        private void Awake()
        {
            OnInputCreated?.Invoke(this);
            _stats = GetComponent<PlayerStatManager>();
            _playerController = GetComponent<PlayerController>();
        }

        private void Start()
        {
            _mainCamera = Camera.main;
            _grid = FindObjectOfType<Grid>();
            _combatPreview = FindObjectOfType<BagBack100.UI.MovementPreview>(true);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                OnInventoryToggle?.Invoke();
            }

            if (IsMovementBlocked || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            {
                if (_combatPreview != null) _combatPreview.Hide();
                return;
            }

            HandleAimAndAttackInput();
            HandleKeyboardMoveInput();
        }

        private void HandleKeyboardMoveInput()
        {
            // WASD 이동 처리
            Vector2Int moveDir = Vector2Int.zero;
            
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) moveDir = Vector2Int.up;
            else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) moveDir = Vector2Int.down;
            else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) moveDir = Vector2Int.left;
            else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) moveDir = Vector2Int.right;

            if (moveDir != Vector2Int.zero && Time.time - _lastMoveInputTime > _moveInputDelay)
            {
                _lastMoveInputTime = Time.time;
                OnMoveInput?.Invoke(moveDir);
            }

            if (Input.GetKeyDown(KeyCode.Z))
            {
                OnSpecialAttackKeyPressed?.Invoke();
            }
        }

        private void HandleAimAndAttackInput()
        {
            Vector3 mouseWorldPos = GetMouseWorldPosition();
            
            // 8방향 에이밍 갱신
            Vector3 dirToMouse = (mouseWorldPos - transform.position).normalized;
            OnAimInput?.Invoke(dirToMouse);

            // 타일 프리뷰 갱신
            if (_grid != null)
            {
                Vector3Int playerCell = _grid.WorldToCell(transform.position);
                Vector3Int mouseCell = _grid.WorldToCell(mouseWorldPos);
                
                int dist = Mathf.Max(Mathf.Abs(playerCell.x - mouseCell.x), Mathf.Abs(playerCell.y - mouseCell.y));
                bool isInRange = dist > 0 && dist <= _stats.CurrentRange;
                
                if (_combatPreview != null)
                {
                    if (_playerController != null && _playerController.CurrentState == PlayerState.Charging)
                    {
                        Vector3 facing = _playerController.FacingDirection;
                        Vector2Int stepDir = new Vector2Int(Mathf.RoundToInt(facing.x), Mathf.RoundToInt(facing.y));
                        if (stepDir == Vector2Int.zero) stepDir = Vector2Int.down;

                        _combatPreview.ShowChargeLinePreview(
                            _grid,
                            playerCell,
                            stepDir,
                            _playerController.ChargeLineLength,
                            _playerController.ChargeProgress
                        );
                    }
                    else
                    {
                        _combatPreview.ShowPreview(_grid.GetCellCenterWorld(mouseCell), isInRange);
                    }
                }

                // 좌클릭: 일반 콤보 공격 입력 (사거리 내일 때만 허용)
                if (Input.GetMouseButtonDown(0))
                {
                    if (isInRange)
                    {
                        OnAttackInput?.Invoke(mouseCell);
                    }
                }

                // 우클릭: 홀드 차징 강공격 입력
                if (Input.GetMouseButtonDown(1))
                {
                    OnChargeStartInput?.Invoke();
                }
            }

            if (Input.GetMouseButtonUp(1))
            {
                OnChargeReleaseInput?.Invoke();
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
    }
}
