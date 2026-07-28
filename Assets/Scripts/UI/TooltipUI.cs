// File: Assets/Scripts/UI/TooltipUI.cs
using UnityEngine;
using UnityEngine.UI;

namespace DimensionLooter.UI
{
    /// <summary>
    /// 마우스 오버 시 몬스터의 스탯 등을 보여주는 툴팁 UI입니다.
    /// [Why] 유저가 몬스터를 클릭하기 전(전투 진입 전) 스탯을 보고 교전 여부를 능동적으로 판단하게 만들기 위함입니다.
    /// </summary>
    public class TooltipUI : MonoBehaviour
    {
        public static TooltipUI Instance { get; private set; }

        [Tooltip("툴팁 배경 패널")]
        [SerializeField] private GameObject _tooltipPanel;
        
        [Tooltip("툴팁 내용 텍스트")]
        [SerializeField] private Text _infoText;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                Hide();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            // 툴팁이 켜져있을 때만 마우스 커서를 따라다니도록 설정
            if (_tooltipPanel != null && _tooltipPanel.activeSelf)
            {
                // 마우스 우측 하단에 살짝 띄워서 표시
                _tooltipPanel.transform.position = Input.mousePosition + new Vector3(15, -15, 0);
            }
        }

        public void Show(string text)
        {
            if (_infoText != null) _infoText.text = text;
            if (_tooltipPanel != null) _tooltipPanel.SetActive(true);
        }

        public void Hide()
        {
            if (_tooltipPanel != null) _tooltipPanel.SetActive(false);
        }
    }
}
