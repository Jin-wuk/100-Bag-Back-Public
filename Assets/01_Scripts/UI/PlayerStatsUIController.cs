using UnityEngine;
using TMPro;
using DimensionLooter.Player;

namespace DimensionLooter.UI
{
    public class PlayerStatsUIController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _hpText;
        [SerializeField] private TextMeshProUGUI _mpText;
        [SerializeField] private TextMeshProUGUI _attackText;
        [SerializeField] private TextMeshProUGUI _defenseText;
        [SerializeField] private TextMeshProUGUI _speedText;

        private PlayerStatManager _statManager;

        private void Awake()
        {
            PlayerStatManager.OnManagerCreated += OnManagerCreated;

            if (_statManager == null)
            {
                var existing = FindObjectOfType<PlayerStatManager>();
                if (existing != null) OnManagerCreated(existing);
            }
        }

        private void OnDestroy()
        {
            PlayerStatManager.OnManagerCreated -= OnManagerCreated;
            
            if (_statManager != null)
            {
                _statManager.OnStatsChanged -= UpdateStats;
            }
        }

        private void OnManagerCreated(PlayerStatManager manager)
        {
            _statManager = manager;
            _statManager.OnStatsChanged += UpdateStats;
            
            // UI가 나중에 활성화(지연 생성)되었을 때를 대비해, 구독 즉시 현재 스탯으로 화면을 1회 강제 동기화합니다.
            UpdateStats(_statManager.CurrentHp, _statManager.CurrentMp, _statManager.CurrentAttack, _statManager.CurrentDefense, _statManager.CurrentSpeed, _statManager.CurrentRange);
        }

        public void UpdateStats(int hp, int mp, int atk, int def, float speed, int range)
        {
            if (_hpText != null) _hpText.text = $"HP: {hp}";
            if (_mpText != null) _mpText.text = $"MP: {mp}";
            if (_attackText != null) _attackText.text = $"ATK: {atk}";
            if (_defenseText != null) _defenseText.text = $"DEF: {def}";
            if (_speedText != null) _speedText.text = $"SPD: {speed:F1}";
        }
    }
}
