using System;
using System.Collections.Generic;
using UnityEngine;
using BagBack100.Data.Core;

namespace BagBack100.Player
{
    /// <summary>
    /// Role: 보안 파우치 슬롯 관리. 사망해도 잃지 않는 별도 칸(기본 2칸, 이후 강화소에서 확장).
    ///       장비 아이템은 넣을 수 없다. 변경 시 PlayerSessionData.PouchItems에 자동 동기화.
    /// Dependencies: SlotContainer, PlayerSessionData
    /// Events: OnManagerCreated(static), OnPouchChanged / OnContainerChanged
    /// </summary>
    public class SecurePouchManager : MonoBehaviour, ISlotItemContainer
    {
        public static event Action<SecurePouchManager> OnManagerCreated;
        public event Action OnPouchChanged;

        event Action ISlotItemContainer.OnContainerChanged
        {
            add => OnPouchChanged += value;
            remove => OnPouchChanged -= value;
        }

        [Tooltip("보안 파우치 기본 칸 수 (기획 상한 4~6칸, 확장은 강화소에서)")]
        [Min(1)] [SerializeField] private int _baseSlots = 2;

        public int MaxSlots => _container != null ? _container.Capacity : _baseSlots;

        private SlotContainer _container;

        private void Awake()
        {
            _container = new SlotContainer(_baseSlots);

            var overflow = new List<RuntimeItem>();
            _container.Load(PlayerSessionData.PouchItems, overflow);
            if (overflow.Count > 0)
            {
                // 파우치는 사망해도 지켜야 하는 칸이므로 넘친 아이템을 버리지 않고 세션에 남겨 둔다
                Debug.LogError($"[SecurePouch] 파우치 용량({_baseSlots}) 초과 아이템 {overflow.Count}개가 표시되지 않습니다.");
                for (int i = 0; i < overflow.Count; i++) _hiddenOverflow.Add(overflow[i]);
            }
            NotifyPouchChanged();
            OnManagerCreated?.Invoke(this);
        }

        private readonly List<RuntimeItem> _hiddenOverflow = new List<RuntimeItem>();
        private readonly List<RuntimeItem> _saveBuffer = new List<RuntimeItem>();

        private void NotifyPouchChanged()
        {
            _saveBuffer.Clear();
            _saveBuffer.AddRange(_container.Items);
            _saveBuffer.AddRange(_hiddenOverflow);
            PlayerSessionData.SavePouch(_saveBuffer);
            OnPouchChanged?.Invoke();
        }

        // 장비는 허브에서만 다루는 제작 전용 자산이라 파우치로 사망 보험을 걸 수 없게 한다 (기획 확정)
        // 카테고리 값이 에셋에서 잘못 설정돼도 장비 타입이면 막도록 타입도 함께 검사
        public static bool IsAllowed(RuntimeItem item) =>
            item != null && item.ItemData != null &&
            item.ItemData.category != ItemCategory.Equipment &&
            !(item.ItemData is BagBack100.Data.Models.EquipmentDataSO);

        public bool CanAddItem(RuntimeItem item) => IsAllowed(item) && _container.CanAdd(item);

        public bool CanPlaceItemAtSlot(RuntimeItem item, int targetSlot) => IsAllowed(item) && _container.CanPlaceAt(item, targetSlot);

        public bool AddItemAt(RuntimeItem item, int targetSlot)
        {
            if (!IsAllowed(item) || !_container.AddAt(item, targetSlot)) return false;
            NotifyPouchChanged();
            return true;
        }

        public bool TryAddNextAvailableSlot(RuntimeItem item)
        {
            if (!IsAllowed(item) || !_container.TryAddAuto(item)) return false;
            NotifyPouchChanged();
            return true;
        }

        public void RemoveItem(RuntimeItem item)
        {
            if (_container.Remove(item)) NotifyPouchChanged();
        }

        public bool RemoveAmount(RuntimeItem item, int amount)
        {
            if (item == null || !_container.Contains(item) || amount <= 0) return false;
            if (amount >= item.Count) _container.Remove(item);
            else item.Count -= amount;
            NotifyPouchChanged();
            return true;
        }

        public IReadOnlyList<RuntimeItem> GetAllItems() => _container.Items;

        public RuntimeItem GetItemAtSlot(int slotIndex) => _container?.GetAt(slotIndex);
    }
}
