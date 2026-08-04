// File: Assets/Scripts/UI/Lobby/GoldUI.cs
using UnityEngine;
using UnityEngine.UI;
using DimensionLooter.Managers;

namespace DimensionLooter.UI.Lobby
{
    public class GoldUI : MonoBehaviour
    {
        [SerializeField] private Text _goldText;

        private void Start()
        {
            if (_goldText == null)
            {
                _goldText = GetComponentInChildren<Text>();
            }

            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.OnGoldChanged += UpdateGoldUI;
                // 초기값 셋업
                UpdateGoldUI(EconomyManager.Instance.CurrentGold);
            }
        }

        private void OnDestroy()
        {
            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.OnGoldChanged -= UpdateGoldUI;
            }
        }

        private void UpdateGoldUI(int currentGold)
        {
            if (_goldText != null)
            {
                _goldText.text = $"{currentGold} G";
            }
        }
    }
}
