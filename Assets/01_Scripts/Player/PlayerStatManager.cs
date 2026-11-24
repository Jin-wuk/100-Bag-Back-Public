using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using BagBack100.Data.Core;
using BagBack100.Core;
using BagBack100.Data.Models;

namespace BagBack100.Player
{
    /// <summary>
    /// Role: 플레이어 기본 스탯 + 장비 보너스 합산, HP/MP 관리, 피격(방어력 경감, 피격 무적, 보스 처치 후 무적), 사망 처리(가방·장비 증발 후 허브 복귀).
    /// Dependencies: PlayerBaseStatsSO(기본 스탯), EquipmentManager, InventoryManager, CombatMath, PlayerSessionData
    /// Events: OnManagerCreated(static), OnStatsChanged, GlobalEvents.OnEntityDamaged
    /// </summary>
    [RequireComponent(typeof(EquipmentManager))]
    public class PlayerStatManager : MonoBehaviour
    {
        public static event Action<PlayerStatManager> OnManagerCreated;
        public event Action<int, int, int, int, float, int> OnStatsChanged;

        [Header("Base Stats")]
        [Tooltip("장비 보너스를 더하기 전 기본 스탯. 던전 Player.prefab과 허브 Manager가 같은 에셋을 쓴다")]
        [SerializeField] private PlayerBaseStatsSO _baseStats;

        [Header("Hit Invincibility (모든 피격 동일, 유저 결정 2026-11-05)")]
        [Tooltip("피격 후 모든 피해를 무시하는 시간(초)")]
        [SerializeField, Min(0f)] private float _hitInvincibleDuration = 0.5f;
        [Tooltip("피격 직후 빨갛게 표시하는 시간(초)")]
        [SerializeField, Min(0f)] private float _hitFlashDuration = 0.1f;
        [Tooltip("무적 중 깜빡임 간격(초)")]
        [SerializeField, Min(0.01f)] private float _hitBlinkInterval = 0.08f;

        [Header("Death Settings")]
        [SerializeField] private string _hubSceneName = "02_HubScene";

        public int MaxHp { get; private set; }
        public int MaxMp { get; private set; }
        public int CurrentHp { get; private set; }
        public int CurrentMp { get; private set; }
        public int CurrentAttack { get; private set; }
        public int CurrentDefense { get; private set; }
        public float CurrentSpeed { get; private set; }
        public float CurrentAttackSpeed { get; private set; }
        public int CurrentRange { get; private set; }

        private EquipmentManager _equipmentManager;
        private InventoryManager _inventoryManager;
        private bool _isInitialized = false;
        private bool _isDead = false;
        private float _hitInvincibleEndTime;
        private SpriteRenderer _hitFeedbackRenderer;
        private Color _hitFeedbackBaseColor = Color.white;
        private Coroutine _hitFeedbackRoutine;

        private void Awake()
        {
            _equipmentManager = GetComponent<EquipmentManager>();
            _inventoryManager = GetComponent<InventoryManager>();
            if (_baseStats == null) Debug.LogError($"[PlayerStatManager] {name}: 기본 스탯 SO(_baseStats)가 연결되지 않아 스탯을 계산하지 않습니다.", this);
            _hitFeedbackRenderer = GetComponentInChildren<SpriteRenderer>();
            // 차징 연출이 색을 바꾸므로 복구 기준은 피격 순간의 색이 아니라 여기서 저장한 기본색이다
            if (_hitFeedbackRenderer != null) _hitFeedbackBaseColor = _hitFeedbackRenderer.color;
            CalculateStats();
            OnManagerCreated?.Invoke(this);
        }

        private void OnEnable()
        {
            if (_equipmentManager != null)
            {
                _equipmentManager.OnEquipmentChanged += CalculateStats;
            }
        }

        private void OnDisable()
        {
            if (_equipmentManager != null)
            {
                _equipmentManager.OnEquipmentChanged -= CalculateStats;
            }
            StopHitFeedback();
        }

        private void Start()
        {
            CalculateStats();
        }

        private void CalculateStats()
        {
            if (_baseStats == null) return;

            var eqStats = _equipmentManager != null
                ? _equipmentManager.GetTotalStats()
                : (hp: 0, mp: 0, atk: 0, def: 0, speed: 0f, range: 0, atkSpeedBonus: 0f, weaponBaseAtkSpeed: 0f);

            MaxHp = Mathf.Max(1, _baseStats.Hp + eqStats.hp);
            MaxMp = Mathf.Max(0, _baseStats.Mp + eqStats.mp);

            if (!_isInitialized)
            {
                CurrentHp = MaxHp;
                CurrentMp = MaxMp;
                _isInitialized = true;
            }
            else
            {
                CurrentHp = Mathf.Clamp(CurrentHp, 0, MaxHp);
                CurrentMp = Mathf.Clamp(CurrentMp, 0, MaxMp);
            }

            CurrentAttack = _baseStats.Attack + eqStats.atk;
            CurrentDefense = _baseStats.Defense + eqStats.def;
            CurrentRange = _baseStats.Range + eqStats.range;

            CurrentSpeed = Mathf.Max(1f, _baseStats.Speed + eqStats.speed);
            
            // 공격 속도 계산 로직
            // 무기의 고유 공격 속도가 우선순위, 없으면 플레이어 기본 스탯 사용, 이후 악세서리 등 합산
            float baseAtkSpd = eqStats.weaponBaseAtkSpeed > 0f ? eqStats.weaponBaseAtkSpeed : _baseStats.AttackSpeed;
            CurrentAttackSpeed = Mathf.Max(0.1f, baseAtkSpd + eqStats.atkSpeedBonus);

            OnStatsChanged?.Invoke(CurrentHp, CurrentMp, CurrentAttack, CurrentDefense, CurrentSpeed, CurrentRange);
        }

