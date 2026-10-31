using System;
using System.Collections.Generic;
using UnityEngine;
using BagBack100.Data.Core;

namespace BagBack100.Player
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
            RestoreFromSession();
            OnManagerCreated?.Invoke(this);
        }

        private void RestoreFromSession()
        {
            _equippedItems.Clear();
            foreach (var kvp in PlayerSessionData.EquippedItems)
            {
                if (kvp.Value != null && kvp.Value.ItemData != null)
                {
                    _equippedItems[kvp.Key] = kvp.Value;
                }
            }
        }

        private void NotifyEquipmentChanged()
        {
            PlayerSessionData.SaveEquipment(_equippedItems);
            OnEquipmentChanged?.Invoke();
        }

        private void Start()
        {
            // Removed direct UI finding, now relying on OnManagerCreated
        }

        public bool TryEquip(RuntimeItem item)
        {
            if (item == null || item.ItemData == null || item.ItemData.category != ItemCategory.Equipment)
                return false;

            if (!(item.ItemData is BagBack100.Data.Models.EquipmentDataSO eqData))
                return false;

            if (eqData.equipType == EquipType.Backpack && eqData is BagBack100.Data.Models.BackpackDataSO bagData)
            {
                return TryEquipBackpack(item, bagData);
            }

            EquipType type = eqData.equipType;
            int originalSlot = item.SlotIndex;

            // 장착할 아이템을 먼저 빼서 기존 장비가 돌아갈 칸을 확보
            _inventoryManager.RemoveItem(item);

            if (_equippedItems.TryGetValue(type, out RuntimeItem oldItem) && oldItem != null && !Unequip(type))
            {
                _inventoryManager.AddItemAt(item, originalSlot);
                return false;
            }

            _equippedItems[type] = item;
            NotifyEquipmentChanged();
            return true;
        }

        /// <summary>
        /// 가방 교체는 용량을 바꾸기 전에 칸 수를 먼저 검사한다.
        /// 새 가방이 가방에서 빠지고(-1) 기존 가방이 들어오므로(+1) 필요한 칸은 현재 아이템 수 그대로다.
        /// </summary>
        private bool TryEquipBackpack(RuntimeItem newBag, BagBack100.Data.Models.BackpackDataSO newBagData)
        {
            _equippedItems.TryGetValue(EquipType.Backpack, out RuntimeItem oldBag);
            bool hasOldBag = oldBag != null;
            int extraItems = hasOldBag ? 0 : -1;

            if (!_inventoryManager.CanFitAfterResize(newBagData.bagAddedSlots, extraItems))
            {
                OnEquipmentFailed?.Invoke("가방 공간이 부족해 가방을 교체할 수 없습니다.");
                return false;
            }

            _inventoryManager.RemoveItem(newBag);
            _inventoryManager.UpdateBagCapacity(newBagData.bagAddedSlots);
            if (hasOldBag)
            {
                _inventoryManager.TryAddNextAvailableSlot(oldBag);
            }

            _equippedItems[EquipType.Backpack] = newBag;
            NotifyEquipmentChanged();
            return true;
        }

        public bool Unequip(EquipType type)
        {
            if (!_equippedItems.TryGetValue(type, out RuntimeItem oldItem) || oldItem == null)
                return false;

            if (type == EquipType.Backpack)
            {
                // 보너스 칸이 사라진 기본 용량에 (기존 아이템 + 해제할 가방)이 모두 들어가야 한다.
                if (!_inventoryManager.CanFitAfterResize(0, 1))
                {
                    OnEquipmentFailed?.Invoke("가방에 빈 공간이 없습니다.");
                    return false;
                }
                _inventoryManager.UpdateBagCapacity(0);
            }

            if (!_inventoryManager.TryAddNextAvailableSlot(oldItem))
            {
                OnEquipmentFailed?.Invoke("가방에 빈 공간이 없습니다.");
                return false;
            }

            _equippedItems.Remove(type);
            NotifyEquipmentChanged();
            return true;
        }

        public void ClearEquipment()
        {
            _equippedItems.Clear();
            if (_inventoryManager != null)
            {
                _inventoryManager.UpdateBagCapacity(0);
            }
            NotifyEquipmentChanged();
        }

        public RuntimeItem GetEquippedItem(EquipType type)
        {
            return _equippedItems.TryGetValue(type, out var item) ? item : null;
        }

        public (int hp, int mp, int atk, int def, float speed, int range, float atkSpeedBonus, float weaponBaseAtkSpeed) GetTotalStats()
        {
            int hp = 0, mp = 0, atk = 0, def = 0, range = 0;
            float speed = 0f, atkSpeedBonus = 0f, weaponBaseAtkSpeed = 0f;
            foreach (var item in _equippedItems.Values)
            {
                if (item != null && item.ItemData is BagBack100.Data.Models.EquipmentDataSO eqData)
                {
                    hp += eqData.hpBonus;
                    mp += eqData.mpBonus;
                    atk += eqData.attackBonus;
                    def += eqData.defenseBonus;
                    speed += eqData.speedBonus;
                    range += eqData.rangeBonus;
                    
                    if (eqData is BagBack100.Data.Models.AccessoryDataSO accData)
                    {
                        atkSpeedBonus += accData.attackSpeedBonus;
                    }
                    
                    if (eqData is BagBack100.Data.Models.WeaponDataSO weaponData && weaponData.baseAttackSpeed > 0f)
                    {
                        weaponBaseAtkSpeed = weaponData.baseAttackSpeed;
                    }
                }
            }
            return (hp, mp, atk, def, speed, range, atkSpeedBonus, weaponBaseAtkSpeed);
        }
    }
}
