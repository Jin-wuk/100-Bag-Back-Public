using BagBack100.Data.Core;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 아이템을 다른 컨테이너로 "옮기지 않고" 소비하거나 참조만 하는 드롭 대상 (휴지통, 판매 칸, 퀵슬롯 등).
    ///       IItemContainerUI(슬롯 간 이동)와 성격이 달라 분리했다. 드래그 종료 시 컨테이너보다 먼저 검사된다.
    /// Dependencies: RuntimeItem, ISlotItemContainer
    /// Events: 없음
    /// </summary>
    public interface IItemDropTarget
    {
        bool CanAcceptDrop(RuntimeItem item, ISlotItemContainer source);

        /// <summary>드롭 처리. 원본 컨테이너에서 아이템을 뺄지 여부는 구현체가 커맨드로 결정한다.</summary>
        void AcceptDrop(RuntimeItem item, ISlotItemContainer source);
    }
}
