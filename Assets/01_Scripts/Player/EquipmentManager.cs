using System;
using System.Collections.Generic;
using UnityEngine;
using DimensionLooter.Data.Core;

namespace DimensionLooter.Player
{
    [RequireComponent(typeof(InventoryManager))]
    public class EquipmentManager : MonoBehaviour
    {
        public static event Action<EquipmentManager> OnManagerCreated;

        public event Action OnEquipmentChanged;
        public event Action<string> OnEquipmentFailed;

        private Dictionary<EquipType, RuntimeItem> _equippedItems = new Dictionary<EquipType, RuntimeItem>();
        private InventoryManager _inventoryManager;

        private void Awake()
        {
            _inventoryManager = GetComponent<InventoryManager>();
            OnManagerCreated?.Invoke(this);
        }

        private void Start()
        {
            // Removed direct UI finding, now relying on OnManagerCreated
        }

        public bool TryEquip(RuntimeItem item)
        {
            if (item.ItemData.category != ItemCategory.Equipment)
                return false;

            EquipType type = item.ItemData.equipType;
            int originalX = item.GridX;
            int originalY = item.GridY;

            // 기존에 장착된 동일 부위 아이템이 있다면 먼저 해제 시도 (인벤토리 공간 확보를 위해 장착할 아이템을 먼저 치워둠)
            if (_equippedItems.TryGetValue(type, out RuntimeItem oldItem) && oldItem != null)
            {
                _inventoryManager.RemoveItem(item);
                
                if (!Unequip(type))
                {
                    // 해제 실패 (예: 가방 공간 부족) 시 원래 상태로 복구
                    _inventoryManager.AddItemAt(item, originalX, originalY);
                    return false;
                }
            }
            else
            {
                _inventoryManager.RemoveItem(item);
            }

            if (type == EquipType.Backpack)
            {
                int newHeight = item.ItemData.bagHeight;
                if (newHeight < _inventoryManager.GridHeight)
                {
                    OnEquipmentFailed?.Invoke("교체하려는 가방의 크기가 작습니다.");
                    return false;
                }
                
                _inventoryManager.UpdateBagSize(newHeight);
            }

            _equippedItems[type] = item;
            
            OnEquipmentChanged?.Invoke();
            return true;
        }

        public bool Unequip(EquipType type)
        {
            if (_equippedItems.TryGetValue(type, out RuntimeItem oldItem) && oldItem != null)
            {
                if (!_inventoryManager.TryAddNextAvailableSlot(oldItem))
                {
                    OnEquipmentFailed?.Invoke("가방에 빈 공간이 없습니다.");
                    return false;
                }

                if (type == EquipType.Backpack)
                {
                    _inventoryManager.UpdateBagSize(4);
                }
                _equippedItems.Remove(type);
                OnEquipmentChanged?.Invoke();
                return true;
            }
            return false;
        }

        public RuntimeItem GetEquippedItem(EquipType type)
        {
            return _equippedItems.TryGetValue(type, out var item) ? item : null;
        }

        public (int hp, int mp, int atk, int def, float speed, int range) GetTotalStats()
        {
            int hp = 0, mp = 0, atk = 0, def = 0, range = 0;
            float speed = 0f;
            foreach (var item in _equippedItems.Values)
            {
                if (item != null && item.ItemData != null)
                {
                    hp += item.ItemData.hpBonus;
                    mp += item.ItemData.mpBonus;
                    atk += item.ItemData.attackBonus;
                    def += item.ItemData.defenseBonus;
                    speed += item.ItemData.speedBonus;
                    range += item.ItemData.rangeBonus;
                }
            }
            return (hp, mp, atk, def, speed, range);
        }
    }
}
