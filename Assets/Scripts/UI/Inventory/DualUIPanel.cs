// File: Assets/Scripts/UI/Inventory/DualUIPanel.cs
using UnityEngine;
using System.Collections.Generic;
using DimensionLooter.Gameplay.Inventory;
using DimensionLooter.Data;
using DimensionLooter.Managers;

namespace DimensionLooter.UI.Inventory
{
    public class DualUIPanel : MonoBehaviour
    {
        public static DualUIPanel Instance { get; private set; }

        public enum TabType { INVENTORY, SKILLS }

        [Header("Root")]
        [SerializeField] private GameObject _panelRoot;

        [Header("Tab Contents")]
        [SerializeField] private GameObject _gearPanel;
        [SerializeField] private GameObject _bagPanel; // _mainInventoryUI의 부모 또는 오브젝트 자신
        [SerializeField] private GameObject _skillsPanel;

        [Header("Grids")]
        [SerializeField] private InventoryGridUI _mainInventoryUI;
        [SerializeField] private InventoryGridUI _secureInventoryUI;
        [SerializeField] private InventoryGridUI _rewardInventoryUI;
        
        [Header("Prefabs")]
        [SerializeField] private GameObject _itemPrefab;

        private InventoryGrid _rewardGrid;
        private PouchInteractable _activePouch;
        public PouchInteractable ActivePouch => _activePouch;
        private GameObject _raycastBlocker;

        public bool IsPanelOpen => _panelRoot != null && _panelRoot.activeSelf;
        public InventoryGridUI RewardInventoryUI => _rewardInventoryUI;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                CreateRaycastBlocker();
                ArrangeInventoryLayout();
                Hide();
                if (InventoryManager.Instance != null)
                {
                    InventoryManager.Instance.OnGridResized += HandleGridResized;
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void ArrangeInventoryLayout()
        {
            if (_gearPanel == null || _bagPanel == null) return;
            
            // 두 패널이 이미 동일한 컨테이너에 배치되어 있을 수 있으므로 부모를 찾음
            Transform parent = _gearPanel.transform.parent;
            
            // 컨테이너 동적 생성
            GameObject container = new GameObject("InventoryVerticalContainer");
            container.transform.SetParent(parent, false);
            container.transform.SetSiblingIndex(_gearPanel.transform.GetSiblingIndex());

            var rt = container.AddComponent<RectTransform>();
            // 부모 앵커 꽉 채우기 (여백 조정 필요 시 offset 수정)
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(50, 50); // 여백 추가
            rt.offsetMax = new Vector2(-50, -50);

            var vlg = container.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 30f; // 장비창과 가방 사이 여백
            vlg.childControlHeight = false;
            vlg.childControlWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = false;

            // 자식으로 편입
            _gearPanel.transform.SetParent(container.transform, false);
            _bagPanel.transform.SetParent(container.transform, false);

            ArrangeGearSlots();
        }

        private void ArrangeGearSlots()
        {
            if (_gearPanel == null) return;
            
            RectTransform gearRt = _gearPanel.GetComponent<RectTransform>();
            if (gearRt != null)
            {
                gearRt.sizeDelta = new Vector2(330, 230); // 2줄 레이아웃 높이에 맞춤
            }

            GearSlotUI[] slots = _gearPanel.GetComponentsInChildren<GearSlotUI>(true);
            foreach (var slot in slots)
            {
                RectTransform rt = slot.GetComponent<RectTransform>();
                if (rt == null) continue;

                rt.sizeDelta = new Vector2(100, 100);
                
                string n = slot.gameObject.name;
                if (n.Contains("Weapon"))
                {
                    rt.anchoredPosition = new Vector2(-110, -10);
                    rt.sizeDelta = new Vector2(100, 210); // 세로 2줄 크기
                }
                else if (n.Contains("Armor"))
                {
                    rt.anchoredPosition = new Vector2(0, -10);
                }
                else if (n.Contains("Helmet"))
                {
                    rt.anchoredPosition = new Vector2(110, -10);
                }
                else if (n.Contains("Accessory"))
                {
                    rt.anchoredPosition = new Vector2(0, -120);
                }
                else if (n.Contains("Bag"))
                {
                    rt.anchoredPosition = new Vector2(110, -120);
                }
            }
        }

        private void CreateRaycastBlocker()
        {
            _raycastBlocker = new GameObject("RaycastBlocker");
            _raycastBlocker.transform.SetParent(_panelRoot.transform, false);
            _raycastBlocker.transform.SetSiblingIndex(0); // 가장 뒤로
            
            var rt = _raycastBlocker.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            var img = _raycastBlocker.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0, 0, 0, 0.5f); // 어둡게 처리하여 모달 느낌 부여
            img.raycastTarget = true;
        }

