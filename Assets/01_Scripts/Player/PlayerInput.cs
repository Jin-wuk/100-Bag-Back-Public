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
        public event Action OnAttackInput;
        public event Action OnSpecialAttackKeyPressed;
        public event Action OnInventoryToggle;
        
        public bool IsMovementBlocked { get; set; } = false;
        
        private Camera _mainCamera;
        private Grid _grid;
        private float _lastMoveInputTime;
        private float _moveInputDelay = 0.15f; // 연속 이동 입력 딜레이 방지

        private void Awake()
        {
            OnInputCreated?.Invoke(this);
        }

        private void Start()
        {
            _mainCamera = Camera.main;
            _grid = FindObjectOfType<Grid>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab))
            {
                OnInventoryToggle?.Invoke();
            }

            if (IsMovementBlocked || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            {
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
            
            // 에이밍 갱신 (마우스 커서 위치를 플레이어 기준 8방향 벡터로 산출)
            Vector3 dirToMouse = (mouseWorldPos - transform.position).normalized;
            OnAimInput?.Invoke(dirToMouse);

            // 공격 입력
            if (Input.GetMouseButtonDown(0))
            {
                OnAttackInput?.Invoke();
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
