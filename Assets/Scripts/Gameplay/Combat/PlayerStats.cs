// File: Assets/Scripts/Gameplay/Combat/PlayerStats.cs
using System;
using UnityEngine;
using DimensionLooter.Core;

namespace DimensionLooter.Gameplay.Combat
{
    /// <summary>
    /// 플레이어의 체력, 마나, 스피드 등 핵심 스탯을 관리합니다.
    /// </summary>
    public class PlayerStats : MonoBehaviour, IDamageable
    {
        [Header("Health Settings")]
        [SerializeField] private int _maxHealth = 100;
        
        [Header("Mana Settings")]
        [SerializeField] private int _maxMana = 100;

        [Header("Combat Stats")]
        [Tooltip("기본 공격력")]
        [SerializeField] private int _baseAttackPower = 10;
        [Tooltip("기본 방어력")]
        [SerializeField] private int _baseDefensePower = 0;
        [Tooltip("행동 게이지 차오르는 속도")]
        [SerializeField] private float _speed = 50f; 

        private int _currentHealth;
        private int _currentMana;
        
        // 장비로 인한 추가 스탯
        private int _gearAttackPower = 0;
        private int _gearDefensePower = 0;

        public bool IsAlive => _currentHealth > 0;
        public int CurrentHealth => _currentHealth;
        public int MaxHealth => _maxHealth;
        public int CurrentMana => _currentMana;
        public int MaxMana => _maxMana;
        public int AttackPower => _baseAttackPower + _gearAttackPower;
        public int DefensePower => _baseDefensePower + _gearDefensePower;
        public float Speed => _speed;

        // [Observer] 스탯 변경 시 UI 등에서 갱신하도록 이벤트 발송
        public event Action<int, int> OnHealthChanged;
        public event Action<int, int> OnManaChanged;
        public event Action<int, int> OnCombatStatsChanged;
        
        // [Observer] 사망 이벤트
        public event Action OnDied;

        private void Awake()
        {
            _currentHealth = _maxHealth;
            _currentMana = _maxMana; // MVP 테스트용 풀마나 시작
        }

        private void Start()
        {
            if (DimensionLooter.Managers.GearManager.Instance != null)
            {
                DimensionLooter.Managers.GearManager.Instance.OnGearStatsChanged += HandleGearStatsChanged;
            }
        }

        private void OnDestroy()
        {
            if (DimensionLooter.Managers.GearManager.Instance != null)
            {
                DimensionLooter.Managers.GearManager.Instance.OnGearStatsChanged -= HandleGearStatsChanged;
            }
        }

        private void HandleGearStatsChanged(int addedAtk, int addedDef)
        {
            _gearAttackPower = addedAtk;
            _gearDefensePower = addedDef;
            Debug.Log($"[PlayerStats] 장비 스탯 반영됨: ATK +{addedAtk}, DEF +{addedDef}");
            OnCombatStatsChanged?.Invoke(AttackPower, DefensePower);
        }

        public void TakeDamage(int amount)
        {
            if (!IsAlive) return;

            _currentHealth = Mathf.Clamp(_currentHealth - amount, 0, _maxHealth);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            if (!IsAlive)
            {
                Debug.Log("[PlayerStats] 플레이어 사망!", this);
                OnDied?.Invoke();
                
                if (DimensionLooter.Managers.GameManager.Instance != null)
                {
                    DimensionLooter.Managers.GameManager.Instance.OnPlayerDeath();
                }
            }
        }

        public void Heal(int amount)
        {
            if (!IsAlive) return;

            _currentHealth = Mathf.Clamp(_currentHealth + amount, 0, _maxHealth);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            Debug.Log($"[PlayerStats] 플레이어 회복: {amount}. 현재 체력: {_currentHealth}");
        }

        public bool ConsumeMana(int amount)
        {
            if (_currentMana >= amount)
            {
                _currentMana -= amount;
                OnManaChanged?.Invoke(_currentMana, _maxMana);
                return true;
            }
            return false;
        }

        public void RestoreMana(int amount)
        {
            _currentMana = Mathf.Clamp(_currentMana + amount, 0, _maxMana);
            OnManaChanged?.Invoke(_currentMana, _maxMana);
        }
    }
}
