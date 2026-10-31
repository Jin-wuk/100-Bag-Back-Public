using System;
using UnityEngine;
using BagBack100.Data.Core;
using BagBack100.Data.Models;
using BagBack100.Player;

namespace BagBack100.Hub
{
    /// <summary>
    /// Role: 허브 거래 처리(2계층 System). 대장간 판매(아이템 → 지갑 골드)와 잡화상 구매(골드 → 아이템).
    ///       판매가는 아이템별 지정 가격(baseValue) × 수량, 구매가는 ShopCatalogSO의 가격.
    ///       구매한 아이템은 가방에 먼저, 가방이 차면 창고에 넣고, 둘 다 차면 구매를 취소한다.
    /// Dependencies: PlayerSessionData(지갑), InventoryManager, StorageManager, ShopCatalogSO
    /// Events: OnTradeFailed(reason), OnTradeCompleted(message)
    /// </summary>
    public class HubTradeSystem : MonoBehaviour
    {
        public event Action<string> OnTradeFailed;
        public event Action<string> OnTradeCompleted;

        [SerializeField] private ShopCatalogSO _generalStoreCatalog;

        public ShopCatalogSO GeneralStoreCatalog => _generalStoreCatalog;

        private InventoryManager _inventory;
        private StorageManager _storage;

        private void Start()
        {
            // HubSystem.Awake가 매니저를 동적으로 붙인 뒤에 찾는다
            _inventory = FindObjectOfType<InventoryManager>();
            _storage = FindObjectOfType<StorageManager>();
        }

        public static int GetSellPrice(RuntimeItem item, int amount)
        {
            if (item == null || item.ItemData == null || amount <= 0) return 0;
            return Mathf.Max(0, item.ItemData.baseValue) * amount;
        }

        public bool CanSell(RuntimeItem item) => item != null && item.ItemData != null && item.ItemData.baseValue > 0;

        public bool TrySell(ISlotItemContainer source, RuntimeItem item, int amount)
        {
            if (source == null || item == null || amount <= 0) return false;
            if (!CanSell(item))
            {
                OnTradeFailed?.Invoke("This item cannot be sold.");
                return false;
            }

            amount = Mathf.Min(amount, item.Count);
            int price = GetSellPrice(item, amount);
            string name = item.ItemData.itemName;

            if (!source.RemoveAmount(item, amount)) return false;

            PlayerSessionData.AddGold(price);
            OnTradeCompleted?.Invoke($"Sold {name} x{amount} for {price}G");
            return true;
        }

        public bool TryBuy(ShopEntry entry)
        {
            if (entry.item == null) return false;

            if (PlayerSessionData.Gold < entry.price)
            {
                OnTradeFailed?.Invoke("Not enough gold.");
                return false;
            }

            var bought = new RuntimeItem(entry.item);
            bool hasRoom = (_inventory != null && _inventory.CanAddItem(bought)) || (_storage != null && _storage.CanAddItem(bought));
            if (!hasRoom)
            {
                OnTradeFailed?.Invoke("No space in bag or storage.");
                return false;
            }

            // 공간을 먼저 확인했으므로 골드 차감 후 넣기는 실패하지 않는다
            PlayerSessionData.TrySpendGold(entry.price);
            bool added = (_inventory != null && _inventory.TryAddNextAvailableSlot(bought)) ||
                         (_storage != null && _storage.TryAddNextAvailableSlot(bought));
            if (!added)
            {
                PlayerSessionData.AddGold(entry.price);
                OnTradeFailed?.Invoke("No space in bag or storage.");
                return false;
            }

            OnTradeCompleted?.Invoke($"Bought {entry.item.itemName} for {entry.price}G");
            return true;
        }
    }
}
