// File: Assets/Scripts/UI/Lobby/SellZoneUI.cs
using UnityEngine;
using DimensionLooter.Gameplay.Inventory;
using DimensionLooter.Managers;
using DimensionLooter.UI.Inventory;

namespace DimensionLooter.UI.Lobby
{
    public class SellZoneUI : MonoBehaviour
    {
        public static SellZoneUI Instance { get; private set; }
        public static bool IsSellModeActive => Instance != null && Instance.gameObject.activeInHierarchy;

        [SerializeField] private UnityEngine.UI.Button _btnClose;

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void Start()
        {
            if (_btnClose != null) _btnClose.onClick.AddListener(Close);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public void SellItem(InventoryItem item, DraggableItemUI itemUI)
        {
            if (item == null || item.Data == null) return;
            
            // 실 판매가 적용, 없을 경우 임시 공식 적용
            int price = item.Data.Price;
            if (price <= 0) 
            {
                price = (item.Data.Width * item.Data.Height) * 50; 
            } 

            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.AddGold(price);
                Debug.Log($"[Blacksmith] {item.Data.Name} 판매 완료. (+{price} G)");
            }

            // 인벤토리 그리드에서 데이터 제거 후 시각적 요소 파괴
            if (itemUI.CurrentGridUI != null && itemUI.CurrentGridUI.GridLogic != null)
            {
                itemUI.CurrentGridUI.GridLogic.RemoveItem(item);
                itemUI.CurrentGridUI.UnregisterItemUI(item);
            }
            
            Destroy(itemUI.gameObject);
        }
    }
}
