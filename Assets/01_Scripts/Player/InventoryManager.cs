using System;
using System.Collections.Generic;
using UnityEngine;
using BagBack100.Data.Core;

namespace BagBack100.Player
{
    /// <summary>
    /// Role: 플레이어 가방(메인 인벤토리) 슬롯 관리. 용량 = 기본 칸 + 장착 가방 보너스. 변경 시 PlayerSessionData에 자동 동기화.
    /// Dependencies: SlotContainer, PlayerSessionData
    /// Events: OnManagerCreated(static), OnInventoryChanged / OnContainerChanged,
    ///         GlobalEvents.OnItemDroppedOnGround (세션 복원 시 용량 초과분 폴백 드랍)
    /// </summary>
    public class InventoryManager : MonoBehaviour, ISlotItemContainer
    {
        public static event Action<InventoryManager> OnManagerCreated;
        public event Action OnInventoryChanged;

        event Action ISlotItemContainer.OnContainerChanged
        {
            add => OnInventoryChanged += value;
            remove => OnInventoryChanged -= value;
        }

        [Header("Base Settings")]
        [SerializeField] private int _baseMaxSlots = 30;

        public int MaxSlots => _container != null ? _container.Capacity : _baseMaxSlots;
        public int BaseMaxSlots => _baseMaxSlots;
        public int ItemCount => _container != null ? _container.ItemCount : 0;

        private SlotContainer _container;
        private readonly List<RuntimeItem> _overflowBuffer = new List<RuntimeItem>();

        private void Awake()
        {
            RestoreFromSession();
            OnManagerCreated?.Invoke(this);
        }

        private void RestoreFromSession()
        {
            PlayerSessionData.GetEquippedBackpackBonus(out int addedSlots);
            _container = new SlotContainer(_baseMaxSlots + addedSlots);

            _overflowBuffer.Clear();
            _container.Load(PlayerSessionData.InventoryItems, _overflowBuffer);
            DropOverflowOnGround();
            NotifyInventoryChanged();
        }

        private void NotifyInventoryChanged()
        {
            PlayerSessionData.SaveInventory(_container.Items);
            OnInventoryChanged?.Invoke();
        }

        private void OnEnable()
        {
            BagBack100.Core.GlobalEvents.OnItemPickedUpByPlayer += HandleItemPickedUp;
        }

        private void OnDisable()
        {
            BagBack100.Core.GlobalEvents.OnItemPickedUpByPlayer -= HandleItemPickedUp;
        }

        private void HandleItemPickedUp(RuntimeItem item, Action<bool> onResult)
        {
            bool success = TryAddNextAvailableSlot(item);
            if (success)
            {
                Debug.Log($"[Inventory] {item.ItemData.itemName} 획득 성공!");
            }
            else
            {
                Debug.LogWarning($"[Inventory] 가방이 꽉 차서 {item.ItemData.itemName}을 획득할 수 없습니다.");
            }
            onResult?.Invoke(success);
        }

        /// <summary>
        /// 가방 보너스 칸을 바꾼다. 호출자(EquipmentManager)가 CanFitAfterResize로 미리 검사하므로 정상 흐름에서는 넘치는 아이템이 없다.
        /// </summary>
        public void UpdateBagCapacity(int addedSlots)
        {
            _overflowBuffer.Clear();
            _container.Resize(_baseMaxSlots + addedSlots, _overflowBuffer);
            DropOverflowOnGround();
            NotifyInventoryChanged();
        }

        /// <summary>가방 보너스를 addedSlots로 바꾼 뒤 (현재 아이템 수 + extraItems)가 들어가는지.</summary>
        public bool CanFitAfterResize(int addedSlots, int extraItems)
        {
            return ItemCount + extraItems <= _baseMaxSlots + addedSlots;
        }

        // 세션 복원 등 예외 경로에서만 발생. 아이템을 조용히 잃지 않도록 플레이어 위치에 떨군다.
        private void DropOverflowOnGround()
        {
            if (_overflowBuffer.Count == 0) return;

            Debug.LogWarning($"[Inventory] 가방 용량 초과로 {_overflowBuffer.Count}개 아이템을 바닥에 떨굽니다.");
            for (int i = 0; i < _overflowBuffer.Count; i++)
            {
                BagBack100.Core.GlobalEvents.OnItemDroppedOnGround?.Invoke(_overflowBuffer[i], transform.position);
            }
            _overflowBuffer.Clear();
        }

        public void ClearInventory()
        {
            _container = new SlotContainer(_baseMaxSlots);
            NotifyInventoryChanged();
        }

        public bool CanAddItem(RuntimeItem item) => _container.CanAdd(item);

        public bool CanPlaceItemAtSlot(RuntimeItem item, int targetSlot) => _container.CanPlaceAt(item, targetSlot);

        public bool AddItemAt(RuntimeItem item, int targetSlot)
        {
            if (!_container.AddAt(item, targetSlot)) return false;
            NotifyInventoryChanged();
            return true;
        }

        public void RemoveItem(RuntimeItem item)
        {
            if (_container.Remove(item)) NotifyInventoryChanged();
        }

        public IReadOnlyList<RuntimeItem> GetAllItems() => _container.Items;

        public RuntimeItem GetItemAtSlot(int slotIndex) => _container?.GetAt(slotIndex);

        public bool TryAddNextAvailableSlot(RuntimeItem item)
        {
            if (!_container.TryAddAuto(item)) return false;
            NotifyInventoryChanged();
            return true;
        }
    }
}
