using System.Collections.Generic;
using UnityEngine;
using BagBack100.Data.Models;

namespace BagBack100.Data.Core
{
    /// <summary>
    /// Role: 1슬롯 1아이템 규칙의 슬롯 배열 + 스택/스왑 로직을 담는 순수 C# 컨테이너.
    ///       인벤토리·창고·보안 파우치가 같은 규칙을 복제하지 않도록 한 곳에 모은다 (리팩토링 트리거 4).
    ///       겹침 한도는 아이템의 maxStack이 기본이고, 창고처럼 최소 겹침 한도를 받은 컨테이너만 더 크게 겹친다.
    /// Dependencies: RuntimeItem
    /// Events: 없음. 변경 알림과 세션 저장은 소유한 매니저가 성공한 변경 뒤에 직접 수행한다.
    /// </summary>
    public class SlotContainer
    {
        private RuntimeItem[] _slots;
        private readonly List<RuntimeItem> _items = new List<RuntimeItem>();
        private readonly int _minStackLimit;

        public int Capacity => _slots.Length;
        public int ItemCount => _items.Count;
        public IReadOnlyList<RuntimeItem> Items => _items;

        /// <param name="minStackLimit">0이면 아이템의 maxStack을 그대로 쓴다. 양수면 겹치는 아이템과 재료를 이 수까지 겹친다 (창고).</param>
        public SlotContainer(int capacity, int minStackLimit = 0)
        {
            _slots = new RuntimeItem[Mathf.Max(0, capacity)];
            _minStackLimit = Mathf.Max(0, minStackLimit);
        }

        // 재료는 가방 압박을 위해 Rare·Legendary가 maxStack 1이지만, 창고에서는 모아 두는 자원이라 등급과 무관하게 겹친다 (기획 07 6절, 임시 규칙)
        public int StackLimitOf(ItemDataSO data)
        {
            if (data == null) return 1;
            int baseLimit = Mathf.Max(1, data.maxStack);
            bool stackable = data.maxStack > 1 || data.category == ItemCategory.Material;
            return _minStackLimit > 0 && stackable ? Mathf.Max(baseLimit, _minStackLimit) : baseLimit;
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

        private bool IsSameStack(RuntimeItem a, RuntimeItem b) =>
            a != null && b != null && a != b && a.ItemData.itemID == b.ItemData.itemID;

        /// <summary>
        /// 외부 아이템을 몇 개까지 받을 수 있는지. slot &lt; 0이면 자동 배치(같은 묶음 여유 + 빈칸 × 한도), 아니면 그 칸 기준.
        /// 내부 아이템은 같은 컨테이너 안 이동이라 전량으로 본다. 결과는 [0, item.Count].
        /// </summary>
        public int GetAcceptableAmount(RuntimeItem item, int slot)
        {
            if (item == null || item.ItemData == null || item.Count <= 0) return 0;
            if (_items.Contains(item)) return item.Count;

            int limit = StackLimitOf(item.ItemData);
            long total = 0;

            if (slot >= 0)
            {
                if (slot >= _slots.Length) return 0;
                var target = _slots[slot];
                if (target == null) total = limit;
                else if (IsSameStack(target, item) && limit > 1) total = Mathf.Max(0, limit - target.Count);
            }
            else
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    var existing = _slots[i];
                    if (existing == null) total += limit;
                    else if (IsSameStack(existing, item) && limit > 1) total += Mathf.Max(0, limit - existing.Count);
                    if (total >= item.Count) break;
                }
            }
            return (int)System.Math.Min(total, item.Count);
        }

        /// <summary>외부 아이템 전량이 자동 배치로 들어가는지 (한도를 넘는 묶음은 여러 칸으로 나눠 넣는 것까지 고려).</summary>
        public bool CanAdd(RuntimeItem item)
        {
            if (item == null || item.ItemData == null || item.Count <= 0 || Contains(item)) return false;
            return GetAcceptableAmount(item, -1) >= item.Count;
        }

        /// <summary>내부 아이템은 어떤 슬롯이든 스왑 가능. 외부 아이템은 한도 안의 묶음을 빈칸에 놓거나 같은 아이템 스택에 전량 합쳐질 때만 가능.</summary>
        public bool CanPlaceAt(RuntimeItem item, int slot)
        {
            if (item == null || item.ItemData == null || slot < 0 || slot >= _slots.Length) return false;
            if (_items.Contains(item)) return true;
            return item.Count > 0 && GetAcceptableAmount(item, slot) >= item.Count;
        }

        public bool AddAt(RuntimeItem item, int slot)
        {
            if (item == null || item.ItemData == null || slot < 0 || slot >= _slots.Length) return false;

            bool isInternal = _items.Contains(item);
            var target = _slots[slot];
            int limit = StackLimitOf(item.ItemData);

            if (target != null && target != item)
            {
                if (target.ItemData.itemID == item.ItemData.itemID && limit > 1)
                {
                    int space = limit - target.Count;
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

            // CanPlaceAt을 거치지 않은 호출에서도 한도를 넘는 묶음이 한 칸에 들어가지 않게 막는다
            if (!isInternal && item.Count > limit) return false;

            if (isInternal && item.SlotIndex >= 0 && item.SlotIndex < _slots.Length)
            {
                _slots[item.SlotIndex] = null;
            }

            _slots[slot] = item;
            item.SlotIndex = slot;
            if (!isInternal) _items.Add(item);
            return true;
        }

        /// <summary>
        /// 스택 가능한 아이템은 기존 스택에 먼저 합치고, 남은 수량은 한도만큼씩 나눠 앞쪽 빈칸에 넣는다.
        /// 전량이 들어갈 때만 실행한다(부분 수용은 SlotItemTransfer가 미리 수량을 나눠서 넣는다).
        /// </summary>
        public bool TryAddAuto(RuntimeItem item)
        {
            // 외부에서 들어오는 아이템 전용. 내부 아이템을 넣으면 부분 병합 뒤 슬롯이 바뀌는 부작용이 있다.
            if (!CanAdd(item)) return false;

            int limit = StackLimitOf(item.ItemData);
            if (limit > 1)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    var existing = _slots[i];
                    if (!IsSameStack(existing, item) || existing.Count >= limit) continue;

                    int addCount = Mathf.Min(limit - existing.Count, item.Count);
                    existing.Count += addCount;
                    item.Count -= addCount;

                    if (item.Count <= 0) return true;
                }
            }

            // 한도를 넘는 나머지는 새 묶음으로 떼어 빈칸에 넣고, 마지막 나머지는 원래 개체가 들고 들어간다
            while (item.Count > limit)
            {
                int empty = FindFirstEmptySlot();
                if (empty < 0) return false; // CanAdd가 막으므로 정상 흐름에서는 오지 않는다
                var piece = new RuntimeItem(item.ItemData) { Count = limit };
                item.Count -= limit;
                AddAt(piece, empty);
            }

            int last = FindFirstEmptySlot();
            return last >= 0 && AddAt(item, last);
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
