using System;
using System.Collections.Generic;
using BagBack100.Data.Models;

namespace BagBack100.Data.Core
{
    [Serializable]
    public class InventorySaveData
    {
        // 런타임 저장용 POCO
        public List<RuntimeItem> StoredItems = new List<RuntimeItem>();
    }

    [Serializable]
    public class EquipmentSaveData
    {
        // 부위별 장착된 아이템의 InstanceID 또는 RuntimeItem 참조를 저장
        // Dictionary는 직렬화가 까다로우므로 List 구조체 방식을 사용하거나 별도의 직렬화 구현 필요
        public List<EquipSlotData> EquippedSlots = new List<EquipSlotData>();
    }

    [Serializable]
    public class EquipSlotData
    {
        public EquipType Type;
        public RuntimeItem EquippedItem;
    }

    /// <summary>
    /// Role: 씬 전환(02_HubScene <-> 03_DungeonScene) 간 인벤토리, 장착 장비, 창고, 보안 파우치, 퀵슬롯 등록, 골드 지갑, 층계 던전 진행(지역·층), 테크트리 해금 노드·지역 진행도를 보존하는 런타임 세션 컨테이너.
    /// Dependencies: RuntimeItem, BackpackDataSO
    /// Events: OnGoldChanged(gold)
    /// </summary>
    public static class PlayerSessionData
    {
        public static List<RuntimeItem> InventoryItems { get; private set; } = new List<RuntimeItem>();
        public static Dictionary<EquipType, RuntimeItem> EquippedItems { get; private set; } = new Dictionary<EquipType, RuntimeItem>();
        public static List<RuntimeItem> StorageItems { get; private set; } = new List<RuntimeItem>();
        // 직전 던전 결과. 허브 결과 화면이 한 번 보여준 뒤 null로 비운다
        public static RunResult LastRunResult { get; set; }

        // 보유 골드(지갑). 가방에 들어가지 않으므로 사망해도 잃지 않는다. 골드 가치가 있는 전리품은 허브에서 팔아야 지갑으로 들어온다
        public const int StartingGold = 1000;
        public static int Gold { get; private set; } = StartingGold;
        public static event Action<int> OnGoldChanged;

        public static void AddGold(int amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            OnGoldChanged?.Invoke(Gold);
        }

        public static bool TrySpendGold(int amount)
        {
            if (amount < 0 || Gold < amount) return false;
            Gold -= amount;
            OnGoldChanged?.Invoke(Gold);
            return true;
        }

        // 층계 던전 진행: 현재 지역 번호와 층(1~10). 던전 입장 시 1층으로 시작한다 (결과 화면 표시용)
        public static int CurrentRegionId { get; private set; }
        public static int CurrentFloor { get; private set; }

        public static void SetCurrentFloor(int regionId, int floor)
        {
            CurrentRegionId = regionId;
            CurrentFloor = floor;
        }

        // 제작소 테크트리 해금 노드 ID. 사망과 무관하게 유지된다 (MVP는 세션 메모리만, 세이브 연동은 MVP 밖)
        private static readonly HashSet<string> _unlockedNodeIds = new HashSet<string>();

        public static bool IsNodeUnlocked(string nodeId)
        {
            return !string.IsNullOrEmpty(nodeId) && _unlockedNodeIds.Contains(nodeId);
        }

        // 새로 기록했으면 true, 이미 해금돼 있었거나 ID가 비었으면 false
        public static bool MarkNodeUnlocked(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return false;
            return _unlockedNodeIds.Add(nodeId);
        }

        // 지역 진행도: 1번부터 이 수까지 해금. MVP는 지역 1만 있으므로 1로 고정하고, 늘리는 API는 F8(지역 확장) 때 추가한다
        public static int UnlockedRegionCount { get; private set; } = 1;

        public static bool IsRegionUnlocked(int regionId)
        {
            return regionId >= 1 && regionId <= UnlockedRegionCount;
        }

        // 보안 파우치: 사망해도 비우지 않는다
        public static List<RuntimeItem> PouchItems { get; private set; } = new List<RuntimeItem>();

        // 퀵슬롯 1·2·3에 연결된 소모품 itemID (0 = 비어 있음). 아이템 자체가 아니라 종류로 연결해 스택이 바뀌어도 유지된다
        public const int QuickSlotCount = 3;
        public static int[] QuickSlotItemIds { get; private set; } = new int[QuickSlotCount];
        // 가방에 해당 소모품이 하나도 없을 때(씬 전환 후 포함)도 퀵슬롯에 흐린 아이콘을 그리기 위해 등록 시점의 아이콘을 보존
        public static UnityEngine.Sprite[] QuickSlotIcons { get; private set; } = new UnityEngine.Sprite[QuickSlotCount];

        public static void SaveInventory(IEnumerable<RuntimeItem> items)
        {
            InventoryItems.Clear();
            if (items == null) return;

            foreach (var item in items)
            {
                if (item != null && item.ItemData != null)
                {
                    InventoryItems.Add(item);
                }
            }
        }

        public static void SaveEquipment(Dictionary<EquipType, RuntimeItem> equipped)
        {
            EquippedItems.Clear();
            if (equipped == null) return;

            foreach (var kvp in equipped)
            {
                if (kvp.Value != null && kvp.Value.ItemData != null)
                {
                    EquippedItems[kvp.Key] = kvp.Value;
                }
            }
        }

        public static void SavePouch(IEnumerable<RuntimeItem> items)
        {
            PouchItems.Clear();
            if (items == null) return;

            foreach (var item in items)
            {
                if (item != null && item.ItemData != null)
                {
                    PouchItems.Add(item);
                }
            }
        }

        public static void SaveStorage(IEnumerable<RuntimeItem> items)
        {
            StorageItems.Clear();
            if (items == null) return;

            foreach (var item in items)
            {
                if (item != null && item.ItemData != null)
                {
                    StorageItems.Add(item);
                }
            }
        }

        public static void GetEquippedBackpackBonus(out int addedSlots)
        {
            addedSlots = 0;

            if (EquippedItems.TryGetValue(EquipType.Backpack, out var bagItem) &&
                bagItem != null &&
                bagItem.ItemData is BackpackDataSO bagData)
            {
                addedSlots = bagData.bagAddedSlots;
            }
        }

        /// <summary>
        /// 플레이어 사망 시 가방(인벤토리) 및 장착 중인 모든 장비를 증발시킵니다. (허브 창고·보안 파우치 아이템은 보존)
        /// </summary>
        public static void ClearPlayerLoadoutOnDeath()
        {
            InventoryItems.Clear();
            EquippedItems.Clear();
        }
    }
}
