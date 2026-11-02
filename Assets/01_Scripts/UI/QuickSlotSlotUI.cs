using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using BagBack100.Data.Core;
using BagBack100.Player;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 퀵슬롯 한 칸의 뷰. 가방의 소모품을 드롭하면 등록(아이템은 가방에 그대로), 우클릭하면 등록 해제.
    ///       아이콘·수량·공용 쿨타임 오버레이만 그리고, 등록/사용은 QuickSlotManager 커맨드로 요청한다.
    /// Dependencies: QuickSlotManager, IItemDropTarget
    /// Events: 없음
    /// </summary>
    public class QuickSlotSlotUI : MonoBehaviour, IItemDropTarget, IPointerClickHandler
    {
        [SerializeField] private int _index;
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _countText;
        [Tooltip("공용 쿨타임 동안 채워지는 Filled 이미지 (fillAmount 1 → 0)")]
        [SerializeField] private Image _cooldownOverlay;

        private QuickSlotManager _manager;

        public int Index => _index;

        public void Bind(QuickSlotManager manager)
        {
            _manager = manager;
        }

        public void Refresh(Sprite icon, int count, bool isAssigned)
        {
            if (_iconImage != null)
            {
                _iconImage.sprite = icon;
                _iconImage.enabled = isAssigned && icon != null;
                // 등록은 되어 있지만 가방에 남은 수량이 없으면 흐리게 표시
                _iconImage.color = count > 0 ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
            if (_countText != null)
            {
                _countText.text = isAssigned ? count.ToString() : string.Empty;
            }
        }

        public void SetCooldownFill(float fill)
        {
            if (_cooldownOverlay != null) _cooldownOverlay.fillAmount = fill;
        }

        public bool CanAcceptDrop(RuntimeItem item, ISlotItemContainer source)
        {
            // 가방(인벤토리)에 있는 소모품만 등록 대상
            return _manager != null && source is InventoryManager && _manager.CanAssign(item);
        }

        public void AcceptDrop(RuntimeItem item, ISlotItemContainer source)
        {
            _manager?.Assign(_index, item);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right) _manager?.ClearSlot(_index);
        }
    }
}
