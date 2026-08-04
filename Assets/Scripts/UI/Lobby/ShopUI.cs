// File: Assets/Scripts/UI/Lobby/ShopUI.cs
using UnityEngine;
using DimensionLooter.Managers;
using DimensionLooter.Data;

namespace DimensionLooter.UI.Lobby
{
    public class ShopUI : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button _btnClose;

        private void Start()
        {
            if (_btnClose != null) _btnClose.onClick.AddListener(Close);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public void BuyItem(string itemId)
        {
            if (DataManager.Instance == null || !DataManager.Instance.ItemDict.TryGetValue(itemId, out ItemData data))
            {
                Debug.LogWarning($"[ShopUI] 아이템 '{itemId}' 데이터를 찾을 수 없습니다.");
                return;
            }

            int price = data.Price; 

            if (EconomyManager.Instance != null && EconomyManager.Instance.CurrentGold >= price)
            {
                var newInst = new ItemInstance(data);
                
                if (InventoryManager.Instance != null)
                {
                    var added = InventoryManager.Instance.TryAddAuto(newInst);
                    if (added != null)
                    {
                        EconomyManager.Instance.SpendGold(price);
                        
                        // 백엔드에는 추가되었으나 UI에 렌더링되지 않는 문제 해결
                        if (DimensionLooter.UI.Inventory.DualUIPanel.Instance != null)
                        {
                            DimensionLooter.UI.Inventory.DualUIPanel.Instance.SpawnMainInventoryItemUI(added);
                        }
                        
                        Debug.Log($"[ShopUI] {data.Name} 구매 완료! (-{price} G)");
                    }
                    else
                    {
                        Debug.LogWarning("[ShopUI] 가방(메인) 공간이 부족하여 구매할 수 없습니다.");
                    }
                }
            }
            else
            {
                Debug.LogWarning("[ShopUI] 골드가 부족합니다.");
            }
        }
    }
}
