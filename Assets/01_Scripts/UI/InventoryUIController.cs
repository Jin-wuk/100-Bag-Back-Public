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
    ///       더블클릭(장착·소모품 사용), 우클릭 메뉴(버리기·판매·나누기), E키 대상(창고 또는 보안 파우치)을 정의.
    ///       창고·파우치의 우클릭 나누기도 이 클래스의 메뉴·수량 창으로 처리한다(나눈 수량은 가방 빈칸으로).
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
            CloseItemActions();
        }

        /// <summary>
        /// 열린 우클릭 메뉴·수량 창을 닫는다. 창고·파우치 패널만 닫히고 가방은 열려 있는 경우(허브에서 대장간 전환 등)
        /// 닫힌 패널의 아이템에 대한 나누기 확인이 늦게 실행되지 않도록 그 패널이 호출한다.
        /// </summary>
        public void CloseItemActions()
        {
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
            if (IsHoldingItem || clickedItem == null || clickedItem.Item == null) return;
            TryOpenItemMenu(_inventoryManager, clickedItem.Item, eventData);
        }

        /// <summary>
        /// 우클릭 메뉴를 연다. 창고·보안 파우치 UI도 이 메서드로 가방 패널의 메뉴·수량 창을 함께 쓴다(나누기 목적지가 항상 가방이라).
        /// 가방 아이템이면 버리기·판매·나누기, 다른 컨테이너 아이템이면 나누기만 보인다.
        /// </summary>
        public bool TryOpenItemMenu(ISlotItemContainer source, RuntimeItem item, UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (source == null || item == null || _contextMenu == null || _inventoryManager == null || !gameObject.activeInHierarchy) return false;

            System.Action onDiscard = null;
            System.Action onSell = null;
            if (ReferenceEquals(source, _inventoryManager))
            {
                // 허브에는 바닥이 없으므로 버리기 버튼을 숨기고, 잡화상이 열려 있을 때만 판매 버튼을 보인다
                if (_inventoryManager.CanDiscard) onDiscard = () => RequestDiscard(item);
                var sellSlot = FindObjectOfType<SellSlotUI>();
                if (sellSlot != null && sellSlot.IsAvailable) onSell = () => sellSlot.RequestSell(item, _inventoryManager);
            }
            // 가방에 빈칸이 없거나 나눌 수량이 없으면 나누기 버튼을 숨긴다 (유저 결정)
            System.Action onSplit = _inventoryManager.GetSplitMaxAmount(source, item) > 0 ? () => RequestSplit(source, item) : (System.Action)null;
            return _contextMenu.Open(eventData.position, eventData.pressEventCamera, onDiscard, onSell, onSplit);
        }

        // 고를 수 있는 최대치가 1이면 수량 창 없이 바로 나눈다
        private void RequestSplit(ISlotItemContainer source, RuntimeItem item)
        {
            int max = _inventoryManager.GetSplitMaxAmount(source, item);
            if (max <= 0) return;
            if (max == 1 || _quantityDialog == null)
            {
                _inventoryManager.TrySplitFrom(source, item, 1);
                return;
            }
            _quantityDialog.Open($"Split {item.ItemData.itemName}", max, "Split",
                amount => _inventoryManager.TrySplitFrom(source, item, amount));
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