        public void Hide()
        {
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnGridResized -= HandleGridResized;
            }
        }

        private void HandleGridResized()
        {
            if (_mainInventoryUI != null) _mainInventoryUI.RefreshSize();
            if (_secureInventoryUI != null) _secureInventoryUI.RefreshSize();
        }

        public void Show(PouchInteractable pouch = null)
        {
            if (_panelRoot != null) _panelRoot.SetActive(true);
            
            SwitchToInventory();

            // 메인 인벤토리 연동
            if (_mainInventoryUI.GridLogic == null)
            {
                _mainInventoryUI.Init(InventoryManager.Instance.MainGrid);
            }

            // 보안 파우치 연동
            if (_secureInventoryUI != null && _secureInventoryUI.GridLogic == null)
            {
                _secureInventoryUI.Init(InventoryManager.Instance.SecureGrid);
            }

            _activePouch = pouch;

            if (_activePouch == null || _activePouch.StoredItems.Count == 0)
            {
                // 보상 목록이 없을 경우 (수동으로 가방을 열었을 때) 보상 UI를 숨김
                if (_rewardInventoryUI != null) _rewardInventoryUI.gameObject.SetActive(false);
                _rewardGrid = null;
            }
            else
            {
                // 파우치나 몬스터 드랍으로 열렸을 때
                if (_rewardInventoryUI != null) _rewardInventoryUI.gameObject.SetActive(true);

                if (_rewardGrid == null)
                {
                    // 넉넉한 가로 폭의 그리드 생성 (가로 15, 세로 6)
                    _rewardGrid = new InventoryGrid(15, 6);
                    _rewardInventoryUI.Init(_rewardGrid);
                }

                // 기존 UI 초기화 후 재배치
                _rewardInventoryUI.Clear();
                _rewardGrid = new InventoryGrid(15, 6);
                _rewardInventoryUI.Init(_rewardGrid);

                // 아이템 배치
                foreach (var itemData in _activePouch.StoredItems)
                {
                    var invItem = new InventoryItem(itemData);
                    bool placed = false;
                    for (int y = 0; y < _rewardGrid.Height; y++)
                    {
                        for (int x = 0; x < _rewardGrid.Width; x++)
                        {
                            if (_rewardGrid.PlaceItem(invItem, x, y))
                            {
                                placed = true;
                                GameObject uiObj = Instantiate(_itemPrefab);
                                DraggableItemUI itemUI = uiObj.GetComponent<DraggableItemUI>();
                                itemUI.Init(invItem, _rewardInventoryUI);
                                _rewardInventoryUI.RegisterItemUI(invItem, itemUI);
                                break;
                            }
                        }
                        if (placed) break;
                    }
                }
            }
        }

        public void SpawnMainInventoryItemUI(InventoryItem invItem)
        {
            if (_mainInventoryUI != null && _itemPrefab != null)
            {
                GameObject uiObj = Instantiate(_itemPrefab);
                DraggableItemUI itemUI = uiObj.GetComponent<DraggableItemUI>();
                itemUI.Init(invItem, _mainInventoryUI);
                _mainInventoryUI.RegisterItemUI(invItem, itemUI);
            }
        }

        public void ClosePanel()
        {
            if (_panelRoot != null) _panelRoot.SetActive(false);

            if (_activePouch != null)
            {
                if (_activePouch.StoredItems.Count == 0)
                {
                    Debug.Log($"[DualUIPanel] 파우치({_activePouch.Id})가 비어있으므로 파괴합니다.");
                    if (DimensionLooter.Managers.LootManager.Instance != null)
                    {
                        DimensionLooter.Managers.LootManager.Instance.DeregisterPouch(_activePouch.Id);
                    }
                    Destroy(_activePouch.gameObject);
                }
                _activePouch = null;
            }

            // 보상 UI 하위 아이템 및 캐싱된 객체 정리
            if (_rewardInventoryUI != null)
            {
                _rewardInventoryUI.Clear();
            }
            _rewardGrid = null;
        }

