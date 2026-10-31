using UnityEngine;
using BagBack100.Player;
using BagBack100.Core;

namespace BagBack100.UI
{
    public class DungeonUIManager : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject _inventoryScreenRoot;
        [Tooltip("상자 오픈 등 특정 상황에서만 켜질 보상 패널 (루트의 자식이어야 함)")]
        [SerializeField] private GameObject _rewardPanel;

        private PlayerInput _playerInput;

        private void Awake()
        {
            PlayerInput.OnInputCreated += OnInputCreated;
            
            if (_playerInput == null)
            {
                var existing = FindObjectOfType<PlayerInput>();
                if (existing != null) OnInputCreated(existing);
            }

            if (_inventoryScreenRoot != null)
                _inventoryScreenRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            PlayerInput.OnInputCreated -= OnInputCreated;

            if (_playerInput != null)
            {
                _playerInput.OnInventoryToggle -= ToggleInventoryScreen;
            }
        }

        private void OnInputCreated(PlayerInput input)
        {
            if (_playerInput != null)
            {
                _playerInput.OnInventoryToggle -= ToggleInventoryScreen;
            }
            _playerInput = input;
            if (_playerInput != null)
            {
                _playerInput.OnInventoryToggle += ToggleInventoryScreen;
            }
        }

        private void Update()
        {
            if (_inventoryScreenRoot != null && _inventoryScreenRoot.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseInventoryScreen();
            }
        }

        private void ToggleInventoryScreen()
        {
            if (_inventoryScreenRoot == null) return;

            bool open = !_inventoryScreenRoot.activeSelf;

            // 순수하게 'Tab' 키로 인벤토리를 열 때는 보상 패널을 끔
            if (open && _rewardPanel != null)
            {
                _rewardPanel.SetActive(false);
            }

            SetInventoryOpen(open);
        }

        public void CloseInventoryScreen()
        {
            SetInventoryOpen(false);
        }

        /// <summary>
        /// 추후 전투 종료나 상자 오픈 시 호출할 메서드.
        /// 3단 패널(장비, 가방, 보상)을 모두 활성화하여 띄웁니다.
        /// </summary>
        public void OpenRewardScreen()
        {
            if (_rewardPanel != null)
            {
                _rewardPanel.SetActive(true);
            }
            SetInventoryOpen(true);
        }

        /// <summary>
        /// 가방 패널의 열림/닫힘을 바꾸는 유일한 경로. 여는 경로(Tab, 보상)와 닫는 경로(Tab, Esc)가 갈라지면
        /// 어느 한쪽에서 일시정지 해제를 빠뜨려 게임이 멈춘 채 남을 수 있어 여기로 모았다.
        /// </summary>
        private void SetInventoryOpen(bool open)
        {
            if (_inventoryScreenRoot == null) return;

            _inventoryScreenRoot.SetActive(open);

            if (_playerInput != null)
            {
                _playerInput.IsMovementBlocked = open;
            }

            if (open) GamePauseService.Request(this);
            else GamePauseService.Release(this);
        }

        private void OnDisable()
        {
            GamePauseService.Release(this);
        }
    }
}
