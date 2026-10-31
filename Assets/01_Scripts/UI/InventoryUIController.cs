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

        [Header("Item Actions")]
        [SerializeField] private ItemContextMenuUI _contextMenu;
        [SerializeField] private ItemQuantityDialogUI _quantityDialog;
        [SerializeField] private UIToast _toast;

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
            if (_inventoryManager != null) _inventoryManager.OnInventoryActionFailed -= ShowMessage;
            base.OnDestroy();
        }

        private void OnManagerCreated(InventoryManager manager)
        {
            if (_inventoryManager != null) _inventoryManager.OnInventoryActionFailed -= ShowMessage;
            _inventoryManager = manager;
            if (_inventoryManager != null) _inventoryManager.OnInventoryActionFailed += ShowMessage;
            BindContainer(manager);
        }

        private void ShowMessage(string message)
        {
            if (_toast != null) _toast.Show(message);
            else Debug.LogWarning($"[Inventory] {message}");
        }

        public override void OnItemRightClicked(DraggableItemUI clickedItem, UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (IsHoldingItem || clickedItem == null || clickedItem.Item == null || _contextMenu == null || _inventoryManager == null) return;

            var item = clickedItem.Item;
            // 허브에는 바닥이 없으므로 버리기 버튼을 숨긴다 (판매 등 허브 전용 버튼은 이후 추가)
            System.Action onDiscard = _inventoryManager.CanDiscard ? () => RequestDiscard(item) : (System.Action)null;
            _contextMenu.Open(eventData.position, eventData.pressEventCamera, onDiscard);
        }

        // 1개짜리는 바로 버리고, 여러 개 쌓인 것은 수량을 고르게 한다
        private void RequestDiscard(RuntimeItem item)
        {
            if (item.Count <= 1 || _quantityDialog == null)
            {
                _inventoryManager.TryDiscard(item, item.Count);
                return;
            }
            _quantityDialog.Open($"{item.ItemData.itemName} 버리기", item.Count, "버리기",
                amount => _inventoryManager.TryDiscard(item, amount));
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

            // 소모품은 더블클릭으로 바로 사용 (던전 전용. 허브에는 ConsumableController가 없어 무시)
            if (clickedItem.Item.ItemData is BagBack100.Data.Models.ConsumableDataSO)
            {
                // 소모품 규칙은 인벤토리와 같은 플레이어 오브젝트에 붙어 있다
                if (_inventoryManager != null && _inventoryManager.TryGetComponent(out ConsumableController consumables))
                {
                    consumables.TryUse(clickedItem.Item);
                }
                return;
            }

            var equipmentManager = FindObjectOfType<EquipmentManager>();
            if (equipmentManager != null)
            {
                equipmentManager.TryEquip(clickedItem.Item);
            }
        }
    }
}
