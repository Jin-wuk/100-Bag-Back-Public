using System.Collections.Generic;

namespace BagBack100.Data.Core
{
    /// <summary>
    /// Role: 던전 한 판의 결과 기록(POCO). 탈출이면 가져온 전리품과 총 가치, 사망이면 잃은 것과 보안 파우치로 지킨 것을 담는다.
    ///       허브 결과 화면이 한 번 보여준 뒤 비운다.
    /// Dependencies: RuntimeItem, PlayerSessionData
    /// Events: 없음
    /// </summary>
    public class RunResult
    {
        public bool IsExtracted { get; private set; }
        public List<RuntimeItem> BroughtBack { get; } = new List<RuntimeItem>(); // 탈출: 가방 + 보안 파우치
        public List<RuntimeItem> Lost { get; } = new List<RuntimeItem>();        // 사망: 가방 + 장착 장비
        public List<RuntimeItem> Saved { get; } = new List<RuntimeItem>();       // 사망: 보안 파우치
        public int TotalValue { get; private set; }                             // 탈출: 가져온 전리품 Σ baseValue × Count, 사망: 잃은 것의 가치
        public int RegionId { get; private set; }                               // 탈출·사망한 지역 번호
        public int Floor { get; private set; }                                  // 탈출·사망한 층 (0 = 던전 층 정보 없음)

        /// <summary>
        /// 현재 세션 상태를 복사해 결과를 만든다. 사망 처리는 세션 목록을 그 자리에서 비우므로 반드시 비우기 전에 호출해야 한다.
        /// </summary>
        public static RunResult CreateFromSession(bool isExtracted)
        {
            var result = new RunResult
            {
                IsExtracted = isExtracted,
                RegionId = PlayerSessionData.CurrentRegionId,
                Floor = PlayerSessionData.CurrentFloor
            };

            if (isExtracted)
            {
                AddSnapshots(result.BroughtBack, PlayerSessionData.InventoryItems);
                AddSnapshots(result.BroughtBack, PlayerSessionData.PouchItems);
                result.TotalValue = SumValue(result.BroughtBack);
            }
            else
            {
                AddSnapshots(result.Lost, PlayerSessionData.InventoryItems);
                AddSnapshots(result.Lost, PlayerSessionData.EquippedItems.Values);
                AddSnapshots(result.Saved, PlayerSessionData.PouchItems);
                result.TotalValue = SumValue(result.Lost);
            }
            return result;
        }

        // 결과 화면이 떠 있는 동안 허브에서 판매·병합으로 원본 수량이 바뀌어도 기록이 변하지 않도록 사본을 저장한다
        private static void AddSnapshots(List<RuntimeItem> target, IEnumerable<RuntimeItem> source)
        {
            foreach (var item in source)
            {
                if (item?.ItemData == null) continue;
                target.Add(new RuntimeItem(item.ItemData) { Count = item.Count });
            }
        }

        private static int SumValue(List<RuntimeItem> items)
        {
            int total = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item?.ItemData != null) total += item.ItemData.baseValue * item.Count;
            }
            return total;
        }
    }
}
