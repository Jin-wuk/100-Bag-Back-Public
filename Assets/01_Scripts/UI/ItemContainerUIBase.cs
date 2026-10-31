using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using BagBack100.Data.Core;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 슬롯형 컨테이너(인벤토리·창고·보안 파우치) UI의 공통 뷰. 그리드 생성, 아이템 아이콘 배치,
    ///       드래그 앤 드롭(내부 이동 / 다른 컨테이너 이동 / IItemDropTarget 드롭), E키 빠른 이동을 처리한다.
    ///       데이터 변경은 ISlotItemContainer 커맨드로만 요청한다.
    /// Dependencies: ISlotItemContainer, DraggableItemUI, IItemContainerUI, IItemDropTarget
    /// Events: 없음
    /// </summary>
    public abstract class ItemContainerUIBase : MonoBehaviour, IItemContainerUI
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private RectTransform _gridContainer;
        [SerializeField] private RectTransform _dragContainer; // 드래그 중인 아이템이 표시될 최상단 부모

        [Header("Prefabs")]
        [SerializeField] private GameObject _gridCellPrefab;
        [SerializeField] private GameObject _draggableItemPrefab;

        private readonly Dictionary<RuntimeItem, GameObject> _itemUIs = new Dictionary<RuntimeItem, GameObject>();
        private readonly List<Transform> _cells = new List<Transform>();
        private readonly List<RuntimeItem> _removeBuffer = new List<RuntimeItem>();
        private readonly List<RaycastResult> _raycastBuffer = new List<RaycastResult>();

        private ISlotItemContainer _container;
        private DraggableItemUI _heldItemUI;
        private int _lastMaxSlots = -1;

        protected ISlotItemContainer Container => _container;
        protected bool IsHoldingItem => _heldItemUI != null;

        /// <summary>E키 빠른 이동 대상 컨테이너. 대상 패널이 닫혀 있으면 null.</summary>
        protected abstract ISlotItemContainer ResolveQuickTransferTarget();

        public abstract void OnItemDoubleClicked(DraggableItemUI clickedItem);

        /// <summary>우클릭 메뉴가 필요한 컨테이너(가방)만 재정의한다.</summary>
        public virtual void OnItemRightClicked(DraggableItemUI clickedItem, PointerEventData eventData) { }

        protected void BindContainer(ISlotItemContainer container)
        {
            if (_container == container)
            {
                RefreshUI();
                return;
            }

            if (_container != null) _container.OnContainerChanged -= RefreshUI;
            _container = container;
            if (_container != null) _container.OnContainerChanged += RefreshUI;
            RefreshUI();
        }

        protected virtual void OnDisable()
        {
            // 패널이 닫힐 때 들고 있던 아이콘이 최상단 캔버스에 떠 있지 않도록 원위치
            if (_heldItemUI != null) ReturnHeldItemToSlot();
        }

        protected virtual void OnDestroy()
        {
            if (_container != null) _container.OnContainerChanged -= RefreshUI;
        }

        // ================= IItemContainerUI =================

        public bool CanPlaceItem(RuntimeItem item, int slotIndex) => _container != null && _container.CanPlaceItemAtSlot(item, slotIndex);

        public void RemoveItem(RuntimeItem item) => _container?.RemoveItem(item);

        public int GetSlotIndex(Transform cellTransform)
        {
            Transform current = cellTransform;
            while (current != null && current != _gridContainer)
            {
                int idx = _cells.IndexOf(current);
                if (idx >= 0) return idx;
                current = current.parent;
            }
            return -1;
        }

        public void MoveItemFrom(RuntimeItem item, int targetSlot, IItemContainerUI sourceContainer)
        {
            if (_container == null || !_container.CanPlaceItemAtSlot(item, targetSlot)) return;
            sourceContainer.RemoveItem(item);
            _container.AddItemAt(item, targetSlot);
        }

        // ================= Rendering =================

        protected void RefreshUI()
        {
            if (_container == null || _gridContainer == null || _gridCellPrefab == null || _draggableItemPrefab == null) return;

            if (_lastMaxSlots != _container.MaxSlots || _cells.Count != _container.MaxSlots)
            {
                DrawGrid();
                _lastMaxSlots = _container.MaxSlots;
            }

            DrawItems();
        }

        private void DrawGrid()
        {
            foreach (Transform child in _gridContainer) Destroy(child.gameObject);
            _cells.Clear();
            _itemUIs.Clear(); // 슬롯 파괴 시 하위 아이템 객체도 파괴되므로 딕셔너리 초기화 필수

            for (int i = 0; i < _container.MaxSlots; i++)
            {
                var cell = Instantiate(_gridCellPrefab, _gridContainer);
                cell.name = $"Slot_{i}";
                _cells.Add(cell.transform);
            }
        }

        private void DrawItems()
        {
            var currentItems = _container.GetAllItems();

            _removeBuffer.Clear();
            foreach (var kvp in _itemUIs)
            {
                if (!ContainsItem(currentItems, kvp.Key))
                {
                    if (_heldItemUI != null && _heldItemUI.gameObject == kvp.Value) _heldItemUI = null;
                    if (kvp.Value != null) Destroy(kvp.Value);
                    _removeBuffer.Add(kvp.Key);
                }
            }
            for (int i = 0; i < _removeBuffer.Count; i++) _itemUIs.Remove(_removeBuffer[i]);

            for (int i = 0; i < currentItems.Count; i++)
            {
                var item = currentItems[i];
                if (item.SlotIndex < 0 || item.SlotIndex >= _cells.Count) continue;

                if (!_itemUIs.TryGetValue(item, out GameObject itemGo) || itemGo == null)
                {
                    itemGo = Instantiate(_draggableItemPrefab);
                    _itemUIs[item] = itemGo;
                }
                var draggable = itemGo.GetComponent<DraggableItemUI>();

                // 현재 드래그 중인 아이템이 아니면 원래 슬롯에 배치
                if (draggable != _heldItemUI)
                {
                    itemGo.transform.SetParent(_cells[item.SlotIndex], false);
                    draggable.Setup(item, this);
                    draggable.rectTransform.anchoredPosition = Vector2.zero;
                    draggable.rectTransform.localPosition = Vector3.zero;
                }
            }
        }

        private static bool ContainsItem(IReadOnlyList<RuntimeItem> items, RuntimeItem target)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == target) return true;
            }
            return false;
        }

        // ================= Drag and Drop =================

        public void OnItemBeginDrag(DraggableItemUI draggedItem, PointerEventData eventData)
        {
            _heldItemUI = draggedItem;
            _heldItemUI.SetHeldState(true);

            // 패널 잘림(Mask) 및 타 패널에 가려짐 방지를 위해 최상단 캔버스를 우선 부모로 설정합니다.
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            RectTransform targetParent = _dragContainer;

            if (targetParent == null || (rootCanvas != null && targetParent.IsChildOf(transform)))
            {
                targetParent = rootCanvas != null ? rootCanvas.GetComponent<RectTransform>() : _gridContainer;
            }

            _heldItemUI.transform.SetParent(targetParent, false);

            if (_dragContainer != null && targetParent == _dragContainer) _dragContainer.SetAsLastSibling();
            _heldItemUI.transform.SetAsLastSibling();
        }

        public void OnItemDrag(DraggableItemUI draggedItem, PointerEventData eventData)
        {
            if (_heldItemUI != draggedItem) return;

            RectTransform currentParent = (RectTransform)_heldItemUI.transform.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                currentParent, eventData.position, eventData.pressEventCamera, out Vector2 localMouse);
            _heldItemUI.rectTransform.localPosition = localMouse;
        }

        public void OnItemEndDrag(DraggableItemUI draggedItem, PointerEventData eventData)
        {
            if (_heldItemUI == draggedItem)
            {
                TryDropHeldItem(eventData.position, eventData.pressEventCamera);
            }
        }

        private void TryDropHeldItem(Vector2 screenPosition, Camera cam)
        {
            if (_heldItemUI == null) return;

            RuntimeItem item = _heldItemUI.Item;
            int targetSlot = GetSlotIndexAtScreenPos(screenPosition, cam);

            if (targetSlot >= 0)
            {
                if (_container != null && _container.CanPlaceItemAtSlot(item, targetSlot))
                {
                    _heldItemUI.SetHeldState(false);
                    _heldItemUI = null;
                    _container.AddItemAt(item, targetSlot);
                    return;
                }
                ReturnHeldItemToSlot();
                return;
            }

            if (TryDropOnExternalTarget(item, screenPosition)) return;

            // 슬롯 밖 허공: 원위치 복귀 (바닥 버리기는 우클릭 메뉴로만 수행)
            ReturnHeldItemToSlot();
        }

        private bool TryDropOnExternalTarget(RuntimeItem item, Vector2 screenPosition)
        {
            if (EventSystem.current == null) return false;

            var pointerData = new PointerEventData(EventSystem.current) { position = screenPosition };
            _raycastBuffer.Clear();
            EventSystem.current.RaycastAll(pointerData, _raycastBuffer);

            // 1차: 소비·참조형 드롭 대상(휴지통·판매·퀵슬롯)을 컨테이너보다 우선
            for (int i = 0; i < _raycastBuffer.Count; i++)
            {
                var dropTarget = _raycastBuffer[i].gameObject.GetComponentInParent<IItemDropTarget>();
                if (dropTarget == null || !dropTarget.CanAcceptDrop(item, _container)) continue;

                // 드롭 대상이 원본에서 아이템을 빼면 RefreshUI가 아이콘을 정리하므로, 들고 있던 아이콘은 먼저 원위치
                ReturnHeldItemToSlot();
                dropTarget.AcceptDrop(item, _container);
                return true;
            }

            // 2차: 다른 슬롯 컨테이너로 이동
            for (int i = 0; i < _raycastBuffer.Count; i++)
            {
                var hitObject = _raycastBuffer[i].gameObject;
                var otherContainer = hitObject.GetComponentInParent<IItemContainerUI>();
                if (otherContainer == null || ReferenceEquals(otherContainer, this)) continue;

                int otherSlot = otherContainer.GetSlotIndex(hitObject.transform);
                if (otherSlot >= 0 && otherContainer.CanPlaceItem(item, otherSlot))
                {
                    _heldItemUI.SetHeldState(false);
                    Destroy(_heldItemUI.gameObject);
                    _itemUIs.Remove(item);
                    _heldItemUI = null;
                    otherContainer.MoveItemFrom(item, otherSlot, this);
                    return true;
                }
            }
            return false;
        }

        private void ReturnHeldItemToSlot()
        {
            if (_heldItemUI == null) return;

            var held = _heldItemUI;
            _heldItemUI = null;
            held.SetHeldState(false);

            RuntimeItem item = held.Item;
            if (item != null && item.SlotIndex >= 0 && item.SlotIndex < _cells.Count)
            {
                held.transform.SetParent(_cells[item.SlotIndex], false);
                held.rectTransform.anchoredPosition = Vector2.zero;
                held.rectTransform.localPosition = Vector3.zero;
            }
            else
            {
                // 슬롯이 사라진 아이템의 아이콘은 최상단 캔버스에 남지 않도록 파괴 (DrawItems는 무효 슬롯을 건너뛴다)
                if (item != null) _itemUIs.Remove(item);
                Destroy(held.gameObject);
                RefreshUI();
            }
        }

        // ================= Quick Transfer (E) =================

        protected virtual void Update()
        {
            if (_heldItemUI == null && !ItemQuantityDialogUI.IsAnyOpen && !ItemContextMenuUI.IsAnyOpen && Input.GetKeyDown(KeyCode.E))
            {
                TryQuickTransferItemUnderMouse();
            }
        }

        private void TryQuickTransferItemUnderMouse()
        {
            if (_container == null) return;

            int hoveredSlot = GetSlotIndexAtScreenPos(Input.mousePosition, GetUICamera());
            if (hoveredSlot < 0) return;

            RuntimeItem item = _container.GetItemAtSlot(hoveredSlot);
            if (item == null) return;

            var target = ResolveQuickTransferTarget();
            if (target == null || !target.CanAddItem(item)) return;

            int originalSlot = item.SlotIndex;
            _container.RemoveItem(item);
            if (!target.TryAddNextAvailableSlot(item))
            {
                _container.AddItemAt(item, originalSlot);
            }
        }

        private Camera GetUICamera()
        {
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            return (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? rootCanvas.worldCamera : null;
        }

        private int GetSlotIndexAtScreenPos(Vector2 screenPosition, Camera cam)
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                if (_cells[i] != null && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)_cells[i], screenPosition, cam))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