        // 보스 처치 후 그 층을 떠날 때까지 피해를 받지 않는다 (기획 07 Q29). DungeonGenerator가 켜고 끈다
        public bool IsInvincible { get; private set; }
        public void SetInvincible(bool value) => IsInvincible = value;

        // 스케일 시간 기준이라 가방·확인창 일시정지 중에는 무적 시간도 흐르지 않는다
        public bool IsHitInvincible => Time.time < _hitInvincibleEndTime;

        /// <param name="damage">방어력 경감 전 원본 피해</param>
        /// <returns>방어력 경감 후 실제로 깎인 체력. 사망 상태이거나 피해가 0 이하면 0 (흡혈 등 후속 효과 판정용)</returns>
        public int TakeDamage(int damage)
        {
            if (_isDead || damage <= 0 || IsInvincible || IsHitInvincible) return 0;

            // 방어력 경감 후 값이 이후 HP 차감·데미지 텍스트·반환값의 기준이 된다
            damage = CombatMath.Mitigate(damage, CurrentDefense);

            // 초과 피해(overkill)는 흡혈 등에 반영되지 않도록 실제 깎인 체력만 반환
            int dealt = Mathf.Min(damage, CurrentHp);
            CurrentHp -= damage;
            if (CurrentHp < 0) CurrentHp = 0;
            Debug.Log($"[Player] {damage} 피해를 입음! 남은 HP: {CurrentHp}/{MaxHp}");
            GlobalEvents.OnEntityDamaged?.Invoke(transform.position, damage, false, true);
            OnStatsChanged?.Invoke(CurrentHp, CurrentMp, CurrentAttack, CurrentDefense, CurrentSpeed, CurrentRange);
            
            if (CurrentHp <= 0)
            {
                HandleDeath();
                return dealt;
            }

            _hitInvincibleEndTime = Time.time + _hitInvincibleDuration;
            if (_hitFeedbackRenderer != null)
            {
                StopHitFeedback();
                _hitFeedbackRoutine = StartCoroutine(HitFeedbackRoutine());
            }
            return dealt;
        }

        private void HandleDeath()
        {
            if (_isDead) return;
            _isDead = true;

            Debug.Log("[Player] 사망! 인벤토리 및 장착 장비가 모두 증발하고 허브로 복귀합니다.");

            // 결과 화면용 기록은 세션 목록이 비워지기 전에 복사해야 잃은 목록이 남는다
            PlayerSessionData.LastRunResult = RunResult.CreateFromSession(false);

            // 가방을 먼저 비워야 장비(가방) 해제로 용량이 줄 때 넘친 아이템이 바닥에 떨어지는 일이 없다.
            if (_inventoryManager != null)
            {
                _inventoryManager.ClearInventory();
            }
            if (_equipmentManager != null)
            {
                _equipmentManager.ClearEquipment();
            }
            PlayerSessionData.ClearPlayerLoadoutOnDeath();

            if (!string.IsNullOrEmpty(_hubSceneName))
            {
                SceneManager.LoadScene(_hubSceneName, LoadSceneMode.Single);
            }
        }

        // 프레임마다 판정해 일시정지(timeScale 0) 중에는 항상 보이게 한다. 꺼진 순간에 멈춰 캐릭터가 사라지는 것을 막는다
        private System.Collections.IEnumerator HitFeedbackRoutine()
        {
            float startTime = Time.time;
            float endTime = Mathf.Max(_hitInvincibleEndTime, startTime + _hitFlashDuration);
            bool isFlashing = _hitFlashDuration > 0f;
            if (isFlashing) _hitFeedbackRenderer.color = Color.red;

            while (Time.time < endTime)
            {
                float elapsed = Time.time - startTime;
                if (isFlashing && elapsed >= _hitFlashDuration)
                {
                    isFlashing = false;
                    _hitFeedbackRenderer.color = _hitFeedbackBaseColor;
                }

                bool isBlinkOff = Time.timeScale > 0f && elapsed >= _hitFlashDuration
                    && Mathf.FloorToInt((elapsed - _hitFlashDuration) / _hitBlinkInterval) % 2 == 0;
                _hitFeedbackRenderer.enabled = !isBlinkOff;
                yield return null;
            }

            _hitFeedbackRoutine = null;
            RestoreHitFeedbackVisual();
        }

        private void StopHitFeedback()
        {
            if (_hitFeedbackRoutine == null) return;
            StopCoroutine(_hitFeedbackRoutine);
            _hitFeedbackRoutine = null;
            RestoreHitFeedbackVisual();
        }

        private void RestoreHitFeedbackVisual()
        {
            if (_hitFeedbackRenderer == null) return;
            _hitFeedbackRenderer.color = _hitFeedbackBaseColor;
            _hitFeedbackRenderer.enabled = true;
        }

        public void Heal(int amount)
        {
            if (_isDead) return;

            CurrentHp = Mathf.Clamp(CurrentHp + amount, 0, MaxHp);
            OnStatsChanged?.Invoke(CurrentHp, CurrentMp, CurrentAttack, CurrentDefense, CurrentSpeed, CurrentRange);
        }

        public void RestoreMp(int amount)
        {
            if (_isDead) return;

            CurrentMp = Mathf.Clamp(CurrentMp + amount, 0, MaxMp);
            OnStatsChanged?.Invoke(CurrentHp, CurrentMp, CurrentAttack, CurrentDefense, CurrentSpeed, CurrentRange);
        }

        public bool IsDead => _isDead;
    }
}
