// File: Assets/Scripts/Managers/InventoryManager.cs
using System;
using UnityEngine;
using DimensionLooter.Data;
using DimensionLooter.Gameplay.Inventory;

namespace DimensionLooter.Managers
{
    /// <summary>
    /// 가방 테트리스 시스템을 총괄하는 매니저입니다.
    /// [Why] 싱글톤으로 유지되어 어느 곳에서든 아이템 획득 시 인벤토리에 추가할 수 있도록 합니다.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [Header("Inventory Settings")]
        [Tooltip("인벤토리 가로 칸 수")]
        [SerializeField] private int _gridWidth = 8;
        
        [Tooltip("인벤토리 세로 칸 수")]
        [SerializeField] private int _gridHeight = 6;

        public InventoryGrid Grid { get; private set; }

        // [Observer] 인벤토리 상태가 변할 때 UI에서 화면을 갱신하도록 이벤트를 날립니다.
        public event Action OnInventoryChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Init();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Init()
        {
            Grid = new InventoryGrid(_gridWidth, _gridHeight);
        }

        /// <summary>
        /// 새로운 아이템을 인벤토리의 빈 공간을 찾아 자동으로 추가합니다.
        /// </summary>
        public bool TryAddAuto(ItemData data)
        {
            InventoryItem newItem = new InventoryItem(data);

            // 좌상단부터 순차적으로 빈 공간 탐색 (테스트용 단순 알고리즘)
            for (int y = 0; y < _gridHeight; y++)
            {
                for (int x = 0; x < _gridWidth; x++)
                {
                    if (Grid.PlaceItem(newItem, x, y))
                    {
                        Debug.Log($"[InventoryManager] {data.Name} 획득! ({x}, {y})에 배치됨.");
                        // [Safe Invocation]
                        OnInventoryChanged?.Invoke();
                        return true;
                    }
                }
            }

            Debug.LogWarning($"[InventoryManager] 가방이 꽉 차서 {data.Name}을(를) 획득할 수 없습니다!", this);
            return false;
        }

        /// <summary>
        /// 유저가 직접 드래그하여 특정 위치에 아이템을 배치하려 할 때 호출됩니다.
        /// </summary>
        public bool TryPlaceManual(InventoryItem item, int targetX, int targetY)
        {
            if (Grid.PlaceItem(item, targetX, targetY))
            {
                OnInventoryChanged?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 인벤토리에서 특정 아이템을 제거(버리기/사용)합니다.
        /// </summary>
        public void RemoveItem(InventoryItem item)
        {
            Grid.RemoveItem(item);
            OnInventoryChanged?.Invoke();
        }
    }
}
