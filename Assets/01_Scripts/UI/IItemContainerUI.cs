using UnityEngine;
using BagBack100.Data.Core;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 슬롯 컨테이너 UI 간 아이템 이동 계약 (드래그 앤 드롭, 더블클릭, 우클릭).
    /// Dependencies: RuntimeItem, DraggableItemUI
    /// Events: 없음
    /// </summary>
    public interface IItemContainerUI
    {
        bool CanPlaceItem(RuntimeItem item, int slotIndex);
        void MoveItemFrom(RuntimeItem item, int targetSlotIndex, IItemContainerUI sourceContainer);
        void RemoveItem(RuntimeItem item);
        int GetSlotIndex(Transform cellTransform);

        void OnItemBeginDrag(DraggableItemUI draggedItem, UnityEngine.EventSystems.PointerEventData eventData);
        void OnItemDrag(DraggableItemUI draggedItem, UnityEngine.EventSystems.PointerEventData eventData);
        void OnItemEndDrag(DraggableItemUI draggedItem, UnityEngine.EventSystems.PointerEventData eventData);
        void OnItemDoubleClicked(DraggableItemUI clickedItem);
        void OnItemRightClicked(DraggableItemUI clickedItem, UnityEngine.EventSystems.PointerEventData eventData);
    }
}
