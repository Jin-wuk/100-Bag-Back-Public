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
        [Header("Stats")]
        [SerializeField] private DimensionLooter.Data.MonsterData _data;

        private int _currentHealth;

        public bool IsAlive => _currentHealth > 0;
        public int CurrentHealth => _currentHealth;
        public int MaxHealth => _data != null ? _data.MaxHealth : 20;
        public int AttackPower => _data != null ? _data.AttackPower : 5;
        public float Speed => _data != null ? _data.AttackSpeed : 40f;
        public DimensionLooter.Data.MonsterData Data => _data;

        public event Action<int, int> OnHealthChanged;
        
        // 몬스터는 사망 시 자신을 넘겨주어 CombatManager나 PoolManager가 처리하게 돕습니다.
        public event Action<GameObject> OnDied;

        private void Start()
        {
            // 에디터에 배치된 더미 몬스터가 Play 모드 진입 시 체력이 0으로 시작하는 것을 방지
            if (_currentHealth <= 0 && _data != null)
            {
                Init(_data);
            }
            else if (_currentHealth <= 0)
            {
                // 더미 데이터 초기화
                Init(new DimensionLooter.Data.MonsterData { MaxHealth = 20, AttackPower = 5, AttackSpeed = 40f, Name = "더미 몬스터" });
            }
        }

        /// <summary>
        /// 몬스터 스폰 시 스탯 데이터를 기반으로 체력을 초기화합니다.
        /// </summary>
        public void Init(DimensionLooter.Data.MonsterData data)
        {
            _data = data;
            _currentHealth = MaxHealth;
            OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
        }

        // 재시작 용도
        public void Init(int maxHp)
        {
            if (_data == null) _data = new DimensionLooter.Data.MonsterData { MaxHealth = maxHp, AttackPower = 5, AttackSpeed = 40f };
            else _data.MaxHealth = maxHp;
            _currentHealth = maxHp;
            OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
        }

        public void TakeDamage(int amount)
        {
            if (!IsAlive) return;

            _currentHealth = Mathf.Clamp(_currentHealth - amount, 0, MaxHealth);
            
            OnHealthChanged?.Invoke(_currentHealth, MaxHealth);

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
            if (TooltipUI.Instance != null && IsAlive)
            {
                string monsterName = _data != null && !string.IsNullOrEmpty(_data.Name) ? _data.Name : "미지의 몬스터";
                string stats = $"[{monsterName}]\n체력: {_currentHealth} / {MaxHealth}\n클릭하여 전투 진입";
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
