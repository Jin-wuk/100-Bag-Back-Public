using System.Collections.Generic;
using UnityEngine;

namespace BagBack100.UI
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class MovementPreview : MonoBehaviour
    {
        [Header("Preview Settings")]
        [SerializeField] private Color _movableColor = new Color(1f, 1f, 1f, 0.5f);
        [SerializeField] private Color _unmovableColor = new Color(1f, 0f, 0f, 0.5f);
        [SerializeField] private Color _chargingLockedColor = new Color(1f, 0.85f, 0.2f, 0.65f);
        [SerializeField] private Color _chargingProgressColor = new Color(1f, 0.9f, 0.4f, 0.35f);
        [SerializeField] private Color _chargeReadyColor = new Color(1f, 0.55f, 0.1f, 0.8f);

        private SpriteRenderer _spriteRenderer;
        private Vector3 _baseLocalScale = Vector3.one;
        private readonly List<SpriteRenderer> _lineRenderers = new List<SpriteRenderer>();

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _baseLocalScale = transform.localScale;
            _lineRenderers.Add(_spriteRenderer);
            Hide(); // 기본적으로 숨김
        }

        public void ShowPreview(Vector3 position, bool isMovable)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            // 추가 차징 타일 렌더러 비활성화
            for (int i = 1; i < _lineRenderers.Count; i++)
            {
                if (_lineRenderers[i] != null && _lineRenderers[i].gameObject.activeSelf)
                {
                    _lineRenderers[i].gameObject.SetActive(false);
                }
            }

            transform.position = position;
            transform.localScale = _baseLocalScale;
            _spriteRenderer.color = isMovable ? _movableColor : _unmovableColor;
        }

        /// <summary>
        /// 우클릭 차징 시간에 맞춰 기본 1칸에서 N칸까지 직선 타일 프리뷰를 순차적으로 확장 표시합니다.
        /// </summary>
        public void ShowChargeLinePreview(Grid grid, Vector3Int playerCell, Vector2Int stepDir, int maxLineLength, float chargeNormalized)
        {
            if (grid == null || maxLineLength <= 0)
            {
                Hide();
                return;
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsurePoolSize(maxLineLength);

            float clampedCharge = Mathf.Clamp01(chargeNormalized);
            bool isFullyCharged = clampedCharge >= 1f;

            // 기본 1칸에서 시작하여 차징 진행도(0~1)에 비례해 maxLineLength(N칸)까지 확장
            float exactTiles = 1f + clampedCharge * Mathf.Max(0, maxLineLength - 1);
            int visibleTileCount = isFullyCharged
                ? maxLineLength
                : Mathf.Min(maxLineLength, Mathf.FloorToInt(exactTiles) + 1);

            float pulse = Mathf.PingPong(Time.time * 14f, 1f);
            Color readyPulseColor = Color.Lerp(_chargingLockedColor, _chargeReadyColor, pulse);

            for (int i = 0; i < _lineRenderers.Count; i++)
            {
                SpriteRenderer sr = _lineRenderers[i];
                if (sr == null) continue;

                if (i >= visibleTileCount)
                {
                    if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false);
                    continue;
                }

                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);

                int tileDist = i + 1; // 1번째 칸 ~ N번째 칸
                Vector3Int targetCell = new Vector3Int(playerCell.x + stepDir.x * tileDist, playerCell.y + stepDir.y * tileDist, 0);
                Vector3 worldPos = grid.GetCellCenterWorld(targetCell);

                if (i == 0)
                {
                    transform.position = worldPos;
                    transform.localScale = _baseLocalScale;
                }
                else
                {
                    sr.transform.position = worldPos;
                }

                if (isFullyCharged)
                {
                    sr.color = readyPulseColor;
                    if (i > 0) sr.transform.localScale = Vector3.one;
                }
                else if (tileDist <= Mathf.FloorToInt(exactTiles))
                {
                    // 이미 차징이 완료된 칸 (기본 1칸 포함)
                    sr.color = _chargingLockedColor;
                    if (i > 0) sr.transform.localScale = Vector3.one;
                }
                else
                {
                    // 현재 차징 중인 칸 (0 -> 1 비율로 크기와 투명도가 차오름)
                    float partial = Mathf.Clamp01(exactTiles - Mathf.Floor(exactTiles));
                    Color c = Color.Lerp(_chargingProgressColor, _chargingLockedColor, partial);
                    sr.color = c;
                    if (i > 0)
                    {
                        float scaleFactor = Mathf.Lerp(0.35f, 1f, partial);
                        sr.transform.localScale = new Vector3(scaleFactor, scaleFactor, 1f);
                    }
                }
            }
        }

        private void EnsurePoolSize(int requiredCount)
        {
            while (_lineRenderers.Count < requiredCount)
            {
                int idx = _lineRenderers.Count;
                GameObject childObj = new GameObject($"ChargePreviewTile_{idx}");
                childObj.transform.SetParent(transform, false);

                SpriteRenderer childSr = childObj.AddComponent<SpriteRenderer>();
                childSr.sprite = _spriteRenderer.sprite;
                childSr.sharedMaterial = _spriteRenderer.sharedMaterial;
                childSr.sortingLayerID = _spriteRenderer.sortingLayerID;
                childSr.sortingOrder = _spriteRenderer.sortingOrder;
                childSr.drawMode = _spriteRenderer.drawMode;
                childSr.size = _spriteRenderer.size;

                _lineRenderers.Add(childSr);
            }
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
