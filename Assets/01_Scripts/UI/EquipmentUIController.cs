using UnityEngine;
using BagBack100.Player;
using BagBack100.Data.Core;

namespace BagBack100.UI
{
    public class EquipmentUIController : MonoBehaviour
    {
        [SerializeField] private EquipmentManager _equipmentManager;
        [SerializeField] private System.Collections.Generic.List<EquipmentSlotUI> _slots;
        [SerializeField] private UIToast _toast;

        private void Awake()
        {
            EquipmentManager.OnManagerCreated += OnManagerCreated;

            var existing = _equipmentManager != null ? _equipmentManager : FindObjectOfType<EquipmentManager>();
            if (existing != null) OnManagerCreated(existing);
        }

        private void OnEnable()
        {
            var existing = _equipmentManager != null ? _equipmentManager : FindObjectOfType<EquipmentManager>();
            if (existing != null)
            {
                OnManagerCreated(existing);
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
            if (_equipmentManager != null)
            {
                _equipmentManager.OnEquipmentChanged -= RefreshUI;
                _equipmentManager.OnEquipmentFailed -= ShowErrorMessage;
            }

            _equipmentManager = manager;
            
            if (_equipmentManager != null)
            {
                _equipmentManager.OnEquipmentChanged += RefreshUI;
                _equipmentManager.OnEquipmentFailed += ShowErrorMessage;

                if (_slots != null)
                {
                    foreach (var slot in _slots)
                    {
                        if (slot != null) slot.Setup(_equipmentManager);
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
            if (_toast != null) _toast.Show(msg);
            else Debug.LogWarning($"[System] {msg}");
        }
    }
}
