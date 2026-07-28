// File: Assets/Scripts/Gameplay/Combat/PlayerHealth.cs
using System;
using UnityEngine;
using DimensionLooter.Core;

namespace DimensionLooter.Gameplay.Combat
{
    /// <summary>
    /// 플레이어의 체력을 관리하며, IDamageable을 구현합니다.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Header("Health Settings")]
        [Tooltip("플레이어의 최대 체력")]
        [SerializeField] private int _maxHealth = 100;
        
        private int _currentHealth;

        public bool IsAlive => _currentHealth > 0;
        public int CurrentHealth => _currentHealth;

        // [Observer] 체력 변경 시 UI 등에서 화면을 갱신하도록 이벤트 발송
        public event Action<int, int> OnHealthChanged;
        
        // [Observer] 사망 시 FSM이나 GameManager에서 감지하도록 이벤트 발송
        public event Action OnDied;

        private void Awake()
        {
            _currentHealth = _maxHealth;
        }

        public void TakeDamage(int amount)
        {
            if (!IsAlive) return;

            _currentHealth = Mathf.Clamp(_currentHealth - amount, 0, _maxHealth);
            
            // [Safe Invocation]
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            if (!IsAlive)
            {
                Debug.Log("[PlayerHealth] 플레이어 사망!", this);
                OnDied?.Invoke();
            }
        }
    }
}