        public void ToggleInventory()
        {
            if (_panelRoot == null) return;
            
            if (_panelRoot.activeSelf)
            {
                ClosePanel();
            }
            else
            {
                // 근접 루팅 (Proximity Looting)
                PouchInteractable targetPouch = null;
                var player = FindObjectOfType<DimensionLooter.Gameplay.PlayerController>();
                if (player != null)
                {
                    Physics2D.SyncTransforms();
                    Collider2D[] colliders = Physics2D.OverlapCircleAll(player.transform.position, 1.5f);
                    float minDistance = float.MaxValue;
                    foreach (var col in colliders)
                    {
                        var pouch = col.GetComponent<DimensionLooter.Gameplay.Inventory.PouchInteractable>();
                        if (pouch != null && pouch.StoredItems.Count > 0)
                        {
                            float dist = Vector2.Distance(player.transform.position, pouch.transform.position);
                            if (dist < minDistance)
                            {
                                minDistance = dist;
                                targetPouch = pouch;
                            }
                        }
                    }
                }
                
                Show(targetPouch);
            }
        }
        public void SwitchTab(TabType tab)
        {
            if (_gearPanel != null) _gearPanel.SetActive(tab == TabType.INVENTORY);
            if (_bagPanel != null) _bagPanel.SetActive(tab == TabType.INVENTORY);
            if (_skillsPanel != null) _skillsPanel.SetActive(tab == TabType.SKILLS);
            
            if (_secureInventoryUI != null) _secureInventoryUI.gameObject.SetActive(tab == TabType.INVENTORY);
            TrashCanUI trashCan = GetComponentInChildren<TrashCanUI>(true);
            if (trashCan != null) trashCan.gameObject.SetActive(tab == TabType.INVENTORY);
        }

        public void SwitchToInventory() => SwitchTab(TabType.INVENTORY);
        public void SwitchToSkills() => SwitchTab(TabType.SKILLS);

        public DimensionLooter.Gameplay.Inventory.InventoryItem TryAddRewardAuto(DimensionLooter.Data.ItemData itemData)
        {
            if (_rewardGrid == null || !IsPanelOpen || _activePouch == null) return null;

            var invItem = new InventoryItem(itemData);
            for (int y = 0; y < _rewardGrid.Height; y++)
            {
                for (int x = 0; x < _rewardGrid.Width; x++)
                {
                    if (_rewardGrid.PlaceItem(invItem, x, y))
                    {
                        if (_itemPrefab != null && _rewardInventoryUI != null)
                        {
                            GameObject uiObj = Instantiate(_itemPrefab);
                            DraggableItemUI itemUI = uiObj.GetComponent<DraggableItemUI>();
                            itemUI.Init(invItem, _rewardInventoryUI);
                            _rewardInventoryUI.RegisterItemUI(invItem, itemUI);
                        }
                        
                        // 백엔드 파우치 데이터 동기화
                        _activePouch.AppendItems(new System.Collections.Generic.List<DimensionLooter.Data.ItemData> { itemData });
                        
                        return invItem;
                    }
                }
            }

            if (itemData.Width != itemData.Height)
            {
                invItem.IsRotated = !invItem.IsRotated;
                for (int y = 0; y < _rewardGrid.Height; y++)
                {
                    for (int x = 0; x < _rewardGrid.Width; x++)
                    {
                        if (_rewardGrid.PlaceItem(invItem, x, y))
                        {
                            if (_itemPrefab != null && _rewardInventoryUI != null)
                            {
                                GameObject uiObj = Instantiate(_itemPrefab);
                                DraggableItemUI itemUI = uiObj.GetComponent<DraggableItemUI>();
                                itemUI.Init(invItem, _rewardInventoryUI);
                                _rewardInventoryUI.RegisterItemUI(invItem, itemUI);
                            }
                            return invItem;
                        }
                    }
                }
                invItem.IsRotated = !invItem.IsRotated;
            }

            return null;
        }
    }
}
