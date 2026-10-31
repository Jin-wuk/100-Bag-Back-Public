using System.Collections.Generic;
using UnityEngine;

namespace BagBack100.Data.Core
{
    /// <summary>
    /// Role: 1슬롯 1아이템 규칙의 슬롯 배열 + 스택/스왑 로직을 담는 순수 C# 컨테이너.
    ///       인벤토리·창고·보안 파우치가 같은 규칙을 복제하지 않도록 한 곳에 모은다 (리팩토링 트리거 4).
    /// Dependencies: RuntimeItem
    /// Events: 없음. 변경 알림과 세션 저장은 소유한 매니저가 성공한 변경 뒤에 직접 수행한다.
    /// </summary>
    public class SlotContainer
    {
        private RuntimeItem[] _slots;
        private readonly List<RuntimeItem> _items = new List<RuntimeItem>();

        public int Capacity => _slots.Length;
        public int ItemCount => _items.Count;
        public IReadOnlyList<RuntimeItem> Items => _items;

        public SlotContainer(int capacity)
        {
            _slots = new RuntimeItem[Mathf.Max(0, capacity)];
        }

        public bool Contains(RuntimeItem item) => item != null && _items.Contains(item);

        public RuntimeItem GetAt(int slot)
        {
            if (slot < 0 || slot >= _slots.Length) return null;
            return _slots[slot];
        }

        /// <summary>
        /// 아이템이 기존 슬롯 번호를 가지고 있으면 그 자리를 우선 유지하고, 아니면 앞쪽 빈칸에 넣는다.
        /// 들어가지 못한 아이템은 overflow로 돌려준다 (세션 복원·용량 변경용).
        /// </summary>
        public void Load(IEnumerable<RuntimeItem> items, List<RuntimeItem> overflow)
        {
            Clear();
            if (items == null) return;

            List<RuntimeItem> unplaced = new List<RuntimeItem>();
            foreach (var item in items)
            {
                if (item == null || item.ItemData == null) continue;

                if (item.SlotIndex >= 0 && item.SlotIndex < _slots.Length && _slots[item.SlotIndex] == null)
                {
                    _slots[item.SlotIndex] = item;
                    _items.Add(item);
                }
                else
                {
                    unplaced.Add(item);
                }
            }

            PlaceInFrontOrOverflow(unplaced, overflow);
        }

        /// <summary>
        /// 용량을 바꾼다. 새 범위 밖 슬롯의 아이템은 앞쪽 빈칸으로 옮기고, 그래도 남으면 컨테이너에서 빼서 overflow로 돌려준다.
        /// </summary>
        public void Resize(int newCapacity, List<RuntimeItem> overflow)
        {
            newCapacity = Mathf.Max(0, newCapacity);
            var oldSlots = _slots;
            _slots = new RuntimeItem[newCapacity];

            List<RuntimeItem> unplaced = new List<RuntimeItem>();
            for (int i = 0; i < oldSlots.Length; i++)
            {
                var item = oldSlots[i];
                if (item == null) continue;

                if (i < newCapacity)
                {
                    _slots[i] = item;
                }
                else
                {
                    _items.Remove(item);
                    unplaced.Add(item);
                }
            }

            PlaceInFrontOrOverflow(unplaced, overflow);
        }

        private void PlaceInFrontOrOverflow(List<RuntimeItem> unplaced, List<RuntimeItem> overflow)
        {
            for (int i = 0; i < unplaced.Count; i++)
            {
                var item = unplaced[i];
                int empty = FindFirstEmptySlot();
                if (empty >= 0)
                {
                    _slots[empty] = item;
                    item.SlotIndex = empty;
                    _items.Add(item);
                }
                else
                {
                    item.SlotIndex = -1;
                    overflow?.Add(item);
                }
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _items.Count; i++) _items[i].SlotIndex = -1;
            _items.Clear();
            System.Array.Clear(_slots, 0, _slots.Length);
        }

