using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DimensionLooter.Data.Core;
using DimensionLooter.Player;

namespace DimensionLooter.UI
{
    public class EquipmentSlotUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private EquipType _slotType;
        [SerializeField] private Image _itemIcon;
        [SerializeField] private Sprite _defaultIcon;

        private EquipmentManager _equipmentManager;
        private RuntimeItem _currentItem;

        public EquipType SlotType => _slotType;

        public void Setup(EquipmentManager manager)
        {
            _equipmentManager = manager;
        }

        public void UpdateSlot(RuntimeItem item)
        {
            _currentItem = item;
            
            if (_currentItem != null)
            {
                _itemIcon.sprite = _currentItem.ItemData.itemIcon;
                _itemIcon.color = Color.white;
            }
            else
            {
                _itemIcon.sprite = _defaultIcon;
                _itemIcon.color = _defaultIcon != null ? Color.white : new Color(1,1,1,0);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.clickCount == 2 && eventData.button == PointerEventData.InputButton.Left)
            {
                if (_currentItem != null && _equipmentManager != null)
                {
                    // Unequip item
                    _equipmentManager.Unequip(_slotType);
                }
            }
        }
    }
}
