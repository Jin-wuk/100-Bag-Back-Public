using UnityEngine;
using UnityEngine.UI;
using BagBack100.Player;
using BagBack100.Data.Core;
using System.Collections.Generic;
using System.Linq;
using BagBack100.Hub;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 허브 창고(StorageManager) 그리드 뷰. 공통 동작은 ItemContainerUIBase, 여기서는 매니저 바인딩·더블클릭/E키 시 가방으로 이동·우클릭 나누기(가방 패널 메뉴 사용)를 정의.
    /// Dependencies: StorageManager, InventoryManager, InventoryUIController
    /// Events: 없음
    /// </summary>
    public class StorageUIController : ItemContainerUIBase
    {
        [Header("References")]
        [SerializeField] private StorageManager _storageManager;

        private void Awake()
        {
            StorageManager.OnManagerCreated += OnManagerCreated;
            var existing = _storageManager != null ? _storageManager : FindObjectOfType<StorageManager>();
            if (existing != null) OnManagerCreated(existing);
        }

        private void OnEnable()
        {
            var existing = _storageManager != null ? _storageManager : FindObjectOfType<StorageManager>();
            if (existing != null) OnManagerCreated(existing);
        }

        protected override void OnDestroy()
        {
            StorageManager.OnManagerCreated -= OnManagerCreated;
            base.OnDestroy();
        }

        private void OnManagerCreated(StorageManager manager)
        {
            _storageManager = manager;
            BindContainer(manager);
        }

        public void Bind(StorageManager invManager) => OnManagerCreated(invManager);

        protected override ISlotItemContainer ResolveQuickTransferTarget()
        {
            var invUI = FindObjectOfType<InventoryUIController>();
            if (invUI == null || !invUI.gameObject.activeInHierarchy) return null;
            return FindObjectOfType<InventoryManager>();
        }

        public override void OnItemDoubleClicked(DraggableItemUI clickedItem)
        {
            if (IsHoldingItem || clickedItem == null || clickedItem.Item == null || Container == null) return;

            TransferTo(FindObjectOfType<InventoryManager>(), clickedItem.Item);
        }

        // 나누기: 메뉴·수량 창은 가방 패널 것을 쓰고, 나눈 수량은 가방 빈칸으로 간다
        public override void OnItemRightClicked(DraggableItemUI clickedItem, UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (IsHoldingItem || clickedItem == null || clickedItem.Item == null || Container == null) return;
            var invUI = FindObjectOfType<InventoryUIController>();
            if (invUI != null) invUI.TryOpenItemMenu(Container, clickedItem.Item, eventData);
        }
    }
}
