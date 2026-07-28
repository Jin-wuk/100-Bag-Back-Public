// File: Assets/Scripts/Gameplay/Combat/MonsterHealth.cs
using System;
using UnityEngine;
using DimensionLooter.Core;
using DimensionLooter.UI; // TooltipUI 접근용
using DimensionLooter.Managers;

namespace DimensionLooter.Gameplay.Combat
{
    /// <summary>
    /// 몬스터의 체력을 관리하며, IDamageable과 IInteractable을 구현합니다.
    /// [Why] 몬스터마다 체력이 다르므로 Init 메서드를 통해 DataManager에서 로드한 스탯을 주입받습니다.
    /// </summary>
    public class MonsterHealth : MonoBehaviour, IDamageable, IInteractable
    {
        private int _maxHealth = 20;
        private int _currentHealth;

        public bool IsAlive => _currentHealth > 0;
        public int CurrentHealth => _currentHealth;
        public int MaxHealth => _maxHealth;

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

        // --- 상호작용 및 UI 연출 ---

        public void Interact(GameObject interactor)
        {
            Debug.Log($"[MonsterHealth] 플레이어와 상호작용 성공! 전투 팝업 열기 시작!");
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.BeginCombat(this);
            }
        }

        private void OnMouseEnter()
        {
            // 마우스 호버링 시 기획에 따라 몬스터 스탯 노출 (능동적 판단 유도)
            if (TooltipUI.Instance != null && IsAlive)
            {
                string stats = $"[미지의 몬스터]\n체력: {_currentHealth} / {_maxHealth}\n클릭하여 전투 진입";
                TooltipUI.Instance.Show(stats);
            }
        }

        private void OnMouseExit()
        {
            if (TooltipUI.Instance != null)
            {
                TooltipUI.Instance.Hide();
            }
        }
    }
}
