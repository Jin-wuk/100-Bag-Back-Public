using UnityEngine;
using BagBack100.Player;
using BagBack100.Data.Core;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 보안 파우치(SecurePouchManager) 그리드 뷰. 공통 동작은 ItemContainerUIBase, 여기서는 매니저 바인딩과
    ///       더블클릭/E키 시 가방으로 꺼내기만 정의한다.
    /// Dependencies: SecurePouchManager, InventoryManager
    /// Events: 없음
    /// </summary>
    public class SecurePouchUIController : ItemContainerUIBase
    {
        [Header("References")]
        [SerializeField] private SecurePouchManager _pouchManager;

        private void Awake()
        {
            SecurePouchManager.OnManagerCreated += OnManagerCreated;
            var existing = _pouchManager != null ? _pouchManager : FindObjectOfType<SecurePouchManager>();
            if (existing != null) OnManagerCreated(existing);
        }

        private void OnEnable()
        {
            var existing = _pouchManager != null ? _pouchManager : FindObjectOfType<SecurePouchManager>();
            if (existing != null) OnManagerCreated(existing);
        }

        protected override void OnDestroy()
        {
            SecurePouchManager.OnManagerCreated -= OnManagerCreated;
            base.OnDestroy();
        }

        private void OnManagerCreated(SecurePouchManager manager)
        {
            _pouchManager = manager;
            BindContainer(manager);
        }

        // 파우치와 가방은 같은 오브젝트(플레이어 또는 허브 매니저 호스트)에 붙어 있다
        private InventoryManager FindInventory()
        {
            return _pouchManager != null && _pouchManager.TryGetComponent(out InventoryManager inventory) ? inventory : null;
        }

        protected override ISlotItemContainer ResolveQuickTransferTarget() => FindInventory();

        public override void OnItemDoubleClicked(DraggableItemUI clickedItem)
        {
            if (IsHoldingItem || clickedItem == null || clickedItem.Item == null || Container == null) return;

            var inventory = FindInventory();
            if (inventory == null || !inventory.CanAddItem(clickedItem.Item)) return;

            var item = clickedItem.Item;
            int originalSlot = item.SlotIndex;
            Container.RemoveItem(item);
            if (!inventory.TryAddNextAvailableSlot(item))
            {
                Container.AddItemAt(item, originalSlot);
            }
        }
    }
}
