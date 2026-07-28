// File: Assets/Scripts/Gameplay/Inventory/InventoryGrid.cs
using System.Collections.Generic;
using DimensionLooter.Data;
using UnityEngine;

namespace DimensionLooter.Gameplay.Inventory
{
    /// <summary>
    /// 인벤토리의 2D 그리드 데이터를 논리적으로 관리하는 클래스입니다.
    /// [Why] UI 클래스에 2D 배열을 넣지 않고 데이터 로직만 따로 빼서 MVC 패턴을 준수합니다.
    /// </summary>
    public class InventoryGrid
    {
        public int Width { get; private set; }
        public int Height { get; private set; }

        // 그리드의 각 칸이 어떤 아이템을 참조하고 있는지 저장 (null이면 빈 칸)
        private InventoryItem[,] _grid;
        private List<InventoryItem> _items = new List<InventoryItem>();

        public InventoryGrid(int width, int height)
        {
            Width = width;
            Height = height;
            _grid = new InventoryItem[Width, Height];
        }

        /// <summary>
        /// 해당 위치에 아이템을 배치할 수 있는지 검사합니다.
        /// </summary>
        public bool CanPlaceItem(InventoryItem item, int startX, int startY)
        {
            int itemWidth = item.GetCurrentWidth();
            int itemHeight = item.GetCurrentHeight();

            // 1. 인벤토리 범위를 벗어나는지 확인
            if (startX < 0 || startY < 0 || startX + itemWidth > Width || startY + itemHeight > Height)
            {
                return false;
            }

            // 2. 다른 아이템과 겹치는지 확인
            for (int x = startX; x < startX + itemWidth; x++)
            {
                for (int y = startY; y < startY + itemHeight; y++)
                {
                    if (_grid[x, y] != null && _grid[x, y] != item) // 자기 자신은 제외
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 아이템을 그리드에 배치합니다.
        /// </summary>
        public bool PlaceItem(InventoryItem item, int startX, int startY)
        {
            if (!CanPlaceItem(item, startX, startY))
            {
                return false;
            }

            // 기존 위치에서 제거 (이동하는 경우를 대비)
            RemoveItem(item);

            item.GridX = startX;
            item.GridY = startY;
            _items.Add(item);

            // 그리드 공간 차지 처리
            int itemWidth = item.GetCurrentWidth();
            int itemHeight = item.GetCurrentHeight();
            for (int x = startX; x < startX + itemWidth; x++)
            {
                for (int y = startY; y < startY + itemHeight; y++)
                {
                    _grid[x, y] = item;
                }
            }

            return true;
        }

        /// <summary>
        /// 아이템을 그리드에서 제거합니다.
        /// </summary>
        public void RemoveItem(InventoryItem item)
        {
            if (!_items.Contains(item)) return;

            _items.Remove(item);

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (_grid[x, y] == item)
                    {
                        _grid[x, y] = null;
                    }
                }
            }
        }

        public InventoryItem GetItemAt(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return null;
            return _grid[x, y];
        }

        public IReadOnlyList<InventoryItem> GetAllItems() => _items.AsReadOnly();
    }
}
