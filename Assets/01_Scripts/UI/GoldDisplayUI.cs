using UnityEngine;
using TMPro;
using BagBack100.Data.Core;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 보유 골드(지갑) 표시.
    /// Dependencies: PlayerSessionData
    /// Events: 없음 (구독: PlayerSessionData.OnGoldChanged)
    /// </summary>
    public class GoldDisplayUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _goldText;

        private void OnEnable()
        {
            PlayerSessionData.OnGoldChanged += HandleGoldChanged;
            HandleGoldChanged(PlayerSessionData.Gold);
        }

        private void OnDisable()
        {
            PlayerSessionData.OnGoldChanged -= HandleGoldChanged;
        }

        private void HandleGoldChanged(int gold)
        {
            if (_goldText != null) _goldText.text = $"Gold: {gold}G";
        }
    }
}
