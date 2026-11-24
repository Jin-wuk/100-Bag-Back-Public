using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using BagBack100.Data.Core;
using BagBack100.Player;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 장비 칸 한 개. 장착 아이템 아이콘 표시, 빈 칸이면 부위 실루엣 표시, 더블클릭 해제 요청.
    /// Dependencies: EquipmentManager
    /// Events: 없음
    /// </summary>
    public class EquipmentSlotUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private EquipType _slotType;
        [SerializeField] private Image _itemIcon;
        [SerializeField] private Sprite _defaultIcon;
        [Tooltip("빈 칸일 때만 보이는 부위 실루엣(ui_equip_hint_*). 비우면 생략")]
        [SerializeField] private Image _hintOverlay;

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
            if (_hintOverlay != null) _hintOverlay.enabled = _currentItem == null;
            
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
            // 수량 창이 떠 있는 동안 해제로 가방 칸이 바뀌지 않게 한다
            if (ItemQuantityDialogUI.IsAnyOpen) return;
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
