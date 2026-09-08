using UnityEngine;
using UnityEngine.EventSystems;
using DimensionLooter.Data.Core;
using DimensionLooter.Player;
using UnityEngine.UI;

namespace DimensionLooter.UI
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class DraggableItemUI : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI Elements")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private Text _countText;

        private RuntimeItem _item;
        private InventoryUIController _uiController;
        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        
        public RuntimeItem Item => _item;
        public RectTransform rectTransform => _rectTransform;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        public void Setup(RuntimeItem item, InventoryUIController uiController)
        {
            _item = item;
            _uiController = uiController;
            
            if (_iconImage != null && _item?.ItemData != null)
            {
                _iconImage.sprite = _item.ItemData.itemIcon;
            }

            if (_countText != null)
            {
                _countText.text = _item.Count > 1 ? _item.Count.ToString() : "";
            }
            
            // 1칸 고정이므로 부모 사이즈에 맞춤 (Stretch)
            _rectTransform.anchorMin = Vector2.zero;
            _rectTransform.anchorMax = Vector2.one;
            _rectTransform.sizeDelta = Vector2.zero;
            _rectTransform.anchoredPosition = Vector2.zero;
        }

        public void SetHeldState(bool isHeld)
        {
            if (isHeld)
            {
                transform.SetAsLastSibling();
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.alpha = 0.8f;
                // Held 상태일 때는 고정된 마우스 크기로 설정 (예: 60x60)
                _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                _rectTransform.sizeDelta = new Vector2(64f, 64f);
            }
            else
            {
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.alpha = 1f;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (eventData.clickCount == 2)
                {
                    if (_uiController != null) _uiController.OnItemDoubleClicked(this);
                }
                else if (eventData.clickCount == 1)
                {
                    if (_uiController != null) _uiController.OnItemClicked(this);
                }
            }
        }
    }
}
