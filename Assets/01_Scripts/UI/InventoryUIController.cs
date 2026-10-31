using UnityEngine;
using UnityEngine.UI;
using BagBack100.Player;
using BagBack100.Hub;
using BagBack100.Data.Core;
using System.Collections.Generic;
using System.Linq;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 플레이어 가방(InventoryManager) 그리드 뷰. 공통 동작은 ItemContainerUIBase, 여기서는 매니저 바인딩·더블클릭 장착·E키 대상(창고)만 정의.
    /// Dependencies: InventoryManager, EquipmentManager, StorageUIController, StorageManager
    /// Events: 없음
    /// </summary>
    public class InventoryUIController : ItemContainerUIBase
    {
        [Header("References")]
        [SerializeField] private InventoryManager _inventoryManager;

        private void Awake()
        {
            InventoryManager.OnManagerCreated += OnManagerCreated;
            var existing = _inventoryManager != null ? _inventoryManager : FindObjectOfType<InventoryManager>();
            if (existing != null) OnManagerCreated(existing);
        }

        private void OnEnable()
        {
            var existing = _inventoryManager != null ? _inventoryManager : FindObjectOfType<InventoryManager>();
            if (existing != null) OnManagerCreated(existing);
        }

        protected override void OnDestroy()
        {
            InventoryManager.OnManagerCreated -= OnManagerCreated;
            base.OnDestroy();
        }

        private void OnManagerCreated(InventoryManager manager)
        {
            _inventoryManager = manager;
            BindContainer(manager);
        }

        public void Bind(InventoryManager invManager) => OnManagerCreated(invManager);

        protected override ISlotItemContainer ResolveQuickTransferTarget()
        {
            var storageUI = FindObjectOfType<StorageUIController>();
            if (storageUI == null || !storageUI.gameObject.activeInHierarchy) return null;
            return FindObjectOfType<StorageManager>();
        }

        public override void OnItemDoubleClicked(DraggableItemUI clickedItem)
        {
            if (IsHoldingItem || clickedItem == null || clickedItem.Item == null) return;
            var equipmentManager = FindObjectOfType<EquipmentManager>();
            if (equipmentManager != null)
            {
                equipmentManager.TryEquip(clickedItem.Item);
            }
        }
    }
}
