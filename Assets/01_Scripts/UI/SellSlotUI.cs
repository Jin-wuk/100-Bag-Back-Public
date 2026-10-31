using UnityEngine;
using BagBack100.Data.Core;
using BagBack100.Hub;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 대장간 판매 칸. 가방·창고·파우치의 아이템을 드롭하면 수량 선택 + 판매가 확인창을 띄우고, 확인 시 판매를 요청한다.
    /// Dependencies: HubTradeSystem, ItemQuantityDialogUI, UIToast, IItemDropTarget
    /// Events: 없음 (구독: HubTradeSystem.OnTradeFailed, OnTradeCompleted)
    /// </summary>
    public class SellSlotUI : MonoBehaviour, IItemDropTarget
    {
        [SerializeField] private HubTradeSystem _tradeSystem;
        [SerializeField] private ItemQuantityDialogUI _dialog;
        [SerializeField] private UIToast _toast;

        private void OnEnable()
        {
            if (_tradeSystem == null) return;
            _tradeSystem.OnTradeFailed += ShowMessage;
            _tradeSystem.OnTradeCompleted += ShowMessage;
        }

        private void OnDisable()
        {
            if (_tradeSystem == null) return;
            _tradeSystem.OnTradeFailed -= ShowMessage;
            _tradeSystem.OnTradeCompleted -= ShowMessage;
        }

        private void ShowMessage(string message)
        {
            if (_toast != null) _toast.Show(message);
        }

        public bool CanAcceptDrop(RuntimeItem item, ISlotItemContainer source)
        {
            return item != null && source != null && _tradeSystem != null && _dialog != null;
        }

        public void AcceptDrop(RuntimeItem item, ISlotItemContainer source)
        {
            if (!_tradeSystem.CanSell(item))
            {
                ShowMessage("This item cannot be sold.");
                return;
            }

            int unitPrice = HubTradeSystem.GetSellPrice(item, 1);
            _dialog.Open($"Sell {item.ItemData.itemName} ({unitPrice}G each)", item.Count, "Sell",
                amount => _tradeSystem.TrySell(source, item, amount));
        }
    }
}
