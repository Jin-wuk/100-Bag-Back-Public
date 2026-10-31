using UnityEngine;
using BagBack100.Data.Core;
using BagBack100.Player;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 휴지통 칸. 가방 아이템을 드롭하면 수량 선택 + 삭제 확인창을 띄우고, 확인 시 영구 삭제를 요청한다.
    /// Dependencies: InventoryManager, ItemQuantityDialogUI, IItemDropTarget
    /// Events: 없음
    /// </summary>
    public class TrashSlotUI : MonoBehaviour, IItemDropTarget
    {
        [SerializeField] private ItemQuantityDialogUI _dialog;

        public bool CanAcceptDrop(RuntimeItem item, ISlotItemContainer source)
        {
            return item != null && source is InventoryManager && _dialog != null;
        }

        public void AcceptDrop(RuntimeItem item, ISlotItemContainer source)
        {
            if (!(source is InventoryManager inventory)) return;

            _dialog.Open($"Delete {item.ItemData.itemName} permanently", item.Count, "Delete",
                amount => inventory.DeleteItem(item, amount));
        }
    }
}
