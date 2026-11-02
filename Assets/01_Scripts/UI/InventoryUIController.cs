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
    /// Role: 플레이어 가방(InventoryManager) 그리드 뷰. 공통 동작은 ItemContainerUIBase, 여기서는 매니저 바인딩,
    ///       더블클릭(장착·소모품 사용), 우클릭 메뉴(버리기), E키 대상(창고 또는 보안 파우치)을 정의.
    /// Dependencies: InventoryManager, EquipmentManager, ConsumableController, StorageUIController, StorageManager,
    ///               SecurePouchUIController, SecurePouchManager, ItemContextMenuUI, ItemQuantityDialogUI, UIToast
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

        protected override void OnDisable()
        {
            base.OnDisable();
            // 메뉴·대화상자가 프리팹 어디에 놓이든 가방이 닫히면 함께 닫아 남은 콜백이 실행되지 않게 한다
            if (_contextMenu != null) _contextMenu.Close();
            if (_quantityDialog != null) _quantityDialog.Close();
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
            // 허브에는 바닥이 없으므로 버리기 버튼을 숨기고, 대장간이 열려 있을 때만 판매 버튼을 보인다
            System.Action onDiscard = _inventoryManager.CanDiscard ? () => RequestDiscard(item) : (System.Action)null;
            var sellSlot = FindObjectOfType<SellSlotUI>();
            System.Action onSell = sellSlot != null && sellSlot.IsAvailable ? () => sellSlot.RequestSell(item, _inventoryManager) : (System.Action)null;
            _contextMenu.Open(eventData.position, eventData.pressEventCamera, onDiscard, onSell);
        }

        // 1개짜리는 바로 버리고, 여러 개 쌓인 것은 수량을 고르게 한다
        private void RequestDiscard(RuntimeItem item)
        {
            if (item.Count <= 1 || _quantityDialog == null)
            {
                _inventoryManager.TryDiscard(item, item.Count);
                return;
            }
            _quantityDialog.Open($"Drop {item.ItemData.itemName}", item.Count, "Drop",
                amount => _inventoryManager.TryDiscard(item, amount));
        }

        public void Bind(InventoryManager invManager) => OnManagerCreated(invManager);

        // E키: 창고가 열려 있으면 창고로, 아니면 보안 파우치로 보낸다
        protected override ISlotItemContainer ResolveQuickTransferTarget()
        {
            var storageUI = FindObjectOfType<StorageUIController>();
            if (storageUI != null && storageUI.gameObject.activeInHierarchy) return FindObjectOfType<StorageManager>();

            // 파우치 UI가 보이지 않는 상태에서 화면에 없는 칸으로 아이템이 옮겨지지 않게 한다
            var pouchUI = FindObjectOfType<SecurePouchUIController>();
            if (pouchUI == null || !pouchUI.gameObject.activeInHierarchy) return null;
            return _inventoryManager != null && _inventoryManager.TryGetComponent(out SecurePouchManager pouch) ? pouch : null;
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
