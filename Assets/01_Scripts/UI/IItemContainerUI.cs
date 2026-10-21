using UnityEngine;
using BagBack100.Data.Core;

namespace BagBack100.UI
{
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
    }
}
