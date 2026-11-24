using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BagBack100.Dungeon
{
    /// <summary>
    /// Role: 월드맵 위 호버·클릭 대상 하나 (던전 탑, 안개, 입장 패널의 몬스터 아이콘). 호버 연출(확대·밝기)을 하고 로컬 이벤트만 낸다.
    ///       툴팁 표시와 클릭 처리는 이벤트를 받은 DungeonEntranceUI가 한다.
    /// Dependencies: 없음
    /// Events: 없음 (로컬 이벤트 Hovered, Clicked)
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class WorldMapNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Tooltip("호버 시 툴팁에 띄울 문구")]
        [SerializeField] private string _label;
        [Tooltip("false면 클릭 이벤트를 내지 않는다 (안개, 몬스터 아이콘)")]
        [SerializeField] private bool _clickable = true;
        [Tooltip("호버 시 확대 배율. 1이면 확대하지 않는다")]
        [SerializeField, Min(1f)] private float _hoverScale = 1.1f;
        [Tooltip("호버 시 강조할 이미지. 비우면 색을 바꾸지 않는다. UI 정점 색은 1을 넘지 못하므로 평소 색을 살짝 어둡게 두고 호버 때 이 색으로 밝힌다")]
        [SerializeField] private Image _highlightTarget;
        [SerializeField] private Color _highlightColor = Color.white;
        [Tooltip("0보다 크면 이 오브젝트 Image의 알파가 이 값 이상인 곳만 마우스에 반응한다 (안개처럼 투명 부분이 넓은 이미지). 텍스처 Read/Write 필요")]
        [SerializeField, Range(0f, 1f)] private float _alphaHitThreshold;

        public event Action<WorldMapNodeUI, bool> Hovered;
        public event Action<WorldMapNodeUI> Clicked;

        public string Label => _label;
        // 꺼진 채 바인딩될 수 있어 Awake 전에도 쓸 수 있게 지연 캐싱한다
        public RectTransform Rect => _rect != null ? _rect : (_rect = (RectTransform)transform);
        public object Payload { get; private set; }

        private RectTransform _rect;
        private Vector3 _baseScale = Vector3.one;
        private Color _baseColor = Color.white;
        private bool _isHovered;

        private void Awake()
        {
            _baseScale = Rect.localScale;
            if (_highlightTarget != null) _baseColor = _highlightTarget.color;
            if (_alphaHitThreshold > 0f && TryGetComponent(out Image image))
            {
                image.alphaHitTestMinimumThreshold = _alphaHitThreshold;
            }
        }

        public void Bind(string label, object payload)
        {
            _label = label;
            Payload = payload;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHover(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHover(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_clickable || eventData.button != PointerEventData.InputButton.Left) return;
            Clicked?.Invoke(this);
        }

        // 호버 중 패널이 꺼지면 OnPointerExit가 오지 않으므로, 다음에 열 때 확대·강조가 남지 않게 여기서 되돌린다
        private void OnDisable()
        {
            SetHover(false);
        }

        private void SetHover(bool hovered)
        {
            if (_isHovered == hovered) return;
            _isHovered = hovered;

            Rect.localScale = hovered ? _baseScale * _hoverScale : _baseScale;
            if (_highlightTarget != null) _highlightTarget.color = hovered ? _highlightColor : _baseColor;
            Hovered?.Invoke(this, hovered);
        }
    }
}
