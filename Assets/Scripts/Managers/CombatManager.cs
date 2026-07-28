// File: Assets/Scripts/Managers/CombatManager.cs
using System.Collections;
using UnityEngine;
using DimensionLooter.Gameplay.Combat;
using DimensionLooter.UI;
using DimensionLooter.UI.Inventory;
using DimensionLooter.Data;
using System.Collections.Generic;

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

            bool isVictory = _currentMonster != null && !_currentMonster.IsAlive;
            
            // 코루틴 내부에서 EndCombat을 부르면 StopCoroutine이 실행되어 이 아래 로직이 무시되는 버그 수정
            _currentMonster = null;
            if (_combatUI != null) _combatUI.HideCombat();

            if (isVictory)
            {
                Debug.Log("[CombatManager] 전투 승리! 듀얼 보상 UI를 엽니다.");
                List<ItemData> dummyDrops = new List<ItemData>()
                {
                    new ItemData { Id = 1, Name = "초보자의 검(1x2)", Width = 1, Height = 2 },
                    new ItemData { Id = 2, Name = "가죽 투구(2x2)", Width = 2, Height = 2 },
                    new ItemData { Id = 3, Name = "물약(1x1)", Width = 1, Height = 1 }
                };
                if (DualUIPanel.Instance != null)
                {
                    DualUIPanel.Instance.Show(dummyDrops);
                }
            }
            
            _combatCoroutine = null;
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

        public void SpawnPouch(List<ItemData> leftovers)
        {
            Debug.Log($"[CombatManager] 남은 전리품 {leftovers.Count}개를 포함한 파우치(Pouch)가 바닥에 떨어졌습니다!");
            
            GameObject pouchPrefab = Resources.Load<GameObject>("Prefabs/Pouch");
            if (pouchPrefab != null)
            {
                // 이전 몬스터가 죽은 위치(또는 플레이어 근처)에 스폰
                Vector3 spawnPos = _currentMonster != null ? _currentMonster.transform.position : new Vector3(3, 3, 0);
                GameObject pouch = Instantiate(pouchPrefab, spawnPos, Quaternion.identity);
                var interactable = pouch.GetComponent<DimensionLooter.Gameplay.Inventory.PouchInteractable>();
                if (interactable != null)
                {
                    interactable.Init(leftovers);
                }
            }
            else
            {
                Debug.LogError("[CombatManager] Pouch 프리팹을 찾을 수 없습니다. (Resources/Prefabs/Pouch.prefab)");
            }
        }
    }
}
