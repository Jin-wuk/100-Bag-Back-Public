using System;
using System.Collections.Generic;
using UnityEngine;
using DimensionLooter.Data.Core;

namespace DimensionLooter.Player
{
    public class InventoryManager : MonoBehaviour
    {
        public static event Action<InventoryManager> OnManagerCreated;
        public event Action OnInventoryChanged;
        
        [Header("Grid Settings")]
        [SerializeField] private int _initialGridWidth = 8;
        [SerializeField] private int _initialGridHeight = 4;

        public int GridWidth { get; private set; }
        public int GridHeight { get; private set; }

        private RuntimeItem[,] _grid;
        private List<RuntimeItem> _items = new List<RuntimeItem>();

        private void Awake()
        {
            OnManagerCreated?.Invoke(this);
            InitializeGrid(_initialGridWidth, _initialGridHeight);
        }

        private void Start()
        {
            // UI 바인딩 로직은 이제 뷰(InventoryUIController) 측에서 의존성 역전(DI)을 통해 알아서 수행합니다.
        }

        public void InitializeGrid(int width, int height)
        {
            GridWidth = width;
            GridHeight = height;
            _grid = new RuntimeItem[width, height];
            
            foreach (var item in _items)
            {
                if(item.GridX >= 0 && item.GridY >= 0)
                    PlaceItemInternal(item, item.GridX, item.GridY);
            }
            OnInventoryChanged?.Invoke();
        }

        public void UpdateBagSize(int newHeight)
        {
            if(GridHeight == newHeight) return;
            InitializeGrid(GridWidth, newHeight);
        }

        public bool CanPlaceItem(RuntimeItem item, int posX, int posY)
        {
            int w = item.GetCurrentWidth();
            int h = item.GetCurrentHeight();

            if (posX < 0 || posY < 0 || posX + w > GridWidth || posY + h > GridHeight)
                return false;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (_grid[posX + x, posY + y] != null && _grid[posX + x, posY + y] != item)
                        return false;
                }
            }
            return true;
        }

        public bool AddItemAt(RuntimeItem item, int startX, int startY)
        {
            if (!CanPlaceItem(item, startX, startY)) return false;

            // 기존 위치에서 제거 (이동인 경우) - 이벤트 방출 없음
            if (item.GridX >= 0 && item.GridY >= 0)
            {
                InternalRemoveItem(item);
            }

            // 새 위치에 할당
            item.GridX = startX;
            item.GridY = startY;

            for (int y = 0; y < item.GetCurrentHeight(); y++)
            {
                for (int x = 0; x < item.GetCurrentWidth(); x++)
                {
                    _grid[startX + x, startY + y] = item;
                }
            }

            if (!_items.Contains(item)) _items.Add(item);

            // 작업이 완전히 끝난 후 이벤트 1회 방출
            OnInventoryChanged?.Invoke();
            return true;
        }

        private void PlaceItemInternal(RuntimeItem item, int posX, int posY)
        {
            item.GridX = posX;
            item.GridY = posY;
            int w = item.GetCurrentWidth();
            int h = item.GetCurrentHeight();

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    _grid[posX + x, posY + y] = item;
                }
            }
        }

        private void InternalRemoveItem(RuntimeItem item)
        {
            if (item == null || !_items.Contains(item)) return;

            // 투명벽 버그 방지: 회전 상태 변화 등에 상관없이 무조건 그리드에서 자기 참조를 모조리 청소함
            for (int y = 0; y < GridHeight; y++)
            {
                for (int x = 0; x < GridWidth; x++)
                {
                    if (_grid[x, y] == item)
                    {
                        _grid[x, y] = null;
                    }
                }
            }
            _items.Remove(item);
            item.GridX = -1;
            item.GridY = -1;
        }

        public void RemoveItem(RuntimeItem item)
        {
            InternalRemoveItem(item);
            OnInventoryChanged?.Invoke();
        }

        public void RotateItem(RuntimeItem item)
        {
            // 이벤트 방출 없이 임시로 제거
            InternalRemoveItem(item);

            item.IsRotated = !item.IsRotated;

            if (!CanPlaceItem(item, item.GridX, item.GridY))
            {
                item.IsRotated = !item.IsRotated; // 실패 시 원래대로 복구
            }

            // 최종 위치에 추가하며 이벤트 1회 방출
            AddItemAt(item, item.GridX, item.GridY);
        }
        
        public IReadOnlyList<RuntimeItem> GetAllItems() => _items;

        public bool TryAddNextAvailableSlot(RuntimeItem item)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                for (int x = 0; x < GridWidth; x++)
                {
                    if (CanPlaceItem(item, x, y))
                    {
                        AddItemAt(item, x, y);
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
