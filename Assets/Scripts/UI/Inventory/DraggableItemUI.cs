// File: Assets/Scripts/UI/Inventory/DraggableItemUI.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DimensionLooter.Gameplay.Inventory;
using System.Collections;
using System.Collections.Generic;

namespace DimensionLooter.UI.Inventory
{
    public class DraggableItemUI : MonoBehaviour, IPointerClickHandler
    {
        public InventoryItem Item { get; private set; }
        public InventoryGridUI CurrentGridUI { get; private set; }
        
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Text _nameText;

        private RectTransform _rectTransform;
        private Canvas _canvas;
        private Transform _originalParent;
        private Vector2 _originalPosition;
        
        // Click-and-Carry 상태
        public bool IsCarrying { get; private set; }
        private float _carryStartTime;
        private Vector2Int _grabOffset;
        private GameObject _ghostObj;
        private Image _ghostBg;
        
        private InventoryGridUI _originalGridUI;
        private int _originalGridX;
        private int _originalGridY;

        public void Init(InventoryItem item, InventoryGridUI initialGridUI)
        {
            Item = item;
            CurrentGridUI = initialGridUI;
            _rectTransform = GetComponent<RectTransform>();
            _canvas = initialGridUI.GetComponentInParent<Canvas>();

            _rectTransform.pivot = new Vector2(0, 1);
            _rectTransform.anchorMin = new Vector2(0, 1);
            _rectTransform.anchorMax = new Vector2(0, 1);

            if (_nameText != null) 
            {
                _nameText.text = item.Data.Name;
            }
            if (_backgroundImage != null) 
            {
                _backgroundImage.color = new Color(Random.Range(0.2f, 0.8f), Random.Range(0.2f, 0.8f), Random.Range(0.2f, 0.8f), 0.9f);
            }

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

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsCarrying)
            {
                // 오른쪽 클릭 시 회전 (들지 않고 제자리 회전 시도)
                if (eventData.button == PointerEventData.InputButton.Right)
                {
                    TryRotateInPlace();
                    return;
                }
                
                // 들기 (Pick Up)
                PickUp(eventData);
            }
            // IsCarrying == true 일 때의 클릭 처리는 Update()의 전역 Input 감지에서 처리하여
            // 허공, 다른 UI, 빈 그리드 등 어디를 클릭해도 동일하게 작동하도록 보장합니다.
        }

        private void TryRotateInPlace()
        {
            CurrentGridUI.GridLogic.RemoveItem(Item);
            Item.IsRotated = !Item.IsRotated;
            
            if (CurrentGridUI.GridLogic.PlaceItem(Item, Item.GridX, Item.GridY))
            {
                UpdateSizeAndRotation(CurrentGridUI.CellSize);
            }
            else
            {
                Item.IsRotated = !Item.IsRotated; // 원상복구
                CurrentGridUI.GridLogic.PlaceItem(Item, Item.GridX, Item.GridY);
            }
        }

        private void PickUp(PointerEventData eventData)
        {
            IsCarrying = true;
            _carryStartTime = Time.unscaledTime;
            _originalParent = transform.parent;
            _originalPosition = _rectTransform.anchoredPosition;
            _originalGridUI = CurrentGridUI;
            _originalGridX = Item.GridX;
            _originalGridY = Item.GridY;

            // 로컬 클릭 위치 계산 (Grab Offset)
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPointer
            );
            
            int grabbedCol = Mathf.FloorToInt(localPointer.x / CurrentGridUI.CellSize);
            int grabbedRow = Mathf.FloorToInt(Mathf.Abs(localPointer.y) / CurrentGridUI.CellSize);
            _grabOffset = new Vector2Int(grabbedCol, grabbedRow);

            CurrentGridUI.GridLogic.RemoveItem(Item);

            transform.SetParent(_canvas.transform, true);
            transform.SetAsLastSibling();
            GetComponent<CanvasGroup>().blocksRaycasts = false; // 자신이 클릭을 막지 않도록

