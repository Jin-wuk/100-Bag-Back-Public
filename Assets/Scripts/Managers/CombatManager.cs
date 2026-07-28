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
        [Tooltip("플레이어 스탯 (Inspector 할당)")]
        [SerializeField] private PlayerStats _playerStats;
        
        [Tooltip("화면 중앙을 덮는 전투 팝업 패널")]
        [SerializeField] private CombatUI _combatUI;

        private MonsterHealth _currentMonster;
        private Coroutine _combatCoroutine;
        private Vector3 _lastCombatPosition = new Vector3(3, 3, 0);
        
        // 전투 내부 상태
        private float _playerGauge = 0f;
        private float _monsterGauge = 0f;
        private float _monsterCastTimer = 0f;
        private float _monsterCastDuration = 3f;
        private bool _isMonsterCasting = false;
        
        public bool IsInCombat => _currentMonster != null;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void BeginCombat(MonsterHealth monster)
        {
            if (IsInCombat || !_playerStats.IsAlive) return;

            _currentMonster = monster;
            _lastCombatPosition = monster.transform.position;
            
            // 게이지 초기화
            _playerGauge = 0f;
            _monsterGauge = 0f;
            _isMonsterCasting = false;
            _monsterCastTimer = 0f;

            _combatUI.ShowCombat(monster);
            
            _combatCoroutine = StartCoroutine(CombatRoutine());
            Debug.Log($"[CombatManager] 전투 시작! 적: {monster.name}");
        }

        private IEnumerator CombatRoutine()
        {
            float maxGauge = 100f;

            while (IsInCombat && _playerStats.IsAlive && _currentMonster.IsAlive)
            {
                yield return null; 
                
                // 플레이어 ATB
                _playerGauge += _playerStats.Speed * Time.deltaTime;
                if (_playerGauge >= maxGauge)
                {
                    _playerGauge -= maxGauge;
                    _currentMonster.TakeDamage(_playerStats.AttackPower);
                    Debug.Log($"[CombatManager] 플레이어 일반 공격 ({_playerStats.AttackPower} 데미지)");
                    if (!_currentMonster.IsAlive) break;
                }

                // 몬스터 ATB 및 캐스팅
                if (_isMonsterCasting)
                {
                    _monsterCastTimer += Time.deltaTime;
                    _combatUI.UpdateMonsterCastBar(_monsterCastTimer / _monsterCastDuration);

                    if (_monsterCastTimer >= _monsterCastDuration)
                    {
                        // 특수 패턴 발동
                        _isMonsterCasting = false;
                        _combatUI.UpdateMonsterCastBar(0f);
                        int heavyDamage = _currentMonster.AttackPower * 3;
                        _playerStats.TakeDamage(heavyDamage);
                        Debug.Log($"[CombatManager] 몬스터 특수 패턴 작렬! ({heavyDamage} 데미지)");
                        if (!_playerStats.IsAlive) break;
                    }
                }
                else
                {
                    _monsterGauge += _currentMonster.Speed * Time.deltaTime;
                    if (_monsterGauge >= maxGauge)
                    {
                        _monsterGauge -= maxGauge;
                        
                        // 30% 확률로 특수 패턴 진입
                        if (Random.value < 0.3f)
                        {
                            _isMonsterCasting = true;
                            _monsterCastTimer = 0f;
                            Debug.Log($"[CombatManager] 몬스터 강공격 준비 중!");
                        }
                        else
                        {
                            _playerStats.TakeDamage(_currentMonster.AttackPower);
                            Debug.Log($"[CombatManager] 몬스터 일반 공격 ({_currentMonster.AttackPower} 데미지)");
                            if (!_playerStats.IsAlive) break;
                        }
                    }
                }
            }

            bool isVictory = _currentMonster != null && !_currentMonster.IsAlive;
            GameObject deadMonsterObj = _currentMonster != null ? _currentMonster.gameObject : null;
            
            _currentMonster = null;
            if (_combatUI != null) _combatUI.HideCombat();

            if (isVictory)
            {
                Debug.Log("[CombatManager] 전투 승리!");
                if (deadMonsterObj != null) Destroy(deadMonsterObj);

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

        // --- 플레이어 스킬 로직 ---

        public void UseSkillStun()
        {
            if (!IsInCombat || !_playerStats.IsAlive) return;

            int manaCost = 30;
            if (_playerStats.ConsumeMana(manaCost))
            {
                Debug.Log($"[CombatManager] 기절 스킬 사용! (MP -{manaCost})");
                if (_isMonsterCasting)
                {
                    Debug.Log($"[CombatManager] 몬스터의 캐스팅을 끊었습니다! (인터럽트 성공)");
                    _isMonsterCasting = false;
                    _monsterCastTimer = 0f;
                    _combatUI.UpdateMonsterCastBar(0f);
                    
                    // 기절 데미지 (옵션)
                    _currentMonster.TakeDamage(_playerStats.AttackPower);
                }
                else
                {
                    Debug.Log($"[CombatManager] 몬스터 캐스팅 중이 아니어서 큰 의미는 없었지만 약간의 데미지 발동.");
                    _currentMonster.TakeDamage(_playerStats.AttackPower);
                }
            }
            else
            {
                Debug.Log($"[CombatManager] 마나가 부족하여 스킬을 사용할 수 없습니다.");
            }
        }

        public void UseSkillHeal()
        {
            if (!IsInCombat || !_playerStats.IsAlive) return;

            int manaCost = 20;
            if (_playerStats.ConsumeMana(manaCost))
            {
                int healAmount = 30;
                Debug.Log($"[CombatManager] 회복 스킬 사용! (MP -{manaCost})");
                _playerStats.Heal(healAmount);
            }
            else
            {
                Debug.Log($"[CombatManager] 마나가 부족하여 스킬을 사용할 수 없습니다.");
            }
        }

        public void Flee()
        {
            if (!IsInCombat) return;

            Debug.Log("[CombatManager] 도주 버튼 클릭! 몬스터 체력이 100% 리셋됩니다.");
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
            GameObject pouchPrefab = Resources.Load<GameObject>("Prefabs/Pouch");
            if (pouchPrefab != null)
            {
                Vector3 spawnPos = _lastCombatPosition;
                GameObject pouch = Instantiate(pouchPrefab, spawnPos, Quaternion.identity);
                var interactable = pouch.GetComponent<DimensionLooter.Gameplay.Inventory.PouchInteractable>();
                if (interactable != null)
                {
                    interactable.Init(leftovers);
                }
            }
        }
    }
}
