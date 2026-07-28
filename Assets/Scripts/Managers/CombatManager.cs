// File: Assets/Scripts/Managers/CombatManager.cs
using System.Collections;
using UnityEngine;
using DimensionLooter.Gameplay.Combat;
using DimensionLooter.UI;

namespace DimensionLooter.Managers
{
    /// <summary>
    /// 반자동 전투(Auto-Battler) 시스템의 상태와 연산을 총괄하는 매니저입니다.
    /// </summary>
    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance { get; private set; }

        [Header("References")]
        [Tooltip("플레이어 체력 (Inspector 할당)")]
        [SerializeField] private PlayerHealth _playerHealth;
        
        [Tooltip("화면 중앙을 덮는 전투 팝업 패널")]
        [SerializeField] private CombatUI _combatUI;

        [Header("MVP Temporary Base Stats")]
        [SerializeField] private int _playerAtk = 10;
        [SerializeField] private float _playerAtkSpeed = 1.0f; // 초당 1회 공격
        [SerializeField] private int _monsterAtk = 5;
        [SerializeField] private float _monsterAtkSpeed = 1.0f;

        private MonsterHealth _currentMonster;
        private Coroutine _combatCoroutine;
        
        public bool IsInCombat => _currentMonster != null;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        /// <summary>
        /// 몬스터와 사거리 내로 접촉했을 때 1대1 전투 팝업을 엽니다.
        /// </summary>
        public void BeginCombat(MonsterHealth monster)
        {
            if (IsInCombat || !_playerHealth.IsAlive) return;

            _currentMonster = monster;
            _combatUI.ShowCombat(monster);
            
            // 전투 시작 (코루틴 FSM)
            _combatCoroutine = StartCoroutine(CombatRoutine());
            Debug.Log($"[CombatManager] 전투 시작! 적: {monster.name}");
        }

        private IEnumerator CombatRoutine()
        {
            float playerTimer = 0f;
            float monsterTimer = 0f;

            float playerCooldown = 1.0f / _playerAtkSpeed;
            float monsterCooldown = 1.0f / _monsterAtkSpeed;

            while (IsInCombat && _playerHealth.IsAlive && _currentMonster.IsAlive)
            {
                // GDD 명세에 따라 0.1초 틱 단위 연산이 이상적이나, MVP에서는 프레임 단위 처리
                yield return null; 
                
                playerTimer += Time.deltaTime;
                monsterTimer += Time.deltaTime;

                if (playerTimer >= playerCooldown)
                {
                    playerTimer -= playerCooldown;
                    _currentMonster.TakeDamage(_playerAtk);
                    Debug.Log($"[CombatManager] 플레이어 -> 몬스터 공격 ({_playerAtk} 데미지)");
                    if (!_currentMonster.IsAlive) break;
                }

                if (monsterTimer >= monsterCooldown)
                {
                    monsterTimer -= monsterCooldown;
                    _playerHealth.TakeDamage(_monsterAtk);
                    Debug.Log($"[CombatManager] 몬스터 -> 플레이어 공격 ({_monsterAtk} 데미지)");
                    if (!_playerHealth.IsAlive) break;
                }
            }

            // 승리 또는 패배 시 팝업 닫기
            EndCombat();
        }

        /// <summary>
        /// HUD의 도주 버튼 클릭 시 호출됩니다.
        /// </summary>
        public void Flee()
        {
            if (!IsInCombat) return;

            Debug.Log("[CombatManager] 도주 버튼 클릭! 몬스터 체력이 100% 리셋됩니다.");
            
            // GDD 페널티: 도주 시 몬스터 체력 리셋
            _currentMonster.Init(_currentMonster.MaxHealth);
            
            EndCombat();
        }

        private void EndCombat()
        {
            if (_combatCoroutine != null)
            {
                StopCoroutine(_combatCoroutine);
                _combatCoroutine = null;
            }

            _currentMonster = null;
            if (_combatUI != null) _combatUI.HideCombat();
        }
    }
}
