using UnityEngine;

namespace DimensionLooter.UI
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class MovementPreview : MonoBehaviour
    {
        [Header("Preview Settings")]
        [SerializeField] private Color _movableColor = new Color(1f, 1f, 1f, 0.5f);
        [SerializeField] private Color _unmovableColor = new Color(1f, 0f, 0f, 0.5f);

        private SpriteRenderer _spriteRenderer;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            Hide(); // 기본적으로 숨김
        }

        public void ShowPreview(Vector3 position, bool isMovable)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            transform.position = position;
            _spriteRenderer.color = isMovable ? _movableColor : _unmovableColor;
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
