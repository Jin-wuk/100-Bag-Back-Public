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

        /// <summary>외부 아이템을 몇 개까지 받을 수 있는지. targetSlot &lt; 0이면 자동 배치 기준 (부분 이동용, SlotItemTransfer)</summary>
        int GetAcceptableAmount(RuntimeItem item, int targetSlot);
        bool CanPlaceItemAtSlot(RuntimeItem item, int targetSlot);
        bool AddItemAt(RuntimeItem item, int targetSlot);
        bool TryAddNextAvailableSlot(RuntimeItem item);
        void RemoveItem(RuntimeItem item);

        /// <summary>스택에서 amount개를 덜어낸다. 전부면 슬롯에서 제거 (판매·삭제용)</summary>
        bool RemoveAmount(RuntimeItem item, int amount);

        /// <summary>같은 itemID의 총 수량 (여러 묶음 합계)</summary>
        int CountItemId(int itemID);

        /// <summary>같은 itemID를 여러 묶음에서 amount개 덜어낸다. 합계가 모자라면 바꾸지 않고 false (제작 재료 차감)</summary>
        bool RemoveItemIdAmount(int itemID, int amount);

        /// <summary>
        /// Begin~End 사이의 변경 알림·세션 저장을 미루고 End에서 바뀐 것이 있으면 1회만 보낸다 (중첩 가능).
        /// 여러 컨테이너를 한 번에 바꾸는 제작에서 UI가 중간 상태를 보거나 여러 번 갱신하지 않게 한다.
        /// </summary>
        void BeginBulkChange();
        void EndBulkChange();
    }
}
