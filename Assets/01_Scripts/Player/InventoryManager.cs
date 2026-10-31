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
        public event Action<string> OnInventoryActionFailed;

        event Action ISlotItemContainer.OnContainerChanged
        {
            add => OnInventoryChanged += value;
            remove => OnInventoryChanged -= value;
        }

        [Header("Base Settings")]
        [SerializeField] private int _baseMaxSlots = 30;
        [Tooltip("가방에서 버린 아이템이 떨어질 수 있는 플레이어 주변 반경(칸)")]
        [Min(1)] [SerializeField] private int _discardRadius = 2;
        [Tooltip("바닥에 버리기 허용 여부. 허브에는 바닥이 없으므로 던전 플레이어 프리팹에서만 켠다")]
        [SerializeField] private bool _allowDiscard = false;

        public bool CanDiscard => _allowDiscard;

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

        /// <summary>아이템을 모두 비우고 용량을 기본 칸으로 되돌린다 (사망 처리용. 직후 장비도 비워진다).</summary>
        public void ClearInventory()
        {
            _container.Clear();
            _container.Resize(_baseMaxSlots, null);
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

        /// <summary>
        /// 아이템을 amount개만큼 플레이어 주변 바닥에 버린다. 반경 안에 빈 칸이 없거나 버릴 수 없는 곳(허브)이면 가방에 그대로 둔다.
        /// </summary>
        public bool TryDiscard(RuntimeItem item, int amount)
        {
            if (item == null || !_container.Contains(item) || amount <= 0) return false;
            if (!_allowDiscard)
            {
                OnInventoryActionFailed?.Invoke("여기서는 아이템을 버릴 수 없습니다.");
                return false;
            }
            amount = Mathf.Min(amount, item.Count);

            // 일부만 버리면 바닥에 놓일 새 묶음을 만들고, 전부 버리면 원래 개체를 그대로 바닥으로 보낸다
            RuntimeItem dropped = amount == item.Count ? item : new RuntimeItem(item.ItemData) { Count = amount };

            bool success = false;
            var handler = BagBack100.Core.GlobalEvents.OnItemDiscardRequested;
            if (handler != null) handler(dropped, transform.position, _discardRadius, result => success = result);

            if (!success)
            {
                OnInventoryActionFailed?.Invoke(handler == null ? "여기서는 아이템을 버릴 수 없습니다." : "주변에 버릴 공간이 없습니다.");
                return false;
            }

            RemoveAmount(item, amount);
            return true;
        }

        /// <summary>아이템을 amount개만큼 영구 삭제한다 (휴지통).</summary>
        public bool DeleteItem(RuntimeItem item, int amount)
        {
            if (item == null || !_container.Contains(item) || amount <= 0) return false;
            RemoveAmount(item, Mathf.Min(amount, item.Count));
            return true;
        }

        private void RemoveAmount(RuntimeItem item, int amount)
        {
            if (amount >= item.Count) _container.Remove(item);
            else item.Count -= amount;
            NotifyInventoryChanged();
        }

        /// <summary>스택에서 1개를 소모한다. 마지막 1개면 슬롯에서 제거한다.</summary>
        public bool ConsumeOne(RuntimeItem item)
        {
            if (item == null || !_container.Contains(item)) return false;

            if (item.Count > 1) item.Count--;
            else _container.Remove(item);

            NotifyInventoryChanged();
            return true;
        }

        /// <summary>같은 itemID 아이템의 총 수량 (퀵슬롯 표시용)</summary>
        public int CountItems(int itemID)
        {
            int total = 0;
            var items = _container.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].ItemData.itemID == itemID) total += items[i].Count;
            }
            return total;
        }

        /// <summary>같은 itemID 중 가장 앞 슬롯의 아이템 (퀵슬롯 사용 대상)</summary>
        public RuntimeItem FindFirstItem(int itemID)
        {
            RuntimeItem found = null;
            var items = _container.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].ItemData.itemID != itemID) continue;
                if (found == null || items[i].SlotIndex < found.SlotIndex) found = items[i];
            }
            return found;
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
