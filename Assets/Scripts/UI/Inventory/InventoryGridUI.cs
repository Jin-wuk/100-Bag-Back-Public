// File: Assets/Scripts/UI/Inventory/InventoryGridUI.cs
using UnityEngine;
using UnityEngine.UI;
using DimensionLooter.Gameplay.Inventory;
using DimensionLooter.Data;
using System.Collections.Generic;

namespace DimensionLooter.UI.Inventory
{
    public class InventoryGridUI : MonoBehaviour
    {
        [SerializeField] private float _cellSize = 50f;
        [SerializeField] private RectTransform _itemsContainer;
        [SerializeField] private bool _drawGridLines = true;
        
        public RectTransform ContainerRect => _itemsContainer;
        public InventoryGrid GridLogic { get; private set; }
        private RectTransform _rectTransform;
        
        private Dictionary<InventoryItem, DraggableItemUI> _itemUIMap = new Dictionary<InventoryItem, DraggableItemUI>();

        public void Init(InventoryGrid gridLogic)
        {
            GridLogic = gridLogic;
            _rectTransform = GetComponent<RectTransform>();

            // 그리드 크기에 맞춰 패널 크기 조정 (Pivot은 Top-Left: 0, 1 가정)
            RefreshSize();
        }

        public void RefreshSize()
        {
            if (GridLogic != null && _rectTransform != null)
            {
                _rectTransform.sizeDelta = new Vector2(GridLogic.Width * _cellSize, GridLogic.Height * _cellSize);
            }

            if (_drawGridLines)
            {
                // [Todo] 격자 이미지 그리기 로직 (임시로 생략하거나 테두리만 사용)
            }
        }

        public Vector2Int GetGridPosFromLocalPoint(Vector2 localPoint)
        {
            // Pivot이 (0,1)이므로 y는 음수임
            int x = Mathf.FloorToInt(localPoint.x / _cellSize);
            int y = Mathf.FloorToInt(-localPoint.y / _cellSize);
            return new Vector2Int(x, y);
        }

        public void RegisterItemUI(InventoryItem item, DraggableItemUI ui)
        {
            _itemUIMap[item] = ui;
            ui.transform.SetParent(_itemsContainer, false);
            UpdateItemPosition(item);
        }

        public void UnregisterItemUI(InventoryItem item)
        {
            if (_itemUIMap.ContainsKey(item))
            {
                _itemUIMap.Remove(item);
            }
        }

        public void UpdateItemPosition(InventoryItem item)
        {
            if (_itemUIMap.TryGetValue(item, out var ui))
            {
                RectTransform rt = ui.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(item.GridX * _cellSize, -item.GridY * _cellSize);
                ui.UpdateSizeAndRotation(_cellSize);
            }
        }
        
        public void Clear()
        {
            _itemUIMap.Clear();
            if (_itemsContainer != null)
            {
                foreach (Transform child in _itemsContainer)
                {
                    Destroy(child.gameObject);
                }
            }
        }
        
        public float CellSize => _cellSize;
    }
}