        private int FindFirstEmptySlot()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null) return i;
            }
            return -1;
        }

        public bool CanAdd(RuntimeItem item)
        {
            if (item == null || item.ItemData == null) return false;
            if (FindFirstEmptySlot() >= 0) return true;

            // 빈칸이 없어도 같은 아이템 스택들의 남은 여유 합계로 전량 수용 가능하면 허용
            if (item.ItemData.maxStack <= 1) return false;

            int availableSpace = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                var existing = _slots[i];
                if (existing != null && existing != item && existing.ItemData.itemID == item.ItemData.itemID)
                {
                    availableSpace += Mathf.Max(0, existing.ItemData.maxStack - existing.Count);
                    if (availableSpace >= item.Count) return true;
                }
            }
            return false;
        }

        /// <summary>내부 아이템은 어떤 슬롯이든 스왑 가능. 외부 아이템은 빈칸이거나 같은 아이템 스택에 전량 합쳐질 때만 가능.</summary>
        public bool CanPlaceAt(RuntimeItem item, int slot)
        {
            if (item == null || item.ItemData == null || slot < 0 || slot >= _slots.Length) return false;
            if (_items.Contains(item)) return true;

            var target = _slots[slot];
            if (target == null) return true;

            return target.ItemData.itemID == item.ItemData.itemID &&
                   item.ItemData.maxStack > 1 &&
                   target.Count + item.Count <= target.ItemData.maxStack;
        }

        public bool AddAt(RuntimeItem item, int slot)
        {
            if (item == null || item.ItemData == null || slot < 0 || slot >= _slots.Length) return false;

            bool isInternal = _items.Contains(item);
            var target = _slots[slot];

            if (target != null && target != item)
            {
                if (target.ItemData.itemID == item.ItemData.itemID && item.ItemData.maxStack > 1)
                {
                    int space = target.ItemData.maxStack - target.Count;
                    if (space >= item.Count)
                    {
                        target.Count += item.Count;
                        if (isInternal) Remove(item);
                        return true;
                    }
                }

                if (!isInternal || item.SlotIndex < 0 || item.SlotIndex >= _slots.Length) return false;

                int oldSlot = item.SlotIndex;
                _slots[oldSlot] = target;
                target.SlotIndex = oldSlot;
                _slots[slot] = item;
                item.SlotIndex = slot;
                return true;
            }

            if (isInternal && item.SlotIndex >= 0 && item.SlotIndex < _slots.Length)
            {
                _slots[item.SlotIndex] = null;
            }

            _slots[slot] = item;
            item.SlotIndex = slot;
            if (!isInternal) _items.Add(item);
            return true;
        }

        /// <summary>스택 가능한 아이템은 기존 스택에 먼저 합치고, 남은 수량은 앞쪽 빈칸에 넣는다.</summary>
        public bool TryAddAuto(RuntimeItem item)
        {
            // 외부에서 들어오는 아이템 전용. 내부 아이템을 넣으면 부분 병합 뒤 슬롯이 바뀌는 부작용이 있다.
            if (Contains(item) || !CanAdd(item)) return false;

            if (item.ItemData.maxStack > 1)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    var existing = _slots[i];
                    if (existing == null || existing == item ||
                        existing.ItemData.itemID != item.ItemData.itemID ||
                        existing.Count >= existing.ItemData.maxStack)
                    {
                        continue;
                    }

                    int addCount = Mathf.Min(existing.ItemData.maxStack - existing.Count, item.Count);
                    existing.Count += addCount;
                    item.Count -= addCount;

                    if (item.Count <= 0)
                    {
                        if (_items.Contains(item)) Remove(item);
                        return true;
                    }
                }
            }

            int empty = FindFirstEmptySlot();
            return empty >= 0 && AddAt(item, empty);
        }

        public bool Remove(RuntimeItem item)
        {
            if (item == null || !_items.Contains(item)) return false;

            if (item.SlotIndex >= 0 && item.SlotIndex < _slots.Length && _slots[item.SlotIndex] == item)
            {
                _slots[item.SlotIndex] = null;
            }
            _items.Remove(item);
            item.SlotIndex = -1;
            return true;
        }
    }
}
