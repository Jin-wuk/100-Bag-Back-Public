using System;
using System.Collections.Generic;
using UnityEngine;
using DimensionLooter.Data.Core;

namespace DimensionLooter.Player
{
    [RequireComponent(typeof(InventoryManager))]
    public class EquipmentManager : MonoBehaviour
    {
        public event Action OnEquipmentChanged;
        public event Action<string> OnEquipmentFailed;

        private Dictionary<EquipType, RuntimeItem> _equippedItems = new Dictionary<EquipType, RuntimeItem>();
        private InventoryManager _inventoryManager;

        private void Awake()
        {
            _inventoryManager = GetComponent<InventoryManager>();
        }

        private void Start()
        {
            // 플레이어 스폰 시 씬에 존재하는 UI 컨트롤러를 찾아 자신을 바인딩 (DI)
            var ui = FindObjectOfType<DimensionLooter.UI.EquipmentUIController>(true);
            if (ui != null)
            {
                ui.Bind(this);
            }
        }

        public bool TryEquip(RuntimeItem item)
        {
            if (item.ItemData.category != ItemCategory.Equipment)
                return false;

            EquipType type = item.ItemData.equipType;

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
            _inventoryManager.RemoveItem(item);
            
            OnEquipmentChanged?.Invoke();
            return true;
        }

        public void Unequip(EquipType type)
        {
            if (_equippedItems.TryGetValue(type, out RuntimeItem oldItem) && oldItem != null)
            {
                if (type == EquipType.Backpack)
                {
                    _inventoryManager.UpdateBagSize(4);
                }
                _equippedItems.Remove(type);
                OnEquipmentChanged?.Invoke();
            }
        }

        public RuntimeItem GetEquippedItem(EquipType type)
        {
            return _equippedItems.TryGetValue(type, out var item) ? item : null;
        }
    }
}
