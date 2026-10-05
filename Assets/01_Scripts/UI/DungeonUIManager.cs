using UnityEngine;
using BagBack100.Player;

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
            _playerInput = input;
            _playerInput.OnInventoryToggle += ToggleInventoryScreen;
        }

        private void ToggleInventoryScreen()
        {
            if (_inventoryScreenRoot == null) return;

            bool isActive = !_inventoryScreenRoot.activeSelf;
            
            // 순수하게 'I' 키로 인벤토리를 열 때는 보상 패널을 끔
            if (isActive && _rewardPanel != null)
            {
                _rewardPanel.SetActive(false);
            }

            _inventoryScreenRoot.SetActive(isActive);

            if (_playerInput != null)
            {
                _playerInput.IsMovementBlocked = isActive;
            }
        }

        /// <summary>
        /// 추후 전투 종료나 상자 오픈 시 호출할 메서드.
        /// 3단 패널(장비, 가방, 보상)을 모두 활성화하여 띄웁니다.
        /// </summary>
        public void OpenRewardScreen()
        {
            if (_inventoryScreenRoot == null) return;

            if (_rewardPanel != null)
            {
                _rewardPanel.SetActive(true);
            }
            
            _inventoryScreenRoot.SetActive(true);

            if (_playerInput != null)
            {
                _playerInput.IsMovementBlocked = true;
            }
        }
    }
}
