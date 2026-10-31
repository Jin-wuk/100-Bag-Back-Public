using System;
using UnityEngine;
using UnityEngine.UI;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 가방 아이템 우클릭 메뉴. 상황에 맞는 버튼만 보여주고, 선택 결과는 콜백으로 돌려준다.
    ///       메뉴 밖(전체 화면 투명 차단판)을 누르면 닫힌다.
    /// Dependencies: 없음
    /// Events: 없음 (버튼 선택 시 Open에 넘긴 콜백 호출)
    /// </summary>
    public class ItemContextMenuUI : MonoBehaviour
    {
        [Tooltip("화면 전체를 덮는 투명 차단판. 누르면 메뉴가 닫힌다")]
        [SerializeField] private Button _blocker;
        [SerializeField] private RectTransform _menuPanel;
        [SerializeField] private Button _discardButton;

        private Action _onDiscard;

        private void Awake()
        {
            if (_blocker != null) _blocker.onClick.AddListener(Close);
            if (_discardButton != null) _discardButton.onClick.AddListener(HandleDiscard);
            Close();
        }

        private void OnDestroy()
        {
            if (_blocker != null) _blocker.onClick.RemoveListener(Close);
            if (_discardButton != null) _discardButton.onClick.RemoveListener(HandleDiscard);
        }

        private void OnDisable() => Close();

        /// <returns>보여줄 버튼이 하나도 없으면 열지 않고 false</returns>
        public bool Open(Vector2 screenPosition, Camera eventCamera, Action onDiscard)
        {
            _onDiscard = onDiscard;
            bool hasAny = onDiscard != null;
            if (_discardButton != null) _discardButton.gameObject.SetActive(onDiscard != null);
            if (!hasAny) return false;

            if (_blocker != null)
            {
                _blocker.gameObject.SetActive(true);
                _blocker.transform.SetAsLastSibling();
            }
            if (_menuPanel != null)
            {
                _menuPanel.gameObject.SetActive(true);
                _menuPanel.SetAsLastSibling();
                var parent = (RectTransform)_menuPanel.parent;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, eventCamera, out Vector2 local))
                {
                    _menuPanel.localPosition = local;
                }
            }
            return true;
        }

        public void Close()
        {
            _onDiscard = null;
            if (_blocker != null) _blocker.gameObject.SetActive(false);
            if (_menuPanel != null) _menuPanel.gameObject.SetActive(false);
        }

        private void HandleDiscard()
        {
            var callback = _onDiscard;
            Close();
            callback?.Invoke();
        }
    }
}
