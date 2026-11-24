using UnityEngine;
using TMPro;
using BagBack100.Player;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 장비 패널의 플레이어 스탯 텍스트(HP·MP·ATK·DEF·SPD) 표시. 던전은 Player.prefab, 허브는 Manager 오브젝트의 PlayerStatManager에 붙는다.
    /// Dependencies: PlayerStatManager
    /// Events: 없음 (구독: PlayerStatManager.OnManagerCreated, PlayerStatManager.OnStatsChanged)
    /// </summary>
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
            BindExisting();
        }

        // 패널이 꺼진 채 시작하면 매니저 생성 이벤트를 놓치므로, 켜질 때마다 현재 매니저를 찾아 다시 붙인다
        private void OnEnable()
        {
            BindExisting();
        }

        private void OnDestroy()
        {
            PlayerStatManager.OnManagerCreated -= OnManagerCreated;

            if (_statManager != null)
            {
                _statManager.OnStatsChanged -= UpdateStats;
            }
        }

        // ??는 파괴된 UnityEngine.Object를 null로 보지 않으므로 != null 오버로드로 확인한다
        private void BindExisting()
        {
            var existing = _statManager != null ? _statManager : FindObjectOfType<PlayerStatManager>();
            if (existing != null) OnManagerCreated(existing);
        }

        private void OnManagerCreated(PlayerStatManager manager)
        {
            if (manager == null) return;

            if (_statManager != manager)
            {
                if (_statManager != null) _statManager.OnStatsChanged -= UpdateStats;
                _statManager = manager;
                _statManager.OnStatsChanged += UpdateStats;
            }

            // 구독 직후 현재 스탯으로 한 번 맞춘다 (패널이 나중에 켜졌을 때 대비)
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
