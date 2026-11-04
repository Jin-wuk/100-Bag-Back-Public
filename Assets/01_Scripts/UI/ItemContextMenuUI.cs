using System;
using UnityEngine;
using UnityEngine.UI;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 아이템 우클릭 메뉴(가방·창고·보안 파우치 공용, 가방 패널 안에 하나). 상황에 맞는 버튼만 보여주고, 선택 결과는 콜백으로 돌려준다.
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
        [SerializeField] private Button _sellButton;
        [SerializeField] private Button _splitButton;

        private Action _onDiscard;
        private Action _onSell;
        private Action _onSplit;

        private static int s_openCount;
        public static bool IsAnyOpen => s_openCount > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_openCount = 0;

        private bool IsOpen => _menuPanel != null && _menuPanel.gameObject.activeSelf;

        private void Awake()
        {
            if (_blocker != null) _blocker.onClick.AddListener(Close);
            if (_discardButton != null) _discardButton.onClick.AddListener(HandleDiscard);
            if (_sellButton != null) _sellButton.onClick.AddListener(HandleSell);
            if (_splitButton != null) _splitButton.onClick.AddListener(HandleSplit);
            Close();
        }

        private void OnDestroy()
        {
            if (_blocker != null) _blocker.onClick.RemoveListener(Close);
            if (_discardButton != null) _discardButton.onClick.RemoveListener(HandleDiscard);
            if (_sellButton != null) _sellButton.onClick.RemoveListener(HandleSell);
            if (_splitButton != null) _splitButton.onClick.RemoveListener(HandleSplit);
        }

        private void OnDisable() => Close();

        /// <returns>보여줄 버튼이 하나도 없으면 열지 않고 false</returns>
        public bool Open(Vector2 screenPosition, Camera eventCamera, Action onDiscard, Action onSell = null, Action onSplit = null)
        {
            _onDiscard = onDiscard;
            _onSell = onSell;
            _onSplit = onSplit;
            bool hasAny = onDiscard != null || onSell != null || onSplit != null;
            if (_discardButton != null) _discardButton.gameObject.SetActive(onDiscard != null);
            if (_sellButton != null) _sellButton.gameObject.SetActive(onSell != null);
            if (_splitButton != null) _splitButton.gameObject.SetActive(onSplit != null);
            if (!hasAny) return false;

            if (!IsOpen) s_openCount++;
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
            _onSell = null;
            _onSplit = null;
            if (IsOpen) s_openCount = Mathf.Max(0, s_openCount - 1);
            if (_blocker != null) _blocker.gameObject.SetActive(false);
            if (_menuPanel != null) _menuPanel.gameObject.SetActive(false);
        }

        private void HandleSell()
        {
            var callback = _onSell;
            Close();
            callback?.Invoke();
        }

        private void HandleSplit()
        {
            var callback = _onSplit;
            Close();
            callback?.Invoke();
        }

        private void HandleDiscard()
        {
            var callback = _onDiscard;
            Close();
            callback?.Invoke();
        }
    }
}
