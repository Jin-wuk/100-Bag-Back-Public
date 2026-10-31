using System;
using System.Collections.Generic;

namespace BagBack100.Data.Core
{
    /// <summary>
    /// Role: 슬롯형 아이템 보관 매니저(인벤토리·창고·보안 파우치)의 공통 커맨드 계약.
    ///       공용 UI(ItemContainerUIBase)가 구체 매니저 타입을 몰라도 그리드를 그리고 아이템을 옮길 수 있게 한다.
    /// Dependencies: RuntimeItem
    /// Events: OnContainerChanged — 슬롯 내용 또는 용량이 바뀌었을 때
    /// </summary>
    public interface ISlotItemContainer
    {
        event Action OnContainerChanged;

        int MaxSlots { get; }
        IReadOnlyList<RuntimeItem> GetAllItems();
        RuntimeItem GetItemAtSlot(int slotIndex);

        bool CanAddItem(RuntimeItem item);
        bool CanPlaceItemAtSlot(RuntimeItem item, int targetSlot);
        bool AddItemAt(RuntimeItem item, int targetSlot);
        bool TryAddNextAvailableSlot(RuntimeItem item);
        void RemoveItem(RuntimeItem item);
    }
}
