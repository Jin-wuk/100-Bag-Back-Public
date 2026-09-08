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
        private bool _ignoreFirstClick;

        private void Update()
        {
            if (_heldItemUI != null)
            {
                // 마우스 따라다니기
                Vector2 localMouse;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _dragContainer != null ? _dragContainer : _gridContainer, 
                    UnityEngine.Input.mousePosition, null, out localMouse);
                _heldItemUI.rectTransform.localPosition = localMouse;

                // 좌클릭 시 내려놓기
                if (UnityEngine.Input.GetMouseButtonDown(0))
                {
                    if (_ignoreFirstClick)
                    {
                        _ignoreFirstClick = false;
                        return;
                    }
                    TryDropHeldItem();
                }
            }
        }

        public void OnItemClicked(DraggableItemUI clickedItem)
        {
            if (_heldItemUI == null)
            {
                _heldItemUI = clickedItem;
                _heldItemUI.SetHeldState(true);
                // 최상단 컨테이너로 이동시켜서 렌더링 순서 최상단 유지
                _heldItemUI.transform.SetParent(_dragContainer != null ? _dragContainer : _gridContainer, false);
                _ignoreFirstClick = true;
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

        private void TryDropHeldItem()
        {
            if (_heldItemUI == null) return;

            // 드랍 타겟 슬롯 계산
            int targetSlot = -1;
            for (int i = 0; i < _cells.Count; i++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)_cells[i], UnityEngine.Input.mousePosition, null))
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
                
                if (!_itemUIs.ContainsKey(item)) _itemUIs.Add(item, droppedItem.gameObject);
                
                _inventoryManager.AddItemAt(item, targetSlot);
            }
            else
            {
                // 외부 아이템인데 배치 실패 시 버림
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

        // ================= External Pickup Logic =================

        private void OnEnable()
        {
            DimensionLooter.Core.GlobalEvents.OnItemPickedUpByMouse += HandleExternalItemPickup;
        }

        private void OnDisable()
        {
            DimensionLooter.Core.GlobalEvents.OnItemPickedUpByMouse -= HandleExternalItemPickup;
        }

        private void HandleExternalItemPickup(RuntimeItem item)
        {
            if (_heldItemUI != null) return;

            GameObject itemGo = Instantiate(_draggableItemPrefab);
            DraggableItemUI draggable = itemGo.GetComponent<DraggableItemUI>();
            draggable.Setup(item, this);
            
            _heldItemUI = draggable;
            _heldItemUI.transform.SetParent(_dragContainer != null ? _dragContainer : _gridContainer, false);
            _heldItemUI.SetHeldState(true);
            _ignoreFirstClick = true;
        }
    }
}
