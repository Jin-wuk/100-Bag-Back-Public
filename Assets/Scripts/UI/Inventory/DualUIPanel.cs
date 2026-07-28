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

        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private InventoryGridUI _mainInventoryUI;
        [SerializeField] private InventoryGridUI _secureInventoryUI; // 보안 파우치 추가
        [SerializeField] private InventoryGridUI _rewardInventoryUI;
        [SerializeField] private GameObject _itemPrefab; // DraggableItemUI 프리팹

        private InventoryGrid _rewardGrid;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
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

        public void Show(List<ItemData> drops)
        {
            if (_panelRoot != null) _panelRoot.SetActive(true);

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

            if (drops == null)
            {
                // 보상 목록이 없을 경우 (수동으로 가방을 열었을 때) 보상 UI를 숨김
                if (_rewardInventoryUI != null) _rewardInventoryUI.gameObject.SetActive(false);
                _rewardGrid = null;
            }
            else
            {
                // 파우치나 몬스터 드랍으로 열렸을 때
                if (_rewardInventoryUI != null) _rewardInventoryUI.gameObject.SetActive(true);

                // 보상 인벤토리(그리드) 생성 (임시 크기: 6x4)
                _rewardGrid = new InventoryGrid(6, 4);
                _rewardInventoryUI.Init(_rewardGrid);

                // 아이템 배치
                foreach (var itemData in drops)
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

        public void ClosePanel()
        {
            if (_panelRoot != null) _panelRoot.SetActive(false);

            if (_rewardGrid != null)
            {
                var remainingItems = _rewardGrid.GetAllItems();
                if (remainingItems.Count > 0)
                {
                    List<ItemData> leftovers = new List<ItemData>();
                    foreach(var i in remainingItems) leftovers.Add(i.Data);
                    
                    Debug.Log($"[DualUIPanel] 보상 {leftovers.Count}개가 남아 바닥에 Pouch를 생성합니다.");
                    
                    // CombatManager를 통해 현재 몬스터가 죽은 위치에 파우치 스폰
                    var cm = FindObjectOfType<CombatManager>();
                    if (cm != null)
                    {
                        cm.SpawnPouch(leftovers);
                    }
                }
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
                Show(null);
            }
        }
    }
}
