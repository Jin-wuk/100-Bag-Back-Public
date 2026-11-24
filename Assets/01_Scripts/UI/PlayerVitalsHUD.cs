using UnityEngine;
using UnityEngine.UI;
using BagBack100.Player;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 던전 핫바 왼쪽의 HP/MP 분할 오브. 왼쪽 절반 HP, 오른쪽 절반 MP를 아래→위로 채우고,
    ///       값이 줄면 줄기 전 높이에 반투명 잔상을 잠깐 남겼다가 따라 내린다. 수치를 계산하지 않고 PlayerStatManager 값만 표시한다.
    /// Dependencies: PlayerStatManager
    /// Events: 없음 (구독: PlayerStatManager.OnManagerCreated, PlayerStatManager.OnStatsChanged)
    /// </summary>
    public class PlayerVitalsHUD : MonoBehaviour
    {
        [Header("Images (Filled / Vertical / Bottom)")]
        [SerializeField] private Image _hpFill;
        [SerializeField] private Image _mpFill;
        [SerializeField] private Image _hpTrail;
        [SerializeField] private Image _mpTrail;

        [Header("Fill Range")]
        [Tooltip("0%일 때 fillAmount. 오브 아트의 채움 유효 범위(y 7~57 / 64) 아래 끝")]
        [SerializeField, Range(0f, 1f)] private float _fillMin = 0.11f;
        [Tooltip("100%일 때 fillAmount. 채움 유효 범위 위 끝")]
        [SerializeField, Range(0f, 1f)] private float _fillMax = 0.89f;

        [Header("Trail")]
        [Tooltip("값이 줄어든 뒤 잔상이 멈춰 있는 시간(초)")]
        [SerializeField, Min(0f)] private float _trailHoldTime = 0.35f;
        [Tooltip("잔상이 내려오는 속도(초당 fillAmount)")]
        [SerializeField, Min(0.01f)] private float _trailDropSpeed = 0.9f;

        private PlayerStatManager _stats;
        private bool _subscribed;
        private float _hpTrailHold;
        private float _mpTrailHold;
        private int _lastMaxHp;
        private int _lastMaxMp;

        private void Awake()
        {
            PlayerStatManager.OnManagerCreated += Bind;
            BindExisting();
        }

        // 플레이어는 DungeonGenerator가 런타임에 만들므로 HUD가 먼저 깨어날 수 있다. 켜질 때마다 현재 매니저를 다시 찾는다
        private void OnEnable()
        {
            BindExisting();
        }

        // 꺼져 있는 동안에는 값을 받지 않는다. 다시 켜지면 OnEnable에서 현재 값으로 맞춘다
        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            PlayerStatManager.OnManagerCreated -= Bind;
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (_stats != null && _subscribed) _stats.OnStatsChanged -= HandleStatsChanged;
            _subscribed = false;
        }

        // ??는 파괴된 UnityEngine.Object를 null로 보지 않으므로 != null 오버로드로 확인한다
        private void BindExisting()
        {
            var existing = _stats != null ? _stats : FindObjectOfType<PlayerStatManager>();
            if (existing != null) Bind(existing);
        }

        private void Bind(PlayerStatManager manager)
        {
            if (manager == null || !isActiveAndEnabled) return;
            if (manager == _stats && _subscribed) return;

            Unsubscribe();
            _stats = manager;
            _stats.OnStatsChanged += HandleStatsChanged;
            _subscribed = true;

            // 처음 붙을 때는 잔상 없이 현재 값으로 맞춘다
            _lastMaxHp = _stats.MaxHp;
            _lastMaxMp = _stats.MaxMp;
            SetImmediate(_hpFill, _hpTrail, ToFill(_stats.CurrentHp, _stats.MaxHp));
            SetImmediate(_mpFill, _mpTrail, ToFill(_stats.CurrentMp, _stats.MaxMp));
            _hpTrailHold = 0f;
            _mpTrailHold = 0f;
        }

        private void HandleStatsChanged(int hp, int mp, int atk, int def, float speed, int range)
        {
            if (_stats == null) return;
            ApplyValue(_hpFill, _hpTrail, ToFill(hp, _stats.MaxHp), _stats.MaxHp != _lastMaxHp, ref _hpTrailHold);
            ApplyValue(_mpFill, _mpTrail, ToFill(mp, _stats.MaxMp), _stats.MaxMp != _lastMaxMp, ref _mpTrailHold);
            _lastMaxHp = _stats.MaxHp;
            _lastMaxMp = _stats.MaxMp;
        }

        // 최대치가 바뀌어 비율만 달라진 경우(장비 교체)는 피해가 아니므로 잔상을 남기지 않는다
        private void ApplyValue(Image fill, Image trail, float target, bool maxChanged, ref float hold)
        {
            if (fill == null) return;
            fill.fillAmount = target;
            if (trail == null) return;

            if (maxChanged || target >= trail.fillAmount)
            {
                trail.fillAmount = target;
                hold = 0f;
            }
            else
            {
                hold = _trailHoldTime;
            }
        }

        // 잔상이 채움보다 높을 때만 움직인다. 할당 없음. 스케일 시간이라 가방·확인창 일시정지 중에는 멈춘다
        private void Update()
        {
            float dt = Time.deltaTime;
            TickTrail(_hpFill, _hpTrail, ref _hpTrailHold, dt);
            TickTrail(_mpFill, _mpTrail, ref _mpTrailHold, dt);
        }

        private void TickTrail(Image fill, Image trail, ref float hold, float dt)
        {
            if (fill == null || trail == null || trail.fillAmount <= fill.fillAmount) return;
            if (hold > 0f)
            {
                hold -= dt;
                return;
            }
            trail.fillAmount = Mathf.MoveTowards(trail.fillAmount, fill.fillAmount, _trailDropSpeed * dt);
        }

        private float ToFill(int current, int max)
        {
            float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            return Mathf.Lerp(_fillMin, _fillMax, ratio);
        }

        private static void SetImmediate(Image fill, Image trail, float value)
        {
            if (fill != null) fill.fillAmount = value;
            if (trail != null) trail.fillAmount = value;
        }
    }
}
