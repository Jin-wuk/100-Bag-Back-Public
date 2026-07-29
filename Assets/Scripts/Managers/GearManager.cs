// File: Assets/Scripts/Managers/GearManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using DimensionLooter.Core;
using DimensionLooter.Data;
using DimensionLooter.Gameplay.Inventory;

namespace DimensionLooter.Managers
{
    public class GearManager : MonoBehaviour
    {
        public static GearManager Instance { get; private set; }

        private Dictionary<Define.ItemType, InventoryItem> _equippedGears = new Dictionary<Define.ItemType, InventoryItem>();

        // 장비 전체 스탯 갱신 이벤트 (공격력 증가량, 방어력 증가량)
        public event Action<int, int> OnGearStatsChanged;
        


        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public bool Equip(InventoryItem item, Define.ItemType slotType)
        {
            if (item == null || item.Data.Type != slotType) return false;

            _equippedGears[slotType] = item;

            if (slotType == Define.ItemType.Backpack)
            {
                if (InventoryManager.Instance != null)
                {
                    InventoryManager.Instance.EquipBackpack(item.Data);
                }
            }
            else
            {
                CalculateTotalStats();
            }

            Debug.Log($"[GearManager] {item.Data.Name} 장착됨 (슬롯: {slotType})");
            return true;
        }

        public InventoryItem Unequip(Define.ItemType slotType)
        {
            if (_equippedGears.TryGetValue(slotType, out InventoryItem item))
            {
                _equippedGears.Remove(slotType);

                if (slotType == Define.ItemType.Backpack)
                {
                    if (InventoryManager.Instance != null)
                    {
                        InventoryManager.Instance.EquipBackpack(null);
                    }
                }
                else
                {
                    CalculateTotalStats();
                }
                
                Debug.Log($"[GearManager] {item.Data.Name} 해제됨 (슬롯: {slotType})");
                return item;
            }
            return null;
        }

        public InventoryItem GetEquippedItem(Define.ItemType slotType)
        {
            _equippedGears.TryGetValue(slotType, out InventoryItem item);
            return item;
        }

        private void CalculateTotalStats()
        {
            int totalAtk = 0;
            int totalDef = 0;

            foreach (var kvp in _equippedGears)
            {
                if (kvp.Value != null && kvp.Value.Data != null)
                {
                    totalAtk += kvp.Value.Data.AttackPower;
                    totalDef += kvp.Value.Data.DefensePower;
                }
            }

            OnGearStatsChanged?.Invoke(totalAtk, totalDef);
        }
    }
}
