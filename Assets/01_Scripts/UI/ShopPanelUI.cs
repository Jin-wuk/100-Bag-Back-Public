using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BagBack100.Data.Models;
using BagBack100.Hub;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 잡화상 판매 목록 뷰. 카탈로그 항목마다 아이콘·이름·가격·구매 버튼 행을 만들고, 구매는 HubTradeSystem에 요청한다.
    /// Dependencies: HubTradeSystem, ShopCatalogSO, UIToast
    /// Events: 없음 (구독: HubTradeSystem.OnTradeFailed, OnTradeCompleted)
    /// </summary>
    public class ShopPanelUI : MonoBehaviour
    {
        [SerializeField] private HubTradeSystem _tradeSystem;
        [SerializeField] private RectTransform _rowContainer;
        [SerializeField] private GameObject _rowPrefab;
        [SerializeField] private UIToast _toast;

        private readonly List<GameObject> _rows = new List<GameObject>();

        private void OnEnable()
        {
            if (_tradeSystem != null)
            {
                _tradeSystem.OnTradeFailed += ShowMessage;
                _tradeSystem.OnTradeCompleted += ShowMessage;
            }
            BuildRows();
        }

        private void OnDestroy() => ClearRows();

        private void ClearRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] == null) continue;
                var button = _rows[i].transform.Find("BuyButton")?.GetComponent<Button>();
                if (button != null) button.onClick.RemoveAllListeners();
                Destroy(_rows[i]);
            }
            _rows.Clear();
        }

        private void OnDisable()
        {
            if (_tradeSystem != null)
            {
                _tradeSystem.OnTradeFailed -= ShowMessage;
                _tradeSystem.OnTradeCompleted -= ShowMessage;
            }
        }

        private void ShowMessage(string message)
        {
            if (_toast != null) _toast.Show(message);
        }

        // 카탈로그는 열 때마다 다시 그려 인스펙터에서 바꾼 가격이 바로 반영되게 한다 (열 때만 실행되므로 할당 부담 없음)
        private void BuildRows()
        {
            ClearRows();

            var catalog = _tradeSystem != null ? _tradeSystem.GeneralStoreCatalog : null;
            if (catalog == null || _rowContainer == null || _rowPrefab == null) return;

            for (int i = 0; i < catalog.entries.Count; i++)
            {
                var entry = catalog.entries[i];
                if (entry.item == null) continue;

                var row = Instantiate(_rowPrefab, _rowContainer);
                row.SetActive(true);
                _rows.Add(row);

                var icon = row.transform.Find("Icon")?.GetComponent<Image>();
                if (icon != null) icon.sprite = entry.item.itemIcon;
                var nameText = row.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
                if (nameText != null) nameText.text = string.IsNullOrEmpty(entry.item.itemName) ? $"Item {entry.item.itemID}" : entry.item.itemName;
                var priceText = row.transform.Find("Price")?.GetComponent<TextMeshProUGUI>();
                if (priceText != null) priceText.text = $"{entry.price}G";

                var button = row.transform.Find("BuyButton")?.GetComponent<Button>();
                if (button != null)
                {
                    var captured = entry;
                    button.onClick.AddListener(() => _tradeSystem.TryBuy(captured));
                }
            }
        }
    }
}
