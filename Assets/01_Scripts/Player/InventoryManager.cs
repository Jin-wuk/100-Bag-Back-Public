using System;
using System.Collections.Generic;
using UnityEngine;
using BagBack100.Data.Core;

namespace BagBack100.Player
{
    public class InventoryManager : MonoBehaviour
    {
        public static event Action<InventoryManager> OnManagerCreated;
        public event Action OnInventoryChanged;
        
        [Header("Base Settings")]
        [SerializeField] private int _baseMaxSlots = 30;
        [SerializeField] private float _baseMaxWeight = 50f;

        public int MaxSlots { get; private set; }
        public float MaxWeight { get; private set; }
        public float CurrentWeight { get; private set; }

        private RuntimeItem[] _slots;
        private List<RuntimeItem> _items = new List<RuntimeItem>();

        private void Awake()
        {
            RestoreFromSession();
            OnManagerCreated?.Invoke(this);
        }

        private void RestoreFromSession()
        {
            _items.Clear();
            foreach (var savedItem in PlayerSessionData.InventoryItems)
            {
                if (savedItem != null && savedItem.ItemData != null)
                {
                    _items.Add(savedItem);
                }
            }

            PlayerSessionData.GetEquippedBackpackBonus(out int addedSlots, out float addedWeight);
            InitializeBag(_baseMaxSlots + addedSlots, _baseMaxWeight + addedWeight);
        }

        private void NotifyInventoryChanged()
        {
            PlayerSessionData.SaveInventory(_items);
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
                Debug.LogWarning($"[Inventory] 가방이 꽉 찼거나 무거워서 {item.ItemData.itemName}을 획득할 수 없습니다.");
            }
            onResult?.Invoke(success);
        }

        public void InitializeBag(int maxSlots, float maxWeight)
        {
            MaxSlots = maxSlots;
            MaxWeight = maxWeight;
            
            var newSlots = new RuntimeItem[maxSlots];
            List<RuntimeItem> outOfBoundsItems = new List<RuntimeItem>();
            
            // 기존 아이템 마이그레이션
            for(int i=0; i<_items.Count; i++)
            {
                var item = _items[i];
                if (item.SlotIndex >= 0 && item.SlotIndex < maxSlots && newSlots[item.SlotIndex] == null)
                {
                    newSlots[item.SlotIndex] = item;
                }
                else
                {
                    outOfBoundsItems.Add(item);
                }
            }
            _slots = newSlots;

            // 초과된 아이템들을 앞쪽 빈 슬롯부터 채워넣음
            foreach(var item in outOfBoundsItems)
            {
                bool placed = false;
                for (int j = 0; j < maxSlots; j++)
                {
                    if (_slots[j] == null)
                    {
                        _slots[j] = item;
                        item.SlotIndex = j;
                        placed = true;
                        break;
                    }
                }
                
                // 새로운 공간에도 빈칸이 없다면 바닥에 드랍
                if (!placed)
                {
                    InternalRemoveItem(item);
                    var player = FindObjectOfType<PlayerController>();
                    Vector3 dropPos = player != null ? player.transform.position : Vector3.zero;
                    BagBack100.Core.GlobalEvents.OnItemDroppedOnGround?.Invoke(item, dropPos);
                }
            }
            RecalculateWeight();
            NotifyInventoryChanged();
        }

        public void UpdateBagCapacity(int addedSlots, float addedWeight)
        {
            InitializeBag(_baseMaxSlots + addedSlots, _baseMaxWeight + addedWeight);
        }

        public void ClearInventory()
        {
            _items.Clear();
            MaxSlots = _baseMaxSlots;
            MaxWeight = _baseMaxWeight;
            _slots = new RuntimeItem[MaxSlots];
            CurrentWeight = 0f;
            NotifyInventoryChanged();
        }

        private void RecalculateWeight()
        {
            CurrentWeight = 0f;
            foreach (var item in _items)
            {
                if (item != null && item.ItemData != null)
                {
                    CurrentWeight += item.ItemData.weight * item.Count;
                }
            }
        }

