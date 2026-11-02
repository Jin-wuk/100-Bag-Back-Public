using UnityEngine;
using TMPro;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 짧은 안내 문구를 잠시 띄웠다 지우는 뷰 ("주변에 버릴 공간이 없습니다" 등).
    ///       가방이 열려 일시정지된 상태에서도 보여야 하므로 unscaled 시간으로 사라진다.
    /// Dependencies: 없음
    /// Events: 없음
    /// </summary>
    public class UIToast : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private float _showDuration = 2f;
        [Tooltip("화면 중앙에서 위쪽으로 띄울 거리 (화면 높이 비율)")]
        [Range(0f, 0.5f)] [SerializeField] private float _screenHeightOffset = 0.3f;

        private float _hideTime;
        private RectTransform _rootCanvasRect;

        private void Awake()
        {
            var canvas = GetComponentInParent<Canvas>();
            _rootCanvasRect = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
            if (_text != null) _text.gameObject.SetActive(false);
            enabled = false; // 표시 중일 때만 Update로 사라질 시간을 검사
        }

        public void Show(string message)
        {
            if (_text == null) return;
            _text.text = message;
            _text.gameObject.SetActive(true);
            _text.transform.SetAsLastSibling();

            // 패널 배치와 무관하게 화면 상단 중앙 근처에 띄운다
            if (_rootCanvasRect != null)
            {
                Vector3 center = _rootCanvasRect.position;
                float height = _rootCanvasRect.rect.height * _rootCanvasRect.lossyScale.y;
                _text.transform.position = center + Vector3.up * (height * _screenHeightOffset);
            }
            _hideTime = Time.unscaledTime + _showDuration;
            enabled = true;
        }

        private void Update()
        {
            if (_text != null && _text.gameObject.activeSelf && Time.unscaledTime >= _hideTime)
            {
                _text.gameObject.SetActive(false);
                enabled = false;
            }
        }
    }
}
