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

        private void Awake()
        {
            if (_text != null) _text.gameObject.SetActive(false);
        }

        public void Show(string message)
        {
            if (_text == null) return;
            _text.text = message;
            _text.gameObject.SetActive(true);
            _text.transform.SetAsLastSibling();

            // 패널 배치와 무관하게 화면 상단 중앙 근처에 띄운다
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var canvasRect = (RectTransform)canvas.rootCanvas.transform;
                Vector3 center = canvasRect.position;
                float height = canvasRect.rect.height * canvasRect.lossyScale.y;
                _text.transform.position = center + Vector3.up * (height * _screenHeightOffset);
            }
            _hideTime = Time.unscaledTime + _showDuration;
        }

        private void Update()
        {
            if (_text != null && _text.gameObject.activeSelf && Time.unscaledTime >= _hideTime)
            {
                _text.gameObject.SetActive(false);
            }
        }
    }
}
