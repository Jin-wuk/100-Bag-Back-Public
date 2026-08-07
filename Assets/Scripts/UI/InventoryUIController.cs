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
        [SerializeField] private PlayerInput _playerInput;
        
        [Header("UI Elements")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private RectTransform _gridContainer;
        [SerializeField] private RectTransform _itemsContainer;
        
        [Header("Prefabs")]
        [SerializeField] private GameObject _gridCellPrefab;
        [SerializeField] private GameObject _draggableItemPrefab;

        private Dictionary<RuntimeItem, GameObject> _itemUIs = new Dictionary<RuntimeItem, GameObject>();
        private UnityEngine.UI.GridLayoutGroup _gridLayout;

        private void Awake()
        {
            InventoryManager.OnManagerCreated += OnManagerCreated;
            PlayerInput.OnInputCreated += OnInputCreated;

            // 이미 존재하는 인스턴스가 있다면 가져오기 (지연 스폰과 초기 스폰 모두 완벽 대응)
            if (_inventoryManager == null) 
            {
                var existing = FindObjectOfType<InventoryManager>();
                if (existing != null) OnManagerCreated(existing);
            }
            if (_playerInput == null)
            {
                var existing = FindObjectOfType<PlayerInput>();
                if (existing != null) OnInputCreated(existing);
            }

            if (_panelRoot != null) _panelRoot.SetActive(false);
            if (_gridContainer != null) _gridLayout = _gridContainer.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        }

        private void OnManagerCreated(InventoryManager manager)
        {
            _inventoryManager = manager;
            _inventoryManager.OnInventoryChanged += RefreshUI;
        }

        private void OnInputCreated(PlayerInput input)
        {
            _playerInput = input;
            _playerInput.OnInventoryToggle += TogglePanel;
        }

        private void Start()
        {
            // 의존성 탐색은 Awake 시점에 정적 이벤트 구독을 통해 수행됩니다.
        }

        private void OnDestroy()
        {
            InventoryManager.OnManagerCreated -= OnManagerCreated;
            PlayerInput.OnInputCreated -= OnInputCreated;

            if (_inventoryManager != null)
            {
                _inventoryManager.OnInventoryChanged -= RefreshUI;
            }
            if (_playerInput != null)
            {
                _playerInput.OnInventoryToggle -= TogglePanel;
            }
        }

        public void Bind(InventoryManager invManager, PlayerInput input)
        {
            // 기존 이벤트 해제
            if (_inventoryManager != null) _inventoryManager.OnInventoryChanged -= RefreshUI;
            if (_playerInput != null) _playerInput.OnInventoryToggle -= TogglePanel;

            _inventoryManager = invManager;
            _playerInput = input;

            // 새 이벤트 구독
            if (_inventoryManager != null) _inventoryManager.OnInventoryChanged += RefreshUI;
            if (_playerInput != null) _playerInput.OnInventoryToggle += TogglePanel;
        }

        public void TogglePanel()
        {
            bool isActive = !_panelRoot.activeSelf;
            _panelRoot.SetActive(isActive);

            if (_playerInput != null)
            {
                _playerInput.IsMovementBlocked = isActive;
            }

            if (isActive)
            {
                RefreshUI();
            }
        }

        private int _lastGridWidth = -1;
        private int _lastGridHeight = -1;

        private void RefreshUI()
        {
            if (!_panelRoot.activeSelf) return;

            // 가방 사이즈가 실제로 변했을 때만 격자를 새로 그림 (최적화)
            if (_lastGridWidth != _inventoryManager.GridWidth || _lastGridHeight != _inventoryManager.GridHeight)
            {
                DrawGrid();
                _lastGridWidth = _inventoryManager.GridWidth;
                _lastGridHeight = _inventoryManager.GridHeight;
            }

            DrawItems();
        }

        private void DrawGrid()
        {
            // Clear old grid
            foreach (Transform child in _gridContainer)
            {
                Destroy(child.gameObject);
            }

            // Grid 레이아웃 강제 설정 (가방 가로폭 일치화 및 좌측 상단 강제 정렬)
            if (_gridLayout != null)
            {
                _gridLayout.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
                _gridLayout.constraintCount = _inventoryManager.GridWidth;
                
                _gridLayout.childAlignment = TextAnchor.UpperLeft;
                _gridLayout.startCorner = UnityEngine.UI.GridLayoutGroup.Corner.UpperLeft;
                _gridLayout.startAxis = UnityEngine.UI.GridLayoutGroup.Axis.Horizontal;
                _gridLayout.padding = new RectOffset(0, 0, 0, 0);
            }

            // Draw new grid based on InventoryManager size
            for (int y = 0; y < _inventoryManager.GridHeight; y++)
            {
                for (int x = 0; x < _inventoryManager.GridWidth; x++)
                {
                    Instantiate(_gridCellPrefab, _gridContainer);
                }
            }
        }

        private void DrawItems()
        {
            if (_gridLayout == null)
            {
                Debug.LogError("[UI] _gridLayout이 NULL입니다. GridContainer에 GridLayoutGroup 컴포넌트가 부착되어 있는지 확인하십시오.");
                return;
            }

            Vector2 cellSize = _gridLayout.cellSize;
            Vector2 spacing = _gridLayout.spacing;

            var currentItems = _inventoryManager.GetAllItems();
            
            // 1. 제거된 아이템 UI 파괴 (상태 동기화)
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

            // 2. 아이템 렌더링 및 갱신
            foreach (var item in currentItems)
            {
                if (item.GridX < 0 || item.GridY < 0) continue;

                GameObject itemGo;
                DraggableItemUI draggable;
                if (!_itemUIs.TryGetValue(item, out itemGo))
                {
                    itemGo = Instantiate(_draggableItemPrefab, _itemsContainer);
                    _itemUIs.Add(item, itemGo);
                    draggable = itemGo.GetComponent<DraggableItemUI>();
                    if (draggable != null) draggable.Setup(item, _inventoryManager, cellSize, spacing, this);
                }
                else
                {
                    draggable = itemGo.GetComponent<DraggableItemUI>();
                }

                // 현재 마우스로 쥐고 있는(Held) 상태가 아닐 때만 제자리로 스냅시킴
                if (draggable != null && draggable != _heldItemUI)
                {
                    draggable.Setup(item, _inventoryManager, cellSize, spacing, this);
                    
                    RectTransform rt = itemGo.GetComponent<RectTransform>();
                    Rect itemsRect = _itemsContainer.rect;
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0, 1);

                    float posX = itemsRect.xMin + (item.GridX * (cellSize.x + spacing.x));
                    float posY = itemsRect.yMax - (item.GridY * (cellSize.y + spacing.y));
                    rt.localPosition = new Vector3(posX, posY, 0);
                    
                    draggable.ResetBorderFeedback(); // 내려놓아진 상태이므로 기본 테두리로 복구
                }
            }
        }

        // ================= Click-to-pickup Logic =================

        private DraggableItemUI _heldItemUI;
        private bool _ignoreFirstClick;
        private Vector2 _dragOffset; // 마우스와 아이템 좌측 상단 간의 오프셋

        private void Update()
        {
            if (_heldItemUI != null)
            {
                // 1. 우클릭 시 회전 (마우스 위치 기준 중심 회전 알고리즘 적용)
                if (UnityEngine.Input.GetMouseButtonDown(1))
                {
                    RuntimeItem item = _heldItemUI.Item;
                    int oldW = item.GetCurrentWidth();
                    int oldH = item.GetCurrentHeight();

                    item.IsRotated = !item.IsRotated;
                    _heldItemUI.UpdateSizeAndRotation();

                    int newW = item.GetCurrentWidth();
                    int newH = item.GetCurrentHeight();
                    
                    Vector2 cellSize = _gridLayout.cellSize;
                    Vector2 spacing = _gridLayout.spacing;
                    float cellTotalX = cellSize.x + spacing.x;
                    float cellTotalY = cellSize.y + spacing.y;

                    // 1. 현재 마우스가 잡고 있는 셀(칸)의 인덱스 계산
                    int cx = Mathf.FloorToInt(_dragOffset.x / cellTotalX);
                    int cy = Mathf.FloorToInt(-_dragOffset.y / cellTotalY);
                    
                    // 예외 방지 (마우스가 아이템 경계를 살짝 벗어났을 경우 가장자리로 클램프)
                    cx = Mathf.Clamp(cx, 0, oldW - 1);
                    cy = Mathf.Clamp(cy, 0, oldH - 1);

                    // 2. 90도 시계방향 회전 후 해당 셀의 새로운 인덱스 계산
                    int new_cx = oldH - 1 - cy;
                    int new_cy = cx;

                    // 3. 셀 내부에서의 정밀한 마우스 픽셀 오프셋(Sub-cell offset) 회전 처리
                    Vector2 oldCellTopLeft = new Vector2(cx * cellTotalX, -cy * cellTotalY);
                    Vector2 subCellOffset = _dragOffset - oldCellTopLeft;

                    Vector2 cellCenter = new Vector2(cellTotalX / 2f, -cellTotalY / 2f);
                    Vector2 vc = subCellOffset - cellCenter;
                    
                    // 시계방향 90도 회전 (Unity UI 좌표계: Y가 위를 향함)
                    Vector2 vc_rot = new Vector2(vc.y, -vc.x);
                    Vector2 newSubCellOffset = cellCenter + vc_rot;

                    // 4. 새로운 마우스 오프셋 적용 (마우스가 잡고 있던 '그 칸'을 계속 유지)
                    Vector2 newCellTopLeft = new Vector2(new_cx * cellTotalX, -new_cy * cellTotalY);
                    _dragOffset = newCellTopLeft + newSubCellOffset;
                }

                // 2. 마우스 따라다니기 (클릭했던 부위 정확히 유지)
                Vector2 localMouse;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_itemsContainer, UnityEngine.Input.mousePosition, null, out localMouse);
                _heldItemUI.rectTransform.localPosition = localMouse - _dragOffset;
                
                // 3. 실시간 겹침 테두리 피드백 갱신
                _heldItemUI.UpdateBorderColorFeedback();

                // 4. 좌클릭 시 내려놓기 시도
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
            // 이미 무언가를 쥐고 있다면 픽업 무시
            if (_heldItemUI == null)
            {
                _heldItemUI = clickedItem;
                _heldItemUI.SetHeldState(true);
                _ignoreFirstClick = true;
                
                // 아이템을 집어들 때 마우스가 아이템의 어느 픽셀을 잡았는지 오프셋 정확히 기억 (강제 중앙 정렬 방지)
                Vector2 localMouse;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_itemsContainer, UnityEngine.Input.mousePosition, null, out localMouse);
                _dragOffset = localMouse - (Vector2)_heldItemUI.rectTransform.localPosition;
            }
        }

        private void TryDropHeldItem()
        {
            if (_heldItemUI == null) return;

            Vector2 cellSize = _gridLayout.cellSize;
            Vector2 spacing = _gridLayout.spacing;

            Rect parentRect = _itemsContainer.rect;
            float offsetX = _heldItemUI.rectTransform.localPosition.x - parentRect.xMin;
            float offsetY = parentRect.yMax - _heldItemUI.rectTransform.localPosition.y;

            int targetGridX = Mathf.RoundToInt(offsetX / (cellSize.x + spacing.x));
            int targetGridY = Mathf.RoundToInt(offsetY / (cellSize.y + spacing.y));

            RuntimeItem item = _heldItemUI.Item;
            
            // 배치가 가능하다면 (혹은 제자리라도) AddItemAt 실행 (이때 내부적으로 이벤트가 터져서 RefreshUI가 호출되며 위치가 자동 고정됨)
            if (_inventoryManager.CanPlaceItem(item, targetGridX, targetGridY))
            {
                _heldItemUI.SetHeldState(false);
                var droppedItem = _heldItemUI;
                _heldItemUI = null;
                
                _inventoryManager.AddItemAt(item, targetGridX, targetGridY);
            }
            else
            {
                // 배치 불가 시 피드백 사운드나 이펙트 추가 가능 (계속 들고 있게 됨)
            }
        }
    }
}
