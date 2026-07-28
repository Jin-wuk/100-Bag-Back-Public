// File: Assets/Scripts/Gameplay/Combat/MonsterHealth.cs
using System;
using UnityEngine;
using DimensionLooter.Core;

namespace DimensionLooter.Gameplay.Combat
{
    /// <summary>
    /// 몬스터의 체력을 관리하며, IDamageable을 구현합니다.
    /// [Why] 몬스터마다 체력이 다르므로 Init 메서드를 통해 DataManager에서 로드한 스탯을 주입받습니다.
    /// </summary>
    public class MonsterHealth : MonoBehaviour, IDamageable
    {
        private int _maxHealth = 20;
        private int _currentHealth;

        public bool IsAlive => _currentHealth > 0;
        public int CurrentHealth => _currentHealth;

        public event Action<int, int> OnHealthChanged;
        
        // 몬스터는 사망 시 자신을 넘겨주어 CombatManager나 PoolManager가 처리하게 돕습니다.
        public event Action<GameObject> OnDied;

        /// <summary>
        /// 몬스터 스폰 시 스탯 데이터를 기반으로 체력을 초기화합니다.
        /// </summary>
        public void Init(int maxHp)
        {
            _maxHealth = maxHp;
            _currentHealth = _maxHealth;
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        public void TakeDamage(int amount)
        {
            if (!IsAlive) return;

            _currentHealth = Mathf.Clamp(_currentHealth - amount, 0, _maxHealth);
            
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            if (!IsAlive)
            {
                OnDied?.Invoke(gameObject);
            }
        }
    }
}
