using UnityEngine;
using UnityEngine.EventSystems;
using DimensionLooter.Data.Core;
using DimensionLooter.Player;

namespace DimensionLooter.UI
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class DraggableItemUI : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI Elements")]
        [Tooltip("배치 불가능 시 빨간색으로 변할 테두리 이미지 컴포넌트")]
        [SerializeField] private UnityEngine.UI.Image _borderImage;
        [Tooltip("실제 아이템 아이콘 트랜스폼 (회전 시 방향만 돌아갈 대상)")]
        [SerializeField] private Transform _iconTransform;

        private RuntimeItem _item;
        private InventoryManager _inventoryManager;
        private InventoryUIController _uiController;
        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;

        private Vector2 _cellSize;
        private Vector2 _spacing;
        
        private Color _defaultBorderColor = Color.white;
        private Color _errorBorderColor = new Color(1f, 0f, 0f, 0.5f);
        private bool _isHeld = false;

        public RuntimeItem Item => _item;
        public RectTransform rectTransform => _rectTransform;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            
            if (_borderImage != null)
                _defaultBorderColor = _borderImage.color;
        }

        public void Setup(RuntimeItem item, InventoryManager invManager, Vector2 cellSize, Vector2 spacing, InventoryUIController uiController)
        {
            _item = item;
            _inventoryManager = invManager;
            _cellSize = cellSize;
            _spacing = spacing;
            _uiController = uiController;
            
            if (_iconTransform != null && _item?.ItemData != null)
            {
                var iconImage = _iconTransform.GetComponent<UnityEngine.UI.Image>();
                if (iconImage != null)
                {
                    iconImage.sprite = _item.ItemData.itemIcon;
                }
            }

            UpdateSizeAndRotation();
        }

        public void UpdateSizeAndRotation()
        {
            int w = _item.GetCurrentWidth();
            int h = _item.GetCurrentHeight();

            // 부모 UI 자체의 물리적 크기 변경 (2x3 -> 3x2)
            float currentWidth = (w * _cellSize.x) + ((w - 1) * _spacing.x);
            float currentHeight = (h * _cellSize.y) + ((h - 1) * _spacing.y);
            _rectTransform.sizeDelta = new Vector2(currentWidth, currentHeight);

            // 테두리 이미지가 자식 오브젝트로 분리되어 있을 경우를 대비한 꽉 채움(Stretch) 강제 설정
            if (_borderImage != null && _borderImage.transform != transform)
            {
                RectTransform borderRt = _borderImage.rectTransform;
                borderRt.anchorMin = Vector2.zero;
                borderRt.anchorMax = Vector2.one;
                borderRt.sizeDelta = Vector2.zero;
                borderRt.anchoredPosition = Vector2.zero;
            }

            // 아이콘 이미지만 회전시키기 위한 강제 설정
            if (_iconTransform != null)
            {
                RectTransform iconRt = _iconTransform as RectTransform;
                if (iconRt != null)
                {
                    // 아이콘은 무조건 '회전 전 기본 크기'를 유지해야 회전했을 때 찌그러지지 않고 부모 영역에 딱 맞음
                    float baseWidth = (_item.ItemData.gridWidth * _cellSize.x) + ((_item.ItemData.gridWidth - 1) * _spacing.x);
                    float baseHeight = (_item.ItemData.gridHeight * _cellSize.y) + ((_item.ItemData.gridHeight - 1) * _spacing.y);

                    iconRt.anchorMin = new Vector2(0.5f, 0.5f);
                    iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                    iconRt.sizeDelta = new Vector2(baseWidth, baseHeight);
                    iconRt.anchoredPosition = Vector2.zero;
                }
                
                _iconTransform.localEulerAngles = _item.IsRotated ? new Vector3(0, 0, -90f) : Vector3.zero;
            }
        }

        public void SetHeldState(bool isHeld)
        {
            _isHeld = isHeld;
            
            if (isHeld)
            {
                transform.SetAsLastSibling();
                _canvasGroup.blocksRaycasts = false; // 마우스 클릭 무시 (빈 공간을 클릭해서 내려놓게 하기 위함)
                _canvasGroup.alpha = 0.8f;
            }
            else
            {
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.alpha = 1f;
                ResetBorderFeedback();
            }
        }

        public void ResetBorderFeedback()
        {
            if (_borderImage != null) _borderImage.color = _defaultBorderColor;
        }

        public void UpdateBorderColorFeedback()
        {
            if (_borderImage == null || _inventoryManager == null) return;

            Rect parentRect = ((RectTransform)transform.parent).rect;
            float offsetX = _rectTransform.localPosition.x - parentRect.xMin;
            float offsetY = parentRect.yMax - _rectTransform.localPosition.y;

            int targetGridX = Mathf.RoundToInt(offsetX / (_cellSize.x + _spacing.x));
            int targetGridY = Mathf.RoundToInt(offsetY / (_cellSize.y + _spacing.y));

            bool canPlace = _inventoryManager.CanPlaceItem(_item, targetGridX, targetGridY);
            _borderImage.color = canPlace ? _defaultBorderColor : _errorBorderColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (eventData.clickCount == 2)
                {
                    if (_uiController != null)
                    {
                        _uiController.OnItemDoubleClicked(this);
                    }
                }
                else if (eventData.clickCount == 1)
                {
                    if (_uiController != null)
                    {
                        _uiController.OnItemClicked(this);
                    }
                }
            }
        }
    }
}
