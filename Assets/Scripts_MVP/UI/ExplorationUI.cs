// File: Assets/Scripts/UI/ExplorationUI.cs
using UnityEngine;
using UnityEngine.UI;
using DimensionLooter.Gameplay.Combat;
using DimensionLooter.UI.Inventory;

namespace DimensionLooter.UI
{
    /// <summary>
    /// 탐험 모드(전투 외 필드)에서 항상 떠있는 최소한의 상시 UI(Top-Left 스탯, Top-Right 가방 버튼)를 관리합니다.
    /// </summary>
    public class ExplorationUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerStats _playerStats;
        
        [Header("Mini HUD")]
        [SerializeField] private Slider _hpBar;
        [SerializeField] private Slider _mpBar;
        
        [Header("Buttons")]
        [SerializeField] private Button _bagButton;

        private void OnEnable()
        {
            if (_playerStats != null)
            {
                _playerStats.OnHealthChanged += UpdateHp;
                _playerStats.OnManaChanged += UpdateMp;
            }

            if (_bagButton != null)
            {
                _bagButton.onClick.AddListener(OnBagClicked);
            }
        }

        private void OnDisable()
        {
            if (_playerStats != null)
            {
                _playerStats.OnHealthChanged -= UpdateHp;
                _playerStats.OnManaChanged -= UpdateMp;
            }

            if (_bagButton != null)
            {
                _bagButton.onClick.RemoveListener(OnBagClicked);
            }
        }

        private void Start()
        {
            if (_playerStats != null)
            {
                UpdateHp(_playerStats.CurrentHealth, _playerStats.MaxHealth);
                UpdateMp(_playerStats.CurrentMana, _playerStats.MaxMana);
            }
        }

        private void UpdateHp(int current, int max)
        {
            if (_hpBar != null) _hpBar.value = (float)current / max;
        }

        private void UpdateMp(int current, int max)
        {
            if (_mpBar != null) _mpBar.value = (float)current / max;
        }

        private void OnBagClicked()
        {
            if (DualUIPanel.Instance != null)
            {
                DualUIPanel.Instance.ToggleInventory();
            }
        }
    }
}
