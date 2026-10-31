using UnityEngine;
using BagBack100.Data.Core;
using BagBack100.Hub;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 대장간 판매 칸. 가방·파우치 아이템을 드롭하거나 가방에서 우클릭 "판매"를 고르면 수량 선택 + 판매가 확인창을 띄우고,
    ///       확인 시 판매를 요청한다. 대장간은 창고 자리를 쓰므로 창고 아이템은 가방으로 옮긴 뒤 판다.
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
            // 대장간에서 다른 패널로 바뀌면 열려 있던 판매 확인창도 닫아 늦은 판매 콜백이 실행되지 않게 한다
            if (_dialog != null) _dialog.Close();
            if (_tradeSystem == null) return;
            _tradeSystem.OnTradeFailed -= ShowMessage;
            _tradeSystem.OnTradeCompleted -= ShowMessage;
        }

        /// <summary>대장간 판매 칸이 화면에 열려 있을 때만 판매를 받는다 (가방 우클릭 "판매" 메뉴 표시 조건).</summary>
        public bool IsAvailable => isActiveAndEnabled && _tradeSystem != null && _dialog != null;

        private void ShowMessage(string message)
        {
            if (_toast != null) _toast.Show(message);
        }

        public bool CanAcceptDrop(RuntimeItem item, ISlotItemContainer source)
        {
            return item != null && source != null && _tradeSystem != null && _dialog != null;
        }

        public void AcceptDrop(RuntimeItem item, ISlotItemContainer source) => RequestSell(item, source);

        public void RequestSell(RuntimeItem item, ISlotItemContainer source)
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
