using System;
using UnityEngine;
using BagBack100.Data.Core;
using BagBack100.Data.Models;

namespace BagBack100.Player
{
    /// <summary>
    /// Role: 소모품 퀵슬롯(1·2·3) 등록과 사용. 슬롯은 아이템 개체가 아니라 itemID에 연결되어
    ///       스택이 바뀌거나 다 써도 등록이 유지되고, 사용 시 가방에서 같은 종류의 가장 앞 칸을 소모한다.
    ///       등록 정보는 PlayerSessionData에 보존되어 허브에서 등록한 슬롯이 던전에서도 유지된다.
    /// Dependencies: InventoryManager, ConsumableController(선택, 던전 전용), PlayerInput(선택), PlayerSessionData
    /// Events: OnManagerCreated(static), OnQuickSlotsChanged
    /// </summary>
    [RequireComponent(typeof(InventoryManager))]
    public class QuickSlotManager : MonoBehaviour
    {
        public static event Action<QuickSlotManager> OnManagerCreated;
        public event Action OnQuickSlotsChanged;

        public int SlotCount => PlayerSessionData.QuickSlotCount;

        private InventoryManager _inventory;
        private ConsumableController _consumables;
        private PlayerInput _input;

        public InventoryManager Inventory => _inventory;
        public ConsumableController Consumables => _consumables;

        private void Awake()
        {
            _inventory = GetComponent<InventoryManager>();
            TryGetComponent(out _consumables);
            TryGetComponent(out _input);
            OnManagerCreated?.Invoke(this);
        }

        private void OnEnable()
        {
            if (_input != null) _input.OnQuickSlotInput += Use;
        }

        private void OnDisable()
        {
            if (_input != null) _input.OnQuickSlotInput -= Use;
        }

        public int GetItemId(int index)
        {
            return IsValidIndex(index) ? PlayerSessionData.QuickSlotItemIds[index] : 0;
        }

        public bool CanAssign(RuntimeItem item) => item != null && item.ItemData is ConsumableDataSO;

        /// <summary>같은 소모품이 다른 칸에 있으면 그 칸은 비워 한 종류가 한 칸만 차지하게 한다.</summary>
        public bool Assign(int index, RuntimeItem item)
        {
            if (!IsValidIndex(index) || !CanAssign(item)) return false;

            int itemID = item.ItemData.itemID;
            var ids = PlayerSessionData.QuickSlotItemIds;
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == itemID) ids[i] = 0;
            }
            ids[index] = itemID;
            OnQuickSlotsChanged?.Invoke();
            return true;
        }

        public void ClearSlot(int index)
        {
            if (!IsValidIndex(index)) return;
            PlayerSessionData.QuickSlotItemIds[index] = 0;
            OnQuickSlotsChanged?.Invoke();
        }

        public void Use(int index)
        {
            if (_consumables == null) return; // 허브: 등록만 가능

            int itemID = GetItemId(index);
            if (itemID == 0) return;

            var item = _inventory.FindFirstItem(itemID);
            if (item != null) _consumables.TryUse(item);
        }

        private bool IsValidIndex(int index) => index >= 0 && index < PlayerSessionData.QuickSlotCount;
    }
}