            CreateGhost();
        }

        private void CreateGhost()
        {
            _ghostObj = new GameObject("Ghost");
            _ghostObj.transform.SetParent(_canvas.transform, false);
            _ghostObj.transform.SetSiblingIndex(transform.GetSiblingIndex()); // 진짜 UI 바로 아래

            RectTransform ghostRt = _ghostObj.AddComponent<RectTransform>();
            ghostRt.pivot = new Vector2(0, 1);
            ghostRt.anchorMin = new Vector2(0, 1);
            ghostRt.anchorMax = new Vector2(0, 1);
            ghostRt.sizeDelta = _rectTransform.sizeDelta;

            _ghostBg = _ghostObj.AddComponent<Image>();
            _ghostBg.raycastTarget = false; // 고스트가 클릭을 막지 않도록
        }

        private void Update()
        {
            if (IsCarrying)
            {
                // 실제 마우스 커서를 따라다님 (Grab Offset 유지)
                Vector2 screenPos = Input.mousePosition;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvas.transform as RectTransform, screenPos, _canvas.worldCamera, out Vector2 localMousePos
                );
                
                // 마우스 위치에서 Grab Offset만큼 보정한 위치가 아이템의 좌상단(pivot)이 됨
                Vector2 offset = new Vector2(_grabOffset.x * CurrentGridUI.CellSize, -_grabOffset.y * CurrentGridUI.CellSize);
                _rectTransform.localPosition = localMousePos - offset;

                UpdateGhostPosition();

                // PickUp 된 프레임 직후 바로 Drop 되는 것을 방지 (0.1초 쿨타임)
                if (Time.unscaledTime - _carryStartTime > 0.1f)
                {
                    if (Input.GetMouseButtonDown(0))
                    {
                        PointerEventData dropEvent = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
                        Drop(dropEvent);
                    }
                    else if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.R))
                    {
                        Item.IsRotated = !Item.IsRotated;
                        UpdateSizeAndRotation(CurrentGridUI.CellSize);
                        if (_ghostObj != null)
                        {
                            _ghostObj.GetComponent<RectTransform>().sizeDelta = _rectTransform.sizeDelta;
                        }
                    }
                }
            }
        }

        private void UpdateGhostPosition()
        {
            if (_ghostObj == null) return;

            PointerEventData eventData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            InventoryGridUI targetGridUI = null;
            foreach (var res in results)
            {
                targetGridUI = res.gameObject.GetComponentInParent<InventoryGridUI>();
                if (targetGridUI != null) break;
            }

            if (targetGridUI != null)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(targetGridUI.ContainerRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
                {
                    int hoverCol = Mathf.FloorToInt(localPoint.x / targetGridUI.CellSize);
                    int hoverRow = Mathf.FloorToInt(Mathf.Abs(localPoint.y) / targetGridUI.CellSize);

                    int targetCol = hoverCol - _grabOffset.x;
                    int targetRow = hoverRow - _grabOffset.y;

                    bool canPlace = targetGridUI.GridLogic.CanPlaceItem(Item, targetCol, targetRow);
                    _ghostBg.color = canPlace ? new Color(0, 1, 0, 0.4f) : new Color(1, 0, 0, 0.4f);

                    _ghostObj.transform.SetParent(targetGridUI.ContainerRect, false);
                    _ghostObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(targetCol * targetGridUI.CellSize, -targetRow * targetGridUI.CellSize);
                    _ghostObj.SetActive(true);
                    return;
                }
            }
            
            // 그리드 밖이면 숨김
            _ghostObj.SetActive(false);
        }

        public void Drop(PointerEventData eventData)
        {
            IsCarrying = false;
            GetComponent<CanvasGroup>().blocksRaycasts = true;
            if (_ghostObj != null) Destroy(_ghostObj);

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            InventoryGridUI targetGridUI = null;
            foreach (var res in results)
            {
                targetGridUI = res.gameObject.GetComponentInParent<InventoryGridUI>();
                if (targetGridUI != null) break;
            }

            if (targetGridUI != null)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(targetGridUI.ContainerRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
                {
                    int hoverCol = Mathf.FloorToInt(localPoint.x / targetGridUI.CellSize);
                    int hoverRow = Mathf.FloorToInt(Mathf.Abs(localPoint.y) / targetGridUI.CellSize);

                    int targetCol = hoverCol - _grabOffset.x;
                    int targetRow = hoverRow - _grabOffset.y;

                    if (targetGridUI.GridLogic.PlaceItem(Item, targetCol, targetRow))
                    {
                        CurrentGridUI.UnregisterItemUI(Item);
                        CurrentGridUI = targetGridUI;
                        CurrentGridUI.RegisterItemUI(Item, this);
                        return;
                    }
                }
            }

            // 실패 시 원상 복귀 (Lerp 코루틴 실행)
            StartCoroutine(ReturnToOriginalCoroutine());
        }

        // 전역 클릭(배경 클릭 등)에서 아이템을 강제로 내려놓아야 할 때 호출될 수 있는 Public 메서드
        public void ForceDropReturn()
        {
            if (!IsCarrying) return;
            IsCarrying = false;
            GetComponent<CanvasGroup>().blocksRaycasts = true;
            if (_ghostObj != null) Destroy(_ghostObj);
            
            StartCoroutine(ReturnToOriginalCoroutine());
        }

        private IEnumerator ReturnToOriginalCoroutine()
        {
            // 데이터 논리 복구
            _originalGridUI.GridLogic.PlaceItem(Item, _originalGridX, _originalGridY);
            CurrentGridUI = _originalGridUI;

            Vector2 startPos = _rectTransform.anchoredPosition;
            Transform startParent = _rectTransform.parent;
            
            // 시각적 도착지는 원래 부모(컨테이너)에서의 anchoredPosition
            Vector2 targetPos = _originalPosition;
            
            // 부모를 캔버스에 둔 상태로 이동해야 다른 UI를 가리지 않고 위로 날아감
            float duration = 0.2f;
            float t = 0f;
            
            // 도착지의 월드 좌표 계산을 위해 임시로 원래 부모로 붙여서 얻어오기 (Canvas 내 로컬 좌표로 변환하기 위함)
            transform.SetParent(_originalParent, false);
            _rectTransform.anchoredPosition = targetPos;
            Vector3 targetWorld = _rectTransform.position;
            
            // 다시 캔버스로 돌려놓고 비행 시작
            transform.SetParent(_canvas.transform, true);
            Vector3 startWorld = _rectTransform.position;

            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                _rectTransform.position = Vector3.Lerp(startWorld, targetWorld, Easing(t));
                yield return null;
            }

            // 최종 부착
            transform.SetParent(_originalParent, false);
            _rectTransform.anchoredPosition = targetPos;
            CurrentGridUI.RegisterItemUI(Item, this); // 레지스터에 재등록하여 정렬 보장
        }

        private float Easing(float t)
        {
            // Out-Quad Easing
            return t * (2f - t);
        }
    }
}
