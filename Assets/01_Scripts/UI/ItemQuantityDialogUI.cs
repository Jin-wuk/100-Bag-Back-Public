using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 수량 선택 + 확인 대화상자. 버리기·삭제·판매처럼 "몇 개를 처리할지" 고르는 공용 뷰.
    ///       최대 수량이 1이면 슬라이더를 숨겨 단순 확인창으로 동작한다. 결과는 콜백으로만 돌려주고 데이터는 직접 바꾸지 않는다.
    /// Dependencies: 없음
    /// Events: 없음 (확인 시 Open에 넘긴 콜백 호출)
    /// </summary>
    public class ItemQuantityDialogUI : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Slider _amountSlider;
        [SerializeField] private TextMeshProUGUI _amountText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private TextMeshProUGUI _confirmLabel;
        [SerializeField] private Button _cancelButton;

        private Action<int> _onConfirm;
        private int _maxAmount;

        public bool IsOpen => _root != null && _root.activeSelf;

        private void Awake()
        {
            if (_confirmButton != null) _confirmButton.onClick.AddListener(Confirm);
            if (_cancelButton != null) _cancelButton.onClick.AddListener(Close);
            if (_amountSlider != null) _amountSlider.onValueChanged.AddListener(HandleSliderChanged);
            Close();
        }

        private void OnDestroy()
        {
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(Confirm);
            if (_cancelButton != null) _cancelButton.onClick.RemoveListener(Close);
            if (_amountSlider != null) _amountSlider.onValueChanged.RemoveListener(HandleSliderChanged);
        }

        private void OnDisable()
        {
            // 가방 패널이 닫히면 열려 있던 대화상자도 결정 없이 닫는다
            Close();
        }

        public void Open(string title, int maxAmount, string confirmLabel, Action<int> onConfirm)
        {
            if (maxAmount <= 0 || onConfirm == null) return;

            _onConfirm = onConfirm;
            _maxAmount = maxAmount;

            if (_titleText != null) _titleText.text = title;
            if (_confirmLabel != null) _confirmLabel.text = confirmLabel;

            if (_amountSlider != null)
            {
                _amountSlider.gameObject.SetActive(maxAmount > 1);
                _amountSlider.minValue = 1;
                _amountSlider.maxValue = maxAmount;
                _amountSlider.wholeNumbers = true;
                _amountSlider.SetValueWithoutNotify(maxAmount);
            }
            UpdateAmountText(maxAmount);

            if (_root != null)
            {
                _root.SetActive(true);
                _root.transform.SetAsLastSibling();
                // 패널 배치와 무관하게 항상 화면 중앙에 띄운다
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null) _root.transform.position = canvas.rootCanvas.transform.position;
            }
        }

        public void Close()
        {
            _onConfirm = null;
            if (_root != null) _root.SetActive(false);
        }

        private void Confirm()
        {
            var callback = _onConfirm;
            int amount = _amountSlider != null && _maxAmount > 1 ? Mathf.RoundToInt(_amountSlider.value) : _maxAmount;
            Close();
            callback?.Invoke(Mathf.Clamp(amount, 1, Mathf.Max(1, _maxAmount)));
        }

        private void HandleSliderChanged(float value) => UpdateAmountText(Mathf.RoundToInt(value));

        private void UpdateAmountText(int amount)
        {
            if (_amountText != null) _amountText.text = $"{amount} / {_maxAmount}";
        }
    }
}
