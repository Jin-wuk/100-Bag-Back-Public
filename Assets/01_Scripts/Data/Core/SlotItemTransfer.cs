namespace BagBack100.Data.Core
{
    /// <summary>
    /// Role: 두 슬롯 컨테이너 사이의 아이템 이동. 대상이 전량을 못 받으면(창고 10개 묶음 → 가방 한도 2) 들어갈 만큼만 떼어
    ///       옮기고 나머지는 원래 칸에 남긴다. UI 여러 곳에 복제돼 있던 "빼기 → 넣기 → 실패 시 복구"를 한 곳에 모은다 (트리거 4).
    /// Dependencies: ISlotItemContainer, RuntimeItem
    /// Events: 없음 (변경 알림은 각 컨테이너가 발행)
    /// </summary>
    public static class SlotItemTransfer
    {
        /// <summary>item을 from에서 to로 옮긴다. targetSlot &lt; 0이면 자동 배치. 옮긴 수량을 돌려준다(0 = 실패).</summary>
        public static int Move(ISlotItemContainer from, ISlotItemContainer to, RuntimeItem item, int targetSlot)
        {
            if (from == null || to == null || item == null || item.ItemData == null || ReferenceEquals(from, to)) return 0;

            int amount = to.GetAcceptableAmount(item, targetSlot);
            if (amount <= 0) return 0;

            if (amount >= item.Count)
            {
                // TryAddAuto가 병합하며 item.Count를 깎으므로 옮긴 수량은 먼저 저장해 둔다
                int moved = item.Count;
                int originalSlot = item.SlotIndex;
                from.RemoveItem(item);
                bool added = targetSlot < 0 ? to.TryAddNextAvailableSlot(item) : to.AddItemAt(item, targetSlot);
                if (!added)
                {
                    from.AddItemAt(item, originalSlot);
                    return 0;
                }
                return moved;
            }

            var piece = new RuntimeItem(item.ItemData) { Count = amount };
            bool pieceAdded = targetSlot < 0 ? to.TryAddNextAvailableSlot(piece) : to.AddItemAt(piece, targetSlot);
            if (!pieceAdded) return 0;
            from.RemoveAmount(item, amount);
            return amount;
        }
    }
}
