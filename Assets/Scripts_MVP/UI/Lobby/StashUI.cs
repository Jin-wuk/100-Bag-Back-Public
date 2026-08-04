// File: Assets/Scripts/UI/Lobby/StashUI.cs
using UnityEngine;
using UnityEngine.UI;
using DimensionLooter.UI.Inventory;
using DimensionLooter.Managers;
using System.Collections.Generic;

namespace DimensionLooter.UI.Lobby
{
    public class StashUI : MonoBehaviour
    {
        [SerializeField] private InventoryGridUI _stashGridUI;
        [SerializeField] private Transform _tabButtonContainer;
        [SerializeField] private Button _tabButtonPrefab;
        [SerializeField] private Button _btnClose;
        
        private List<Button> _tabButtons = new List<Button>();

        private void Start()
        {
            if (_btnClose != null) _btnClose.onClick.AddListener(Close);
            InitTabs();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void InitTabs()
        {
            if (InventoryManager.Instance == null || _stashGridUI == null || _tabButtonPrefab == null) return;
            
            var stashGrids = InventoryManager.Instance.StashGrids;

            for (int i = 0; i < stashGrids.Count; i++)
            {
                int index = i;
                Button btn = Instantiate(_tabButtonPrefab, _tabButtonContainer);
                btn.gameObject.SetActive(true); // 프리팹 비활성화 상태를 복제본에서 활성화
                btn.GetComponentInChildren<Text>().text = $"창고 {i + 1}";
                btn.onClick.AddListener(() => OnTabClicked(index));
                _tabButtons.Add(btn);
            }

            if (stashGrids.Count > 0)
            {
                OnTabClicked(0);
            }
        }

        private void OnTabClicked(int index)
        {
            if (InventoryManager.Instance == null || _stashGridUI == null) return;

            var targetGrid = InventoryManager.Instance.StashGrids[index];
            _stashGridUI.Clear();
            _stashGridUI.Init(targetGrid);
            
            // 기존 아이템들 UI 생성 로직
            if (DualUIPanel.Instance != null && DualUIPanel.Instance.ItemPrefab != null)
            {
                foreach (var item in targetGrid.GetAllItems())
                {
                    GameObject uiObj = Instantiate(DualUIPanel.Instance.ItemPrefab);
                    DraggableItemUI itemUI = uiObj.GetComponent<DraggableItemUI>();
                    itemUI.Init(item, _stashGridUI);
                    _stashGridUI.RegisterItemUI(item, itemUI);
                }
            }
        }
    }
}
