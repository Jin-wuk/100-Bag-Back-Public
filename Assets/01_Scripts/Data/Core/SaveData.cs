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
    /// 씬 전환(02_HubScene <-> 03_DungeonScene) 간 인벤토리, 장착 장비, 창고 데이터를 보존하는 런타임 세션 컨테이너.
    /// </summary>
    public static class PlayerSessionData
    {
        public static List<RuntimeItem> InventoryItems { get; private set; } = new List<RuntimeItem>();
        public static Dictionary<EquipType, RuntimeItem> EquippedItems { get; private set; } = new Dictionary<EquipType, RuntimeItem>();
        public static List<RuntimeItem> StorageItems { get; private set; } = new List<RuntimeItem>();

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

        public static void GetEquippedBackpackBonus(out int addedSlots, out float addedWeight)
        {
            addedSlots = 0;
            addedWeight = 0f;

            if (EquippedItems.TryGetValue(EquipType.Backpack, out var bagItem) &&
                bagItem != null &&
                bagItem.ItemData is BackpackDataSO bagData)
            {
                addedSlots = bagData.bagAddedSlots;
                addedWeight = bagData.bagMaxWeight;
            }
        }

        /// <summary>
        /// 플레이어 사망 시 가방(인벤토리) 및 장착 중인 모든 장비를 증발시킵니다. (허브 창고 아이템은 보존)
        /// </summary>
        public static void ClearPlayerLoadoutOnDeath()
        {
            InventoryItems.Clear();
            EquippedItems.Clear();
        }
    }
}
