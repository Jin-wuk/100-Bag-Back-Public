using UnityEngine;

namespace DimensionLooter.Hub
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class OutlineEffect : MonoBehaviour
    {
        private SpriteRenderer _spriteRenderer;
        private Color _originalColor;
        
        [SerializeField] private Color _highlightColor = Color.yellow;
        [SerializeField] private float _colorMultiplier = 1.2f;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _originalColor = _spriteRenderer.color;
        }

        public void SetOutline(bool active)
        {
            if (active)
            {
                // 원본 색상과 하이라이트 색상을 혼합(Tint)하여 가시성 확보
                _spriteRenderer.color = Color.Lerp(_originalColor, _highlightColor, 0.5f);
            }
            else
            {
                _spriteRenderer.color = _originalColor;
            }
        }
    }
}
