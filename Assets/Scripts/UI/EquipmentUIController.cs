using UnityEngine;
using DimensionLooter.Player;
using DimensionLooter.Data.Core;

namespace DimensionLooter.UI
{
    public class EquipmentUIController : MonoBehaviour
    {
        [SerializeField] private EquipmentManager _equipmentManager;
        [SerializeField] private System.Collections.Generic.List<EquipmentSlotUI> _slots;

        private void Awake()
        {
            EquipmentManager.OnManagerCreated += OnManagerCreated;

            if (_equipmentManager == null)
            {
                var existing = FindObjectOfType<EquipmentManager>();
                if (existing != null) OnManagerCreated(existing);
            }
        }

        private void OnDestroy()
        {
            EquipmentManager.OnManagerCreated -= OnManagerCreated;

            if (_equipmentManager != null)
            {
                _equipmentManager.OnEquipmentChanged -= RefreshUI;
                _equipmentManager.OnEquipmentFailed -= ShowErrorMessage;
            }
        }

        private void OnManagerCreated(EquipmentManager manager)
        {
            _equipmentManager = manager;
            
            if (_equipmentManager != null)
            {
                _equipmentManager.OnEquipmentChanged += RefreshUI;
                _equipmentManager.OnEquipmentFailed += ShowErrorMessage;

                if (_slots != null)
                {
                    foreach (var slot in _slots)
                    {
                        slot.Setup(_equipmentManager);
                    }
                }
                
                RefreshUI();
            }
        }

        private void RefreshUI()
        {
            if (_equipmentManager == null || _slots == null) return;

            foreach (var slot in _slots)
            {
                var item = _equipmentManager.GetEquippedItem(slot.SlotType);
                slot.UpdateSlot(item);
            }
        }

        private void ShowErrorMessage(string msg)
        {
            Debug.LogWarning($"[System] {msg}");
            // 화면 중앙 플로팅 텍스트 띄우기 로직 연동 가능
        }
    }
}
