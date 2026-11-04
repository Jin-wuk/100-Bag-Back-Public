using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BagBack100.Data.Core;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 허브 진입 시 직전 던전 결과 표시. 탈출이면 가져온 전리품과 총 가치, 사망이면 잃은 아이템과 보안 파우치로 지킨 아이템을 대조한다.
    ///       제목 아래에 탈출·사망한 지역과 층을 던전 HUD와 같은 형식으로 표시한다 (기획 07 F7).
    ///       닫으면 결과 기록을 비워 다시 뜨지 않는다.
    /// Dependencies: PlayerSessionData.LastRunResult, RunResult
    /// Events: 없음
    /// </summary>
    public class RunResultUI : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Image _boxImage;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _leftHeader;
        [SerializeField] private TextMeshProUGUI _leftList;
        [SerializeField] private TextMeshProUGUI _rightHeader;
        [SerializeField] private TextMeshProUGUI _rightList;
        [SerializeField] private TextMeshProUGUI _summaryText;
        [SerializeField] private Button _closeButton;

        [Tooltip("제목 아래 지역·층 표시 형식. {0} = 지역 번호, {1} = 층 (던전 HUD 표기와 맞춤)")]
        [SerializeField] private string _stageFormat = "Stage {0}-{1}F";

        [Header("Colors")]
        [SerializeField] private Color _extractedColor = new Color(0.15f, 0.22f, 0.15f, 0.97f);
        [SerializeField] private Color _deathColor = new Color(0.28f, 0.08f, 0.08f, 0.97f);

        private readonly StringBuilder _builder = new StringBuilder();

        private void Awake()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(Close);
            if (_root != null) _root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
        }

        private void Start()
        {
            var result = PlayerSessionData.LastRunResult;
            if (result != null) Show(result);
        }

        private void Show(RunResult result)
        {
            if (_root == null) return;

            if (result.IsExtracted)
            {
                SetText(_titleText, BuildTitle("Extracted!", result));
                SetText(_leftHeader, "Brought back");
                SetText(_leftList, BuildList(result.BroughtBack));
                SetText(_rightHeader, string.Empty);
                SetText(_rightList, string.Empty);
                SetText(_summaryText, $"Total value: {result.TotalValue}G  (sell at the Blacksmith)");
                if (_boxImage != null) _boxImage.color = _extractedColor;
            }
            else
            {
                SetText(_titleText, BuildTitle("Killed in Action", result));
                SetText(_leftHeader, "Lost");
                SetText(_leftList, BuildList(result.Lost));
                SetText(_rightHeader, "Saved (Secure Pouch)");
                SetText(_rightList, BuildList(result.Saved));
                SetText(_summaryText, $"Lost value: {result.TotalValue}G");
                if (_boxImage != null) _boxImage.color = _deathColor;
            }

            _root.SetActive(true);
            // 같은 Canvas의 퀵슬롯 바 등 다른 UI보다 위에 그린다
            transform.SetAsLastSibling();
        }

        // 층 정보가 없으면(던전을 거치지 않은 기록) 제목만 쓴다
        private string BuildTitle(string title, RunResult result)
        {
            if (result.Floor <= 0) return title;
            return $"{title}\n<size=60%>{string.Format(_stageFormat, result.RegionId, result.Floor)}</size>";
        }

        private string BuildList(List<RuntimeItem> items)
        {
            if (items.Count == 0) return "(none)";

            _builder.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                var data = items[i]?.ItemData;
                if (data == null) continue;
                string name = string.IsNullOrEmpty(data.itemName) ? $"Item {data.itemID}" : data.itemName;
                _builder.Append(name);
                if (items[i].Count > 1) _builder.Append(" x").Append(items[i].Count);
                _builder.Append('\n');
            }
            return _builder.ToString();
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null) text.text = value;
        }

        public void Close()
        {
            PlayerSessionData.LastRunResult = null;
            if (_root != null) _root.SetActive(false);
        }
    }
}
