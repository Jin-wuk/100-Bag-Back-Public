using System;
using System.Collections.Generic;
using UnityEngine;
using BagBack100.Data.Core;

namespace BagBack100.Hub
{
    /// <summary>
    /// Role: 허브 개인 창고 슬롯 관리. 변경 시 PlayerSessionData.StorageItems에 자동 동기화.
    /// Dependencies: SlotContainer, PlayerSessionData
    /// Events: OnManagerCreated(static), OnStorageChanged / OnContainerChanged
    /// </summary>
    public class StorageManager : MonoBehaviour, ISlotItemContainer
    {
        public static event Action<StorageManager> OnManagerCreated;
        public event Action OnStorageChanged;

        event Action ISlotItemContainer.OnContainerChanged
        {
            add => OnStorageChanged += value;
            remove => OnStorageChanged -= value;
        }

        [Header("Base Settings")]
        [SerializeField] private int _baseMaxSlots = 30;

        public int MaxSlots => _container != null ? _container.Capacity : _baseMaxSlots;

        private SlotContainer _container;
        // 창고 용량보다 많이 저장된 아이템. 바닥이 없는 허브라 버리지 않고 세션에 계속 보존한다.
        private readonly List<RuntimeItem> _hiddenOverflow = new List<RuntimeItem>();
        private readonly List<RuntimeItem> _saveBuffer = new List<RuntimeItem>();

        private void Awake()
        {
            RestoreFromSession();
            OnManagerCreated?.Invoke(this);
        }

        private void RestoreFromSession()
        {
            _container = new SlotContainer(_baseMaxSlots);

            _hiddenOverflow.Clear();
            _container.Load(PlayerSessionData.StorageItems, _hiddenOverflow);
            if (_hiddenOverflow.Count > 0)
            {
                Debug.LogError($"[StorageManager] 창고 용량({_baseMaxSlots}) 초과 아이템 {_hiddenOverflow.Count}개가 표시되지 않습니다. _baseMaxSlots를 늘려주세요.");
            }
            NotifyStorageChanged();
        }

        private void NotifyStorageChanged()
        {
            _saveBuffer.Clear();
            _saveBuffer.AddRange(_container.Items);
            _saveBuffer.AddRange(_hiddenOverflow);
            PlayerSessionData.SaveStorage(_saveBuffer);
            OnStorageChanged?.Invoke();
        }

        public bool CanAddItem(RuntimeItem item) => _container.CanAdd(item);

        public bool CanPlaceItemAtSlot(RuntimeItem item, int targetSlot) => _container.CanPlaceAt(item, targetSlot);

        public bool AddItemAt(RuntimeItem item, int targetSlot)
        {
            if (!_container.AddAt(item, targetSlot)) return false;
            NotifyStorageChanged();
            return true;
        }

        public void RemoveItem(RuntimeItem item)
        {
            if (_container.Remove(item)) NotifyStorageChanged();
        }

        public IReadOnlyList<RuntimeItem> GetAllItems() => _container.Items;

        public RuntimeItem GetItemAtSlot(int slotIndex) => _container?.GetAt(slotIndex);

        public bool TryAddNextAvailableSlot(RuntimeItem item)
        {
            if (!_container.TryAddAuto(item)) return false;
            NotifyStorageChanged();
            return true;
        }
    }
}
