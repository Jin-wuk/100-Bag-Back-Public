// File: Assets/Scripts/Gameplay/Inventory/InventoryGrid.cs
using System.Collections.Generic;
using UnityEngine;

namespace DimensionLooter.Gameplay.Inventory
{
    public class InventoryGrid
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        
        private InventoryItem[,] _grid;
        private List<InventoryItem> _items = new List<InventoryItem>();

        public InventoryGrid(int width, int height)
        {
            Width = width;
            Height = height;
            _grid = new InventoryItem[width, height];
        }

        public bool CanPlaceItem(InventoryItem item, int startX, int startY)
        {
            int w = item.GetWidth();
            int h = item.GetHeight();

            // 바운더리 체크
            if (startX < 0 || startY < 0 || startX + w > Width || startY + h > Height)
                return false;

            // 충돌 체크
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (_grid[startX + x, startY + y] != null && _grid[startX + x, startY + y] != item)
                        return false;
                }
            }
            return true;
        }

        public bool PlaceItem(InventoryItem item, int startX, int startY)
        {
            if (!CanPlaceItem(item, startX, startY)) return false;

            // 이미 배치되어 있었다면 기존 자리에서 제거
            if (_items.Contains(item)) RemoveItem(item);

            item.GridX = startX;
            item.GridY = startY;

            int w = item.GetWidth();
            int h = item.GetHeight();

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    _grid[startX + x, startY + y] = item;
                }
            }

            _items.Add(item);
            return true;
        }

        public void RemoveItem(InventoryItem item)
        {
            if (!_items.Contains(item)) return;

            int w = item.GetWidth();
            int h = item.GetHeight();

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    _grid[item.GridX + x, item.GridY + y] = null;
                }
            }

            _items.Remove(item);
        }

        public InventoryItem GetItemAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return null;
            return _grid[x, y];
        }

        public IReadOnlyList<InventoryItem> GetAllItems() => _items.AsReadOnly();

        /// <summary>
        /// 그리드 크기를 변경하고, 새 바운더리를 벗어나는 아이템들을 리스트로 반환(제거)합니다.
        /// </summary>
        public List<InventoryItem> Resize(int newWidth, int newHeight)
        {
            List<InventoryItem> overflows = new List<InventoryItem>();
            
            // 1. 기존 아이템들을 모두 임시 리스트에 복사
            List<InventoryItem> allItems = new List<InventoryItem>(_items);
            
            // 2. 그리드 초기화
            Width = newWidth;
            Height = newHeight;
            _grid = new InventoryItem[Width, Height];
            _items.Clear();
            
            // 3. 아이템 재배치 시도
            foreach (var item in allItems)
            {
                if (CanPlaceItem(item, item.GridX, item.GridY))
                {
                    PlaceItem(item, item.GridX, item.GridY);
                }
                else
                {
                    // 바운더리를 벗어나는 아이템은 오버플로우로 처리
                    overflows.Add(item);
                }
            }
            
            return overflows;
        }

        public void Clear()
        {
            _items.Clear();
            _grid = new InventoryItem[Width, Height];
        }
    }
}