        // 아이템을 인벤토리에 넣을 수 있는지 여부 (특정 슬롯 지정 혹은 아무 슬롯)
        public bool CanAddItem(RuntimeItem item)
        {
            if (item == null || item.ItemData == null) return false;

            // 무게 체크
            if (CurrentWeight + (item.ItemData.weight * item.Count) > MaxWeight)
                return false;

            // 빈 슬롯이 하나라도 있으면 수용 가능
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i] == null) return true;
            }

            // 빈 슬롯이 없더라도 스택 가능한 여유 공간 합계가 충분하면 수용 가능
            if (item.ItemData.maxStack > 1)
            {
                int availableSpace = 0;
                for (int i = 0; i < MaxSlots; i++)
                {
                    var existing = _slots[i];
                    if (existing != null && existing != item && existing.ItemData.itemID == item.ItemData.itemID)
                    {
                        availableSpace += Mathf.Max(0, existing.ItemData.maxStack - existing.Count);
                        if (availableSpace >= item.Count) return true;
                    }
                }
            }

            return false;
        }

        // 특정 슬롯 인덱스에 아이템이 배치 가능한지 (스왑 포함)
        public bool CanPlaceItemAtSlot(RuntimeItem item, int targetSlot)
        {
            if (item == null || item.ItemData == null || targetSlot < 0 || targetSlot >= MaxSlots)
                return false;
            
            bool isInternal = _items.Contains(item);

            // 인벤토리 외부(창고 등)에서 들어오는 아이템이라면 무게 및 대상 슬롯 상태 체크
            if (!isInternal)
            {
                if (CurrentWeight + (item.ItemData.weight * item.Count) > MaxWeight) return false;

                if (_slots[targetSlot] != null)
                {
                    var targetItem = _slots[targetSlot];
                    if (targetItem.ItemData.itemID != item.ItemData.itemID ||
                        item.ItemData.maxStack <= 1 ||
                        targetItem.Count + item.Count > targetItem.ItemData.maxStack)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        // 외부에서 획득하거나 슬롯이동 시
        public bool AddItemAt(RuntimeItem item, int targetSlot)
        {
            if (item == null || targetSlot < 0 || targetSlot >= MaxSlots) return false;

            bool isInternal = _items.Contains(item);

            // 대상 슬롯에 무언가 있고, 같은 아이템이며 스택 가능하다면 합침
            if (_slots[targetSlot] != null && _slots[targetSlot] != item)
            {
                var targetItem = _slots[targetSlot];
                if (targetItem.ItemData.itemID == item.ItemData.itemID && item.ItemData.maxStack > 1)
                {
                    int space = targetItem.ItemData.maxStack - targetItem.Count;
                    if (space >= item.Count)
                    {
                        targetItem.Count += item.Count;
                        if (isInternal) InternalRemoveItem(item); // 기존 슬롯에서 제거
                        RecalculateWeight();
                        NotifyInventoryChanged();
                        return true;
                    }
                }
                
                // 단순 스왑 로직
                if (isInternal && item.SlotIndex >= 0 && item.SlotIndex < MaxSlots)
                {
                    // 내부 이동인 경우 스왑
                    int oldSlot = item.SlotIndex;
                    _slots[oldSlot] = targetItem;
                    targetItem.SlotIndex = oldSlot;
                    
                    _slots[targetSlot] = item;
                    item.SlotIndex = targetSlot;
                    
                    NotifyInventoryChanged();
                    return true;
                }
                else
                {
                    // 외부에서 온 건데 대상 슬롯이 차있고 스택도 안되면 실패
                    return false;
                }
            }

            // 단순 이동/추가
            if (isInternal && item.SlotIndex >= 0 && item.SlotIndex < MaxSlots)
            {
                _slots[item.SlotIndex] = null;
            }
            
            _slots[targetSlot] = item;
            item.SlotIndex = targetSlot;
            
            if (!isInternal) _items.Add(item);

            RecalculateWeight();
            NotifyInventoryChanged();
            return true;
        }

        private void InternalRemoveItem(RuntimeItem item)
        {
            if (item == null || !_items.Contains(item)) return;

            if (item.SlotIndex >= 0 && item.SlotIndex < MaxSlots && _slots[item.SlotIndex] == item)
            {
                _slots[item.SlotIndex] = null;
            }
            _items.Remove(item);
            item.SlotIndex = -1;
        }

        public void RemoveItem(RuntimeItem item)
        {
            InternalRemoveItem(item);
            RecalculateWeight();
            NotifyInventoryChanged();
        }

        public IReadOnlyList<RuntimeItem> GetAllItems() => _items;

        public RuntimeItem GetItemAtSlot(int slotIndex)
        {
            if (_slots == null || slotIndex < 0 || slotIndex >= MaxSlots) return null;
            return _slots[slotIndex];
        }

        public bool TryAddNextAvailableSlot(RuntimeItem item)
        {
            if (!CanAddItem(item)) return false;

            bool isInternal = _items.Contains(item);

            // 1. 중첩 가능한 아이템이면 상단 왼쪽(0번 슬롯)부터 기존 스택에 우선 합산
            if (item.ItemData.maxStack > 1)
            {
                for (int i = 0; i < MaxSlots; i++)
                {
                    var existing = _slots[i];
                    if (existing != null && existing != item && existing.ItemData.itemID == item.ItemData.itemID && existing.Count < existing.ItemData.maxStack)
                    {
                        int space = existing.ItemData.maxStack - existing.Count;
                        int addCount = Mathf.Min(space, item.Count);
                        existing.Count += addCount;
                        item.Count -= addCount;

                        if (item.Count <= 0)
                        {
                            if (isInternal) InternalRemoveItem(item);
                            RecalculateWeight();
                            NotifyInventoryChanged();
                            return true;
                        }
                    }
                }
            }

            // 2. 남은 수량(또는 중첩 불가 아이템)은 가장 상단 왼쪽(0번 슬롯) 빈칸부터 채움
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i] == null)
                {
                    AddItemAt(item, i);
                    return true;
                }
            }
            return false;
        }
    }
}
