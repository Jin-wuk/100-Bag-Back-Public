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
    }
}
