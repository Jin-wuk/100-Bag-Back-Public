using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using BagBack100.Data.Core;

namespace BagBack100.Player
{
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
            if (_inventoryManager != null)
            {
                _inventoryManager.OnInventoryChanged += CalculateStats;
            }
        }

        private void OnDisable()
        {
            if (_equipmentManager != null)
            {
                _equipmentManager.OnEquipmentChanged -= CalculateStats;
            }
            if (_inventoryManager != null)
            {
                _inventoryManager.OnInventoryChanged -= CalculateStats;
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

            float speedPenalty = 0f;
            if (_inventoryManager != null && _inventoryManager.CurrentWeight > _inventoryManager.MaxWeight)
            {
                speedPenalty = (_inventoryManager.CurrentWeight - _inventoryManager.MaxWeight) * 0.5f;
            }
            CurrentSpeed = Mathf.Max(1f, (_baseSpeed + eqStats.speed) - speedPenalty);
            
            // 공격 속도 계산 로직
            // 무기의 고유 공격 속도가 우선순위, 없으면 플레이어 기본 스탯 사용, 이후 악세서리 등 합산
            float baseAtkSpd = eqStats.weaponBaseAtkSpeed > 0f ? eqStats.weaponBaseAtkSpeed : _baseAttackSpeed;
            CurrentAttackSpeed = Mathf.Max(0.1f, baseAtkSpd + eqStats.atkSpeedBonus);

            OnStatsChanged?.Invoke(CurrentHp, CurrentMp, CurrentAttack, CurrentDefense, CurrentSpeed, CurrentRange);
        }

        public void TakeDamage(int damage)
        {
            if (_isDead) return;

            CurrentHp -= damage;
            if (CurrentHp < 0) CurrentHp = 0;
            Debug.Log($"[Player] {damage} 피해를 입음! 남은 HP: {CurrentHp}/{MaxHp}");
            BagBack100.Core.GlobalEvents.OnEntityDamaged?.Invoke(transform.position, damage, false, true);
            OnStatsChanged?.Invoke(CurrentHp, CurrentMp, CurrentAttack, CurrentDefense, CurrentSpeed, CurrentRange);
            
            if (CurrentHp <= 0)
            {
                HandleDeath();
                return;
            }

            var renderer = GetComponentInChildren<SpriteRenderer>();
            if (renderer != null)
            {
                StartCoroutine(FlashRedRoutine(renderer));
            }
        }

        private void HandleDeath()
        {
            if (_isDead) return;
            _isDead = true;

            Debug.Log("[Player] 사망! 인벤토리 및 장착 장비가 모두 증발하고 허브로 복귀합니다.");

            if (_equipmentManager != null)
            {
                _equipmentManager.ClearEquipment();
            }
            if (_inventoryManager != null)
            {
                _inventoryManager.ClearInventory();
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
    }
}
