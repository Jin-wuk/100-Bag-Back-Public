using UnityEngine;
using UnityEngine.UI;
using BagBack100.Player;
using BagBack100.Hub;
using BagBack100.Data.Core;
using System.Collections.Generic;
using System.Linq;

namespace BagBack100.UI
{
    public class InventoryUIController : MonoBehaviour, IItemContainerUI
    {
        [Header("References")]
        [SerializeField] private InventoryManager _inventoryManager;
        
        [Header("UI Elements")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private RectTransform _gridContainer;
        [SerializeField] private RectTransform _dragContainer; // 드래그 중인 아이템이 표시될 최상단 부모
        
        [Header("Prefabs")]
        [SerializeField] private GameObject _gridCellPrefab;
        [SerializeField] private GameObject _draggableItemPrefab;

        private Dictionary<RuntimeItem, GameObject> _itemUIs = new Dictionary<RuntimeItem, GameObject>();
        private List<Transform> _cells = new List<Transform>();
        private GridLayoutGroup _gridLayout;

        private void Awake()
        {
            if (_gridContainer != null) _gridLayout = _gridContainer.GetComponent<GridLayoutGroup>();
            
            InventoryManager.OnManagerCreated += OnManagerCreated;
            var existing = _inventoryManager != null ? _inventoryManager : FindObjectOfType<InventoryManager>();
            if (existing != null) OnManagerCreated(existing);
        }

        private void OnManagerCreated(InventoryManager manager)
        {
            if (_inventoryManager != null) _inventoryManager.OnInventoryChanged -= RefreshUI;
            _inventoryManager = manager;
            if (_inventoryManager != null)
            {
                _inventoryManager.OnInventoryChanged -= RefreshUI;
                _inventoryManager.OnInventoryChanged += RefreshUI;
            }
            RefreshUI();
        }

        private void OnEnable()
        {
            var existing = _inventoryManager != null ? _inventoryManager : FindObjectOfType<InventoryManager>();
            if (existing != null)
            {
                OnManagerCreated(existing);
            }
        }

        private void OnDestroy()
        {
            InventoryManager.OnManagerCreated -= OnManagerCreated;
            if (_inventoryManager != null) _inventoryManager.OnInventoryChanged -= RefreshUI;
        }

        public void Bind(InventoryManager invManager)
        {
            if (_inventoryManager != null) _inventoryManager.OnInventoryChanged -= RefreshUI;
            _inventoryManager = invManager;
            if (_inventoryManager != null) _inventoryManager.OnInventoryChanged += RefreshUI;
            RefreshUI();
        }

        
        public bool CanPlaceItem(RuntimeItem item, int slotIndex) => _inventoryManager != null && _inventoryManager.CanPlaceItemAtSlot(item, slotIndex);
        public void RemoveItem(RuntimeItem item) => _inventoryManager?.RemoveItem(item);
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
            if (_inventoryManager == null || !_inventoryManager.CanPlaceItemAtSlot(item, targetSlot)) return;
            sourceContainer.RemoveItem(item);
            _inventoryManager.AddItemAt(item, targetSlot);
        }

        private int _lastMaxSlots = -1;

        private void RefreshUI()
        {
            if (_inventoryManager == null || _gridContainer == null || _gridCellPrefab == null || _draggableItemPrefab == null) return;

            if (_lastMaxSlots != _inventoryManager.MaxSlots || _cells.Count != _inventoryManager.MaxSlots)
            {
                DrawGrid();
                _lastMaxSlots = _inventoryManager.MaxSlots;
            }

            DrawItems();
        }

        private void DrawGrid()
        {
            foreach (Transform child in _gridContainer) Destroy(child.gameObject);
            _cells.Clear();
            _itemUIs.Clear(); // 슬롯 파괴 시 하위 아이템 객체도 파괴되므로 딕셔너리 초기화 필수

            for (int i = 0; i < _inventoryManager.MaxSlots; i++)
            {
                var cell = Instantiate(_gridCellPrefab, _gridContainer);
                cell.name = $"Slot_{i}";
                _cells.Add(cell.transform);
            }
        }

        private void DrawItems()
        {
            var currentItems = _inventoryManager.GetAllItems();
            
            // 삭제된 아이템 UI 파괴
            List<RuntimeItem> toRemove = new List<RuntimeItem>();
            foreach (var kvp in _itemUIs)
            {
                if (!currentItems.Contains(kvp.Key))
                {
                    if (kvp.Value != null) Destroy(kvp.Value);
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var key in toRemove) _itemUIs.Remove(key);

            // 남은 아이템 UI 생성 및 위치 세팅
            foreach (var item in currentItems)
            {
                if (item.SlotIndex < 0 || item.SlotIndex >= _cells.Count) continue;

                GameObject itemGo;
                DraggableItemUI draggable;
                if (!_itemUIs.TryGetValue(item, out itemGo) || itemGo == null)
                {
                    itemGo = Instantiate(_draggableItemPrefab);
                    _itemUIs[item] = itemGo;
                    draggable = itemGo.GetComponent<DraggableItemUI>();
                }
                else
                {
                    draggable = itemGo.GetComponent<DraggableItemUI>();
                }

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

        // ================= Drag and Drop Logic =================

        private DraggableItemUI _heldItemUI;

        public void OnItemBeginDrag(DraggableItemUI draggedItem, UnityEngine.EventSystems.PointerEventData eventData)
        {
            _heldItemUI = draggedItem;
            _heldItemUI.SetHeldState(true);
            
            // 패널 잘림(Mask) 및 타 패널(창고 등)에 가려짐 방지를 위해 최상단 캔버스를 우선 부모로 설정합니다.
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            RectTransform targetParent = _dragContainer;
            
            if (targetParent == null || (rootCanvas != null && targetParent.IsChildOf(transform)))
            {
                targetParent = rootCanvas != null ? rootCanvas.GetComponent<RectTransform>() : _gridContainer;
            }

            _heldItemUI.transform.SetParent(targetParent, false);
            
            // 부모 컨테이너(가 있다면)와 아이템 자체를 하이어라키 맨 아래(렌더링 최상단)로 이동
            if (_dragContainer != null && targetParent == _dragContainer) _dragContainer.SetAsLastSibling();
            _heldItemUI.transform.SetAsLastSibling();
        }

        public void OnItemDrag(DraggableItemUI draggedItem, UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_heldItemUI == draggedItem)
            {
                Vector2 localMouse;
                RectTransform currentParent = (RectTransform)_heldItemUI.transform.parent;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    currentParent, 
                    eventData.position, eventData.pressEventCamera, out localMouse);
                _heldItemUI.rectTransform.localPosition = localMouse;
            }
        }

        public void OnItemEndDrag(DraggableItemUI draggedItem, UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_heldItemUI == draggedItem)
            {
                TryDropHeldItem(eventData.position, eventData.pressEventCamera);
            }
        }

