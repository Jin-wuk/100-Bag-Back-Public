// File: Assets/Scripts/UI/Inventory/DraggableItemUI.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DimensionLooter.Gameplay.Inventory;
using System.Collections.Generic;

namespace DimensionLooter.UI.Inventory
{
    public class DraggableItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public InventoryItem Item { get; private set; }
        public InventoryGridUI CurrentGridUI { get; private set; }
        
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Text _nameText;

        private RectTransform _rectTransform;
        private Canvas _canvas;
        private Transform _originalParent;
        private Vector2 _originalPosition;
        private bool _isDragging = false;

        public void Init(InventoryItem item, InventoryGridUI initialGridUI)
        {
            Item = item;
            CurrentGridUI = initialGridUI;
            _rectTransform = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();

            // Pivot을 Top-Left로 고정
            _rectTransform.pivot = new Vector2(0, 1);
            _rectTransform.anchorMin = new Vector2(0, 1);
            _rectTransform.anchorMax = new Vector2(0, 1);

            if (_nameText != null) 
            {
                _nameText.text = item.Data.Name;
            }
            if (_backgroundImage != null) 
            {
                // 랜덤 색상으로 칠하되 테두리가 보이게 약간 투명도 줌
                _backgroundImage.color = new Color(Random.Range(0.2f, 0.8f), Random.Range(0.2f, 0.8f), Random.Range(0.2f, 0.8f), 0.9f);
            }

            // CanvasGroup이 없으면 추가하여 드래그 중 광선 차단을 제어
            if (GetComponent<CanvasGroup>() == null)
            {
                gameObject.AddComponent<CanvasGroup>();
            }

            UpdateSizeAndRotation(initialGridUI.CellSize);
        }

        public void UpdateSizeAndRotation(float cellSize)
        {
            int w = Item.GetWidth();
            int h = Item.GetHeight();
            _rectTransform.sizeDelta = new Vector2(w * cellSize, h * cellSize);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
            _originalParent = transform.parent;
            _originalPosition = _rectTransform.anchoredPosition;

            // 드래그 중 최상단 노출
            transform.SetParent(_canvas.transform, true);
            transform.SetAsLastSibling();

            GetComponent<CanvasGroup>().blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            _rectTransform.anchoredPosition += eventData.delta / _canvas.scaleFactor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
            GetComponent<CanvasGroup>().blocksRaycasts = true;

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            InventoryGridUI targetGridUI = null;
            foreach (var result in results)
            {
                targetGridUI = result.gameObject.GetComponentInParent<InventoryGridUI>();
                if (targetGridUI != null) break;
            }

            if (targetGridUI != null)
            {
                RectTransform targetRect = targetGridUI.GetComponent<RectTransform>();
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
                {
                    Vector2Int gridPos = targetGridUI.GetGridPosFromLocalPoint(localPoint);

                    CurrentGridUI.GridLogic.RemoveItem(Item);

                    if (targetGridUI.GridLogic.PlaceItem(Item, gridPos.x, gridPos.y))
                    {
                        CurrentGridUI.UnregisterItemUI(Item);
                        CurrentGridUI = targetGridUI;
                        CurrentGridUI.RegisterItemUI(Item, this);
                        return;
                    }
                    else
                    {
                        CurrentGridUI.GridLogic.PlaceItem(Item, Item.GridX, Item.GridY);
                    }
                }
            }

            ReturnToOriginalPosition();
        }

        private void ReturnToOriginalPosition()
        {
            transform.SetParent(_originalParent, false);
            _rectTransform.anchoredPosition = _originalPosition;
        }

        private void Update()
        {
            if (_isDragging && Input.GetKeyDown(KeyCode.R))
            {
                Item.IsRotated = !Item.IsRotated;
                UpdateSizeAndRotation(CurrentGridUI.CellSize);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // 우클릭으로도 회전 지원 (옵션)
            if (_isDragging && eventData.button == PointerEventData.InputButton.Right)
            {
                Item.IsRotated = !Item.IsRotated;
                UpdateSizeAndRotation(CurrentGridUI.CellSize);
            }
        }
    }
}
