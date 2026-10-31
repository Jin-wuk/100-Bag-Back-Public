using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using BagBack100.Data.Core;
using BagBack100.Core;

namespace BagBack100.Player
{
    /// <summary>
    /// Role: 플레이어 기본 스탯 + 장비 보너스 합산, HP/MP 관리, 피격(방어력 경감) 및 사망 처리(가방·장비 증발 후 허브 복귀).
    /// Dependencies: EquipmentManager, InventoryManager, CombatMath, PlayerSessionData
    /// Events: OnManagerCreated(static), OnStatsChanged, GlobalEvents.OnEntityDamaged
    /// </summary>
    [RequireComponent(typeof(EquipmentManager))]
    public class PlayerStatManager : MonoBehaviour
    {
        public static event Action<PlayerStatManager> OnManagerCreated;
        public event Action<int, int, int, int, float, int> OnStatsChanged;

        [Header("Base Stats")]
        [SerializeField] private int _baseHp = 100;
        [SerializeField] private int _baseMp = 50;
        [SerializeField] private int _baseAttack = 10;
        [SerializeField] private int _baseDefense = 5;
        [SerializeField] private float _baseSpeed = 10f; // 이동 속도
        [SerializeField] private float _baseAttackSpeed = 1f; // 초당 공격 횟수
        [SerializeField] private int _baseRange = 1;

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

        private void Awake()
        {
            _equipmentManager = GetComponent<EquipmentManager>();
            _inventoryManager = GetComponent<InventoryManager>();
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
        }

        private void Start()
        {
            CalculateStats();
        }

        private void CalculateStats()
        {
            var eqStats = _equipmentManager != null
                ? _equipmentManager.GetTotalStats()
                : (hp: 0, mp: 0, atk: 0, def: 0, speed: 0f, range: 0, atkSpeedBonus: 0f, weaponBaseAtkSpeed: 0f);

            MaxHp = Mathf.Max(1, _baseHp + eqStats.hp);
            MaxMp = Mathf.Max(0, _baseMp + eqStats.mp);

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

            CurrentAttack = _baseAttack + eqStats.atk;
            CurrentDefense = _baseDefense + eqStats.def;
            CurrentRange = _baseRange + eqStats.range;

            CurrentSpeed = Mathf.Max(1f, _baseSpeed + eqStats.speed);
            
            // 공격 속도 계산 로직
            // 무기의 고유 공격 속도가 우선순위, 없으면 플레이어 기본 스탯 사용, 이후 악세서리 등 합산
            float baseAtkSpd = eqStats.weaponBaseAtkSpeed > 0f ? eqStats.weaponBaseAtkSpeed : _baseAttackSpeed;
            CurrentAttackSpeed = Mathf.Max(0.1f, baseAtkSpd + eqStats.atkSpeedBonus);

            OnStatsChanged?.Invoke(CurrentHp, CurrentMp, CurrentAttack, CurrentDefense, CurrentSpeed, CurrentRange);
        }

        /// <param name="damage">방어력 경감 전 원본 피해</param>
        /// <returns>방어력 경감 후 실제로 깎인 체력. 사망 상태이거나 피해가 0 이하면 0 (흡혈 등 후속 효과 판정용)</returns>
        public int TakeDamage(int damage)
        {
            if (_isDead || damage <= 0) return 0;

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

            var renderer = GetComponentInChildren<SpriteRenderer>();
            if (renderer != null)
            {
                StartCoroutine(FlashRedRoutine(renderer));
            }
            return dealt;
        }

        private void HandleDeath()
        {
            if (_isDead) return;
            _isDead = true;

            Debug.Log("[Player] 사망! 인벤토리 및 장착 장비가 모두 증발하고 허브로 복귀합니다.");

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

        private System.Collections.IEnumerator FlashRedRoutine(SpriteRenderer renderer)
        {
            Color original = renderer.color;
            renderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            renderer.color = original;
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