        private void Update()
        {
            if (_heldItemUI == null && Input.GetKeyDown(KeyCode.E))
            {
                TryQuickTransferItemUnderMouse();
            }
        }

        private void TryQuickTransferItemUnderMouse()
        {
            if (_inventoryManager == null) return;

            var storageUI = FindObjectOfType<StorageUIController>();
            if (storageUI == null || !storageUI.gameObject.activeInHierarchy) return;

            var storageManager = FindObjectOfType<StorageManager>();
            if (storageManager == null) return;

            int hoveredSlot = GetSlotIndexAtScreenPos(Input.mousePosition);
            if (hoveredSlot < 0) return;

            RuntimeItem item = _inventoryManager.GetItemAtSlot(hoveredSlot);
            if (item == null) return;

            if (storageManager.CanAddItem(item))
            {
                _inventoryManager.RemoveItem(item);
                storageManager.TryAddNextAvailableSlot(item);
            }
        }

        private int GetSlotIndexAtScreenPos(Vector2 screenPosition)
        {
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            Camera uiCamera = (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? rootCanvas.worldCamera
                : null;

            for (int i = 0; i < _cells.Count; i++)
            {
                if (_cells[i] != null && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)_cells[i], screenPosition, uiCamera))
                {
                    return i;
                }
            }
            return -1;
        }

        public void OnItemDoubleClicked(DraggableItemUI clickedItem)
        {
            if (_heldItemUI != null || clickedItem == null || clickedItem.Item == null) return;
            var equipmentManager = FindObjectOfType<EquipmentManager>();
            if (equipmentManager != null)
            {
                equipmentManager.TryEquip(clickedItem.Item);
            }
        }

        private void TryDropHeldItem(Vector2 screenPosition, Camera cam)
        {
            if (_heldItemUI == null) return;

            // 드랍 타겟 슬롯 계산
            int targetSlot = -1;
            for (int i = 0; i < _cells.Count; i++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)_cells[i], screenPosition, cam))
                {
                    targetSlot = i;
                    break;
                }
            }

            RuntimeItem item = _heldItemUI.Item;

            
            if (targetSlot >= 0 && _inventoryManager != null && _inventoryManager.CanPlaceItemAtSlot(item, targetSlot))
            {
                _heldItemUI.SetHeldState(false);
                _heldItemUI = null;
                
                // 기존 위치에서 이동하는 것이라면 스왑을 포함하여 AddItemAt 호출
                _inventoryManager.AddItemAt(item, targetSlot);
            }
            else if (targetSlot < 0)
            {
                // 다른 컨테이너(창고 등)로 드래그했는지 확인
                if (UnityEngine.EventSystems.EventSystem.current != null)
                {
                    var pointerData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = screenPosition };
                    var results = new List<UnityEngine.EventSystems.RaycastResult>();
                    UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerData, results);
                    
                    foreach (var result in results)
                    {
                        var otherContainer = result.gameObject.GetComponentInParent<IItemContainerUI>();
                        if (otherContainer != null && (object)otherContainer != this)
                        {
                            int otherSlot = otherContainer.GetSlotIndex(result.gameObject.transform);
                            if (otherSlot >= 0 && otherContainer.CanPlaceItem(item, otherSlot))
                            {
                                _heldItemUI.SetHeldState(false);
                                Destroy(_heldItemUI.gameObject);
                                _heldItemUI = null;
                                otherContainer.MoveItemFrom(item, otherSlot, this);
                                return;
                            }
                        }
                    }
                }

                ReturnOrDropHeldItem(item);
            }
            else
            {
                ReturnOrDropHeldItem(item);
            }
        }

        private void ReturnOrDropHeldItem(RuntimeItem item)
        {
            if (_heldItemUI == null) return;

            if (item.SlotIndex < 0)
            {
                var player = FindObjectOfType<PlayerController>();
                Vector3 dropPos = player != null ? player.transform.position : Vector3.zero;
                BagBack100.Core.GlobalEvents.OnItemDroppedOnGround?.Invoke(item, dropPos);
                
                Destroy(_heldItemUI.gameObject);
                _heldItemUI = null;
            }
            else if (item.SlotIndex < _cells.Count)
            {
                _heldItemUI.SetHeldState(false);
                _heldItemUI.transform.SetParent(_cells[item.SlotIndex], false);
                _heldItemUI.rectTransform.anchoredPosition = Vector2.zero;
                _heldItemUI.rectTransform.localPosition = Vector3.zero;
                _heldItemUI = null;
            }
            else
            {
                _heldItemUI.SetHeldState(false);
                _heldItemUI = null;
                RefreshUI();
            }
        }
    }
}
