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
    ///       컨테이너 사이 이동은 SlotItemTransfer로 하며, 대상이 다 못 받으면 들어갈 만큼만 옮긴다.
    /// Dependencies: ISlotItemContainer, SlotItemTransfer, DraggableItemUI, IItemContainerUI, IItemDropTarget
    /// Events: 없음
    /// </summary>
    public abstract class ItemContainerUIBase : MonoBehaviour, IItemContainerUI
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private RectTransform _gridContainer;
        [SerializeField] private RectTransform _dragContainer; // 드래그 중인 아이템이 표시될 최상단 부모
        [Tooltip("그리드가 스크롤되는 경우의 Viewport(RectMask2D). 마우스가 이 밖이면 가려진 칸에 드롭하지 않는다. 비우면 검사 생략")]
        [SerializeField] private RectTransform _slotViewport;

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
        private int _lastDisplaySlots = -1;

        protected ISlotItemContainer Container => _container;
        protected bool IsHoldingItem => _heldItemUI != null;

        /// <summary>그릴 칸 수. MaxSlots를 넘는 칸은 잠금으로 그린다. 기본은 MaxSlots(잠금 칸 없음), 가방만 최대 가능 칸 수로 재정의</summary>
        protected virtual int DisplaySlotCount => _container != null ? _container.MaxSlots : 0;

        /// <summary>RefreshUI가 끝난 뒤 호출. 칸 수·아이템 수 표시 같은 부가 UI를 갱신한다</summary>
        protected virtual void OnRefreshed() { }

        /// <summary>E키 빠른 이동 대상 컨테이너. 대상 패널이 닫혀 있으면 null.</summary>
        protected abstract ISlotItemContainer ResolveQuickTransferTarget();

        public abstract void OnItemDoubleClicked(DraggableItemUI clickedItem);

        /// <summary>우클릭 메뉴가 필요한 컨테이너만 재정의한다. 가방은 버리기·판매·나누기, 창고·파우치는 나누기(가방 패널 메뉴 사용).</summary>
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

        public ISlotItemContainer SlotContainer => _container;

        // 다른 컨테이너에서 끌어온 아이템 전용. 전량이 아니어도 일부라도 들어가면 놓을 수 있다 (나머지는 원래 칸에 남음)
        public bool CanPlaceItem(RuntimeItem item, int slotIndex) => _container != null && _container.GetAcceptableAmount(item, slotIndex) > 0;

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
            if (_container == null || sourceContainer == null) return;
            SlotItemTransfer.Move(sourceContainer.SlotContainer, _container, item, targetSlot);
        }

        // ================= Rendering =================

        protected void RefreshUI()
        {
            if (_container == null || _gridContainer == null || _gridCellPrefab == null || _draggableItemPrefab == null) return;

            int displaySlots = DisplaySlotCount;
            if (_lastMaxSlots != _container.MaxSlots || _lastDisplaySlots != displaySlots || _cells.Count != _container.MaxSlots)
            {
                DrawGrid();
                _lastMaxSlots = _container.MaxSlots;
                _lastDisplaySlots = displaySlots;
            }

            DrawItems();
            OnRefreshed();
        }

        private void DrawGrid()
        {
            foreach (Transform child in _gridContainer) Destroy(child.gameObject);
            _cells.Clear();
            _itemUIs.Clear(); // 슬롯 파괴 시 하위 아이템 객체도 파괴되므로 딕셔너리 초기화 필수

            // 잠긴 칸(MaxSlots 이상)은 _cells에 넣지 않아 GetSlotIndex·드롭·E키 이동에서 자동으로 빠진다
            int maxSlots = _container.MaxSlots;
            int total = Mathf.Max(maxSlots, DisplaySlotCount);
            for (int i = 0; i < total; i++)
            {
                var cell = Instantiate(_gridCellPrefab, _gridContainer);
                bool locked = i >= maxSlots;
                cell.name = locked ? $"Locked_{i}" : $"Slot_{i}";
                if (cell.TryGetComponent(out GridCellUI cellUI)) cellUI.SetLocked(locked);
                if (!locked) _cells.Add(cell.transform);
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
                    FitToCell(draggable.rectTransform);
                }
            }
        }

        // 아이콘이 칸(GridLayout 셀) 크기를 그대로 따르게 해, 컨테이너마다 칸 크기가 달라도 프리팹 하나로 쓴다
        private static void FitToCell(RectTransform itemRect)
        {
            itemRect.anchorMin = Vector2.zero;
            itemRect.anchorMax = Vector2.one;
            itemRect.pivot = new Vector2(0.5f, 0.5f);
            itemRect.sizeDelta = Vector2.zero;
            itemRect.anchoredPosition = Vector2.zero;
            itemRect.localScale = Vector3.one;
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
            // 칸 크기·배율이 컨테이너마다 달라서, 옮긴 부모 아래에서도 칸에 있을 때와 같은 화면상 크기로 맞춘다.
            // SetHeldState가 크기를 바꾸므로 그 전에 잰다
            RectTransform heldRect = _heldItemUI.rectTransform;
            Vector2 worldSize = Vector2.Scale(heldRect.rect.size, heldRect.lossyScale);
            _heldItemUI.SetHeldState(true);

            // 패널 잘림(Mask) 및 타 패널에 가려짐 방지를 위해 최상단 캔버스를 우선 부모로 설정합니다.
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            RectTransform targetParent = _dragContainer;

            if (targetParent == null || (rootCanvas != null && targetParent.IsChildOf(transform)))
            {
                targetParent = rootCanvas != null ? rootCanvas.GetComponent<RectTransform>() : _gridContainer;
            }

            _heldItemUI.transform.SetParent(targetParent, false);
            Vector3 parentScale = targetParent.lossyScale;
            heldRect.anchorMin = heldRect.anchorMax = new Vector2(0.5f, 0.5f);
            heldRect.sizeDelta = new Vector2(worldSize.x / Mathf.Max(parentScale.x, 0.0001f), worldSize.y / Mathf.Max(parentScale.y, 0.0001f));

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
                    // 일부만 옮겨지면 원래 묶음이 남으므로 아이콘을 먼저 원위치한다. 전량이 옮겨지면 RefreshUI가 아이콘을 정리한다
                    ReturnHeldItemToSlot();
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
                FitToCell(held.rectTransform);
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

            SlotItemTransfer.Move(_container, ResolveQuickTransferTarget(), item, -1);
        }

        /// <summary>더블클릭 등으로 이 컨테이너의 아이템을 target으로 보낸다. 다 안 들어가면 들어갈 만큼만 옮긴다.</summary>
        protected void TransferTo(ISlotItemContainer target, RuntimeItem item)
        {
            if (IsHoldingItem || item == null || _container == null) return;
            SlotItemTransfer.Move(_container, target, item, -1);
        }

        private Camera GetUICamera()
        {
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            return (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? rootCanvas.worldCamera : null;
        }

        private int GetSlotIndexAtScreenPos(Vector2 screenPosition, Camera cam)
        {
            // 스크롤로 가려진 칸은 화면 좌표상 Viewport 밖에 있으므로 먼저 걸러낸다
            if (_slotViewport != null && !RectTransformUtility.RectangleContainsScreenPoint(_slotViewport, screenPosition, cam)) return -1;

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
