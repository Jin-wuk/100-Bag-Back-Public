using UnityEngine;
using UnityEngine.EventSystems;
using BagBack100.Data.Core;
using BagBack100.Player;
using UnityEngine.UI;
using TMPro;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 슬롯 안 아이템 아이콘. 드래그·더블클릭·우클릭 입력을 소속 컨테이너 UI로 전달만 한다.
    /// Dependencies: IItemContainerUI, RuntimeItem
    /// Events: 없음
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class DraggableItemUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("UI Elements")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _countText;

        private RuntimeItem _item;
        private IItemContainerUI _uiController;
        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        
        public RuntimeItem Item => _item;
        public RectTransform rectTransform => _rectTransform;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        public void Setup(RuntimeItem item, IItemContainerUI uiController)
        {
            _item = item;
            _uiController = uiController;

            if (_iconImage == null) Debug.LogWarning($"[DraggableItemUI] {_item.ItemData.itemName} UI 생성 중 경고: Icon Image가 인스펙터에 연결되지 않았습니다!");
            if (_countText == null) Debug.LogWarning($"[DraggableItemUI] {_item.ItemData.itemName} UI 생성 중 경고: Count Text가 인스펙터에 연결되지 않았습니다!");
            
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
                // Held 상태일 때는 고정된 마우스 크기로 설정 (예: 64x64)
                _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                _rectTransform.sizeDelta = new Vector2(64f, 64f);
            }
            else
            {
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.alpha = 1f;
                _rectTransform.anchorMin = Vector2.zero;
                _rectTransform.anchorMax = Vector2.one;
                _rectTransform.sizeDelta = Vector2.zero;
                _rectTransform.anchoredPosition = Vector2.zero;
            }
        }

        // 수량 창(나누기·버리기·판매)이 떠 있는 동안에는 뒤쪽 아이템을 끌거나 누르지 못하게 한다.
        // 창을 띄운 뒤 원본 수량·빈칸이 바뀌면 확인이 아무 일 없이 닫히므로 입력 입구에서 막는다 (유저 승인)
        private static bool IsInputLocked => ItemQuantityDialogUI.IsAnyOpen;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (IsInputLocked) return;
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (_uiController != null) _uiController.OnItemBeginDrag(this, eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (_uiController != null) _uiController.OnItemDrag(this, eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (_uiController != null) _uiController.OnItemEndDrag(this, eventData);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsInputLocked) return;
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (eventData.clickCount == 2)
                {
                    if (_uiController != null) _uiController.OnItemDoubleClicked(this);
                }
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (_uiController != null) _uiController.OnItemRightClicked(this, eventData);
            }
        }
    }
}
