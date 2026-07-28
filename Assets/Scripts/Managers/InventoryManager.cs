// File: Assets/Scripts/Managers/InventoryManager.cs
using UnityEngine;
using DimensionLooter.Gameplay.Inventory;

namespace DimensionLooter.Managers
{
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [Header("Main Inventory Settings")]
        [SerializeField] private int _width = 6;
        [SerializeField] private int _height = 6;

        public InventoryGrid MainGrid { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                MainGrid = new InventoryGrid(_width, _height);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 빈 공간을 찾아 자동으로 아이템을 배치합니다. (주로 치트나 퀘스트 보상용)
        /// </summary>
        public bool TryAddAuto(DimensionLooter.Data.ItemData itemData)
        {
            var invItem = new InventoryItem(itemData);
            for (int y = 0; y < MainGrid.Height; y++)
            {
                for (int x = 0; x < MainGrid.Width; x++)
                {
                    if (MainGrid.PlaceItem(invItem, x, y))
                    {
                        // UI 갱신을 위해 DualUIPanel이나 MainInventoryUI 측에 알려야 함
                        // 임시로 자동 배치 시에는 UI가 열려있다면 갱신하도록 처리 가능
                        // [Todo] Event 기반 UI 갱신 로직 (현재는 드래그 앤 드롭 전용이므로 생략)
                        Debug.Log($"[InventoryManager] {itemData.Name} 아이템을 자동 배치했습니다. ({x}, {y})");
                        return true;
                    }
                }
            }
            Debug.LogWarning($"[InventoryManager] 가방이 꽉 찼습니다! {itemData.Name} 배치 실패.");
            return false;
        }
    }
}
