using UnityEngine;
using UnityEngine.UI;
using DimensionLooter.Player;
using DimensionLooter.Data.Core;
using System.Collections.Generic;
using System.Linq;

namespace DimensionLooter.UI
{
    public class InventoryUIController : MonoBehaviour
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
            if (_inventoryManager == null) 
            {
                var existing = FindObjectOfType<InventoryManager>();
                if (existing != null) OnManagerCreated(existing);
            }
        }

        private void OnManagerCreated(InventoryManager manager)
        {
            _inventoryManager = manager;
            _inventoryManager.OnInventoryChanged += RefreshUI;
            RefreshUI();
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
        }

        private int _lastMaxSlots = -1;

        private void RefreshUI()
        {
            if (_inventoryManager == null) return;

            if (_lastMaxSlots != _inventoryManager.MaxSlots)
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
                    Destroy(kvp.Value);
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
                if (!_itemUIs.TryGetValue(item, out itemGo))
                {
                    itemGo = Instantiate(_draggableItemPrefab);
                    _itemUIs.Add(item, itemGo);
                    draggable = itemGo.GetComponent<DraggableItemUI>();
                }
                else
                {
                    draggable = itemGo.GetComponent<DraggableItemUI>();
                }

                // 현재 드래그 중인 아이템이 아니면 원래 슬롯에 배치
                if (draggable != _heldItemUI)
                {
                    draggable.Setup(item, this);
                    itemGo.transform.SetParent(_cells[item.SlotIndex], false);
                }
            }
        }

        // ================= Drag and Drop Logic =================

        private DraggableItemUI _heldItemUI;

        public void OnItemBeginDrag(DraggableItemUI draggedItem, UnityEngine.EventSystems.PointerEventData eventData)
        {
            _heldItemUI = draggedItem;
            _heldItemUI.SetHeldState(true);
            
            // 패널 잘림(Mask) 및 가려짐 방지를 위해 가장 최상단의 캔버스를 찾아 부모로 설정합니다.
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            RectTransform targetParent = _dragContainer;
            
            if (targetParent == null)
            {
                targetParent = rootCanvas != null ? rootCanvas.GetComponent<RectTransform>() : _gridContainer;
            }

            _heldItemUI.transform.SetParent(targetParent, false);
            
            // 부모 컨테이너(가 있다면)와 아이템 자체를 하이어라키 맨 아래(렌더링 최상단)로 이동
            if (_dragContainer != null) _dragContainer.SetAsLastSibling();
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

        public void OnItemDoubleClicked(DraggableItemUI clickedItem)
        {
            if (_heldItemUI != null) return;
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

            if (targetSlot >= 0 && _inventoryManager.CanPlaceItemAtSlot(item, targetSlot))
            {
                _heldItemUI.SetHeldState(false);
                var droppedItem = _heldItemUI;
                _heldItemUI = null;
                
                // 기존 위치에서 이동하는 것이라면 스왑을 포함하여 AddItemAt 호출
                _inventoryManager.AddItemAt(item, targetSlot);
            }
            else
            {
                // 외부 아이템인데 배치 실패 시 버림 (기존 외부획득 로직 잔재. 필요 시 유지)
                if (item.SlotIndex < 0)
                {
                    var player = FindObjectOfType<PlayerController>();
                    Vector3 dropPos = player != null ? player.transform.position : Vector3.zero;
                    DimensionLooter.Core.GlobalEvents.OnItemDroppedOnGround?.Invoke(item, dropPos);
                    
                    Destroy(_heldItemUI.gameObject);
                    _heldItemUI = null;
                }
                else
                {
                    // 원래 위치로 원상복구
                    _heldItemUI.SetHeldState(false);
                    _heldItemUI.transform.SetParent(_cells[item.SlotIndex], false);
                    _heldItemUI = null;
                }
            }
        }

    }
}
