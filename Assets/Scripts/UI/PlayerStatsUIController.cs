using UnityEngine;
using TMPro;

namespace DimensionLooter.UI
{
    public class PlayerStatsUIController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _hpText;
        [SerializeField] private TextMeshProUGUI _mpText;
        [SerializeField] private TextMeshProUGUI _attackText;
        [SerializeField] private TextMeshProUGUI _defenseText;

        // 실제로는 PlayerStatManager 등의 이벤트를 구독하여 갱신합니다.
        public void UpdateStats(int hp, int mp, int atk, int def)
        {
            if (_hpText != null) _hpText.text = $"HP: {hp}";
            if (_mpText != null) _mpText.text = $"MP: {mp}";
            if (_attackText != null) _attackText.text = $"ATK: {atk}";
            if (_defenseText != null) _defenseText.text = $"DEF: {def}";
        }
    }
}
