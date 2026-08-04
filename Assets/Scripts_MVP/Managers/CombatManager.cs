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
        
        // UI 참조 제거, 이벤트 방식으로 변경 (Strict Decoupling)
        public event System.Action<MonsterHealth> OnCombatBegun;
        public event System.Action OnCombatEnded;
        public event System.Action<int, float, float> OnCombatStateUpdated;
        public event System.Action<float> OnMonsterCastUpdated;

        private MonsterHealth _currentMonster;
        private Coroutine _combatCoroutine;
        private Vector3 _lastCombatPosition = new Vector3(3, 3, 0);
        private Vector3 _lastMonsterPosition = Vector3.zero;
        
        // 전투 내부 상태
        private int _currentDistance = 5; // 임시 시작 거리 5칸
        private float _playerGauge = 0f;
        private float _monsterGauge = 0f;
        private float _monsterCastTimer = 0f;
        private float _monsterCastDuration = 3f;
        private bool _isMonsterCasting = false;
        
        private Dictionary<string, float> _skillNextAvailableTime = new Dictionary<string, float>();
        
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
            _lastCombatPosition = _playerStats.transform.position; // 플레이어의 원래 위치 저장
            _lastMonsterPosition = monster.transform.position; // 몬스터의 원래 위치 저장
            
            // 전투 필드(0,0,0)로 이동
            _playerStats.transform.position = new Vector3(-2, 0, 0);
            monster.transform.position = new Vector3(2, 0, 0);
            
            // 카메라 강제 이동 및 추적 중지
            var camFollow = Camera.main.GetComponent<Gameplay.CameraFollow>();
            if (camFollow != null) camFollow.enabled = false;
            Camera.main.transform.position = new Vector3(0, 0, -10);

            // 다른 UI 패널(가방 등)이 열려있다면 강제 닫기
            if (DimensionLooter.UI.Inventory.DualUIPanel.Instance != null)
            {
                DimensionLooter.UI.Inventory.DualUIPanel.Instance.ClosePanel();
            }

            // 게이지 및 상태 초기화
            _currentDistance = 5;
            _playerGauge = 0f;
            _monsterGauge = 0f;
            _isMonsterCasting = false;
            _monsterCastTimer = 0f;

            OnCombatBegun?.Invoke(monster);
            
            _combatCoroutine = StartCoroutine(CombatRoutine());
            Debug.Log($"[CombatManager] 전투 시작! 적: {monster.name}");
        }

        private IEnumerator CombatRoutine()
        {
            float maxGauge = 100f;

            while (IsInCombat && _playerStats.IsAlive && _currentMonster.IsAlive)
            {
                yield return null; 
                
                // UI 갱신 (거리, 게이지 비율)
                OnCombatStateUpdated?.Invoke(_currentDistance, _playerGauge / maxGauge, _monsterGauge / maxGauge);

                // 플레이어 ATB 진행
                _playerGauge += _playerStats.Speed * Time.deltaTime;
                if (_playerGauge >= maxGauge)
                {
                    _playerGauge -= maxGauge; // 초과분 이월 (ATB 특성)

                    if (_currentDistance > _playerStats.Range)
                    {
                        _currentDistance--;
                        Debug.Log($"[CombatManager] 플레이어 전진! (현재 거리: {_currentDistance})");
                    }
                    else
                    {
                        _currentMonster.TakeDamage(_playerStats.AttackPower);
                        Debug.Log($"[CombatManager] 플레이어 일반 공격 ({_playerStats.AttackPower} 데미지)");
                    }
                    
                    if (!_currentMonster.IsAlive) break;
                }

                // 몬스터 ATB 및 캐스팅 진행
                if (_isMonsterCasting)
                {
                    _monsterCastTimer += Time.deltaTime;
                    OnMonsterCastUpdated?.Invoke(_monsterCastTimer / _monsterCastDuration);

                    if (_monsterCastTimer >= _monsterCastDuration)
                    {
                        // 특수 패턴 발동
                        _isMonsterCasting = false;
                        OnMonsterCastUpdated?.Invoke(0f);
                        int multiplier = _currentMonster.Data != null ? _currentMonster.Data.CastDamageMultiplier : 3;
                        int heavyDamage = _currentMonster.AttackPower * multiplier;
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
                        _monsterGauge -= maxGauge; // 초과분 이월
                        
                        int monsterRange = _currentMonster.Data != null ? _currentMonster.Data.Range : 1;
                        if (_currentDistance > monsterRange)
                        {
                            _currentDistance--;
                            Debug.Log($"[CombatManager] 몬스터 전진! (현재 거리: {_currentDistance})");
                        }
                        else
                        {
                            float castProb = _currentMonster.Data != null ? _currentMonster.Data.CastProbability : 0.3f;
                            if (Random.value < castProb)
                            {
                                _isMonsterCasting = true;
                                _monsterCastTimer = 0f;
                                _monsterCastDuration = _currentMonster.Data != null ? _currentMonster.Data.CastDuration : 3f;
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
            }

            bool isVictory = _currentMonster != null && !_currentMonster.IsAlive;
            GameObject deadMonsterObj = _currentMonster != null ? _currentMonster.gameObject : null;
            
            _currentMonster = null;
            OnCombatEnded?.Invoke();

            if (isVictory)
            {
                Debug.Log("[CombatManager] 전투 승리!");
                if (deadMonsterObj != null) Destroy(deadMonsterObj);

                List<ItemInstance> dropList = new List<ItemInstance>();
                if (DataManager.Instance != null && DataManager.Instance.ItemDict.Count > 0)
                {
                    List<ItemData> allItems = new List<ItemData>(DataManager.Instance.ItemDict.Values);
                    int dropCount = Random.Range(1, 4);
                    for (int i = 0; i < dropCount; i++)
                    {
                        dropList.Add(new ItemInstance(allItems[Random.Range(0, allItems.Count)]));
                    }
                }

                if (DimensionLooter.Managers.LootManager.Instance != null && dropList.Count > 0)
                {
                    Vector3 dropPos = _lastMonsterPosition; // 파우치 생성 위치를 몬스터가 있던 곳으로 변경
                    DimensionLooter.Managers.LootManager.Instance.SpawnPouch(dropList, dropPos);
                }
            }
            
            // 전투 종료 후 원래 던전 좌표로 플레이어 복귀
            if (_playerStats != null)
            {
                _playerStats.transform.position = _lastCombatPosition;
            }

            // 카메라 추적 복구
            var camFollow = Camera.main.GetComponent<Gameplay.CameraFollow>();
            if (camFollow != null) camFollow.enabled = true;

            if (_playerStats != null && isVictory)
            {
                _playerStats.DecreaseBuffEncounter();
            }

            _combatCoroutine = null;
        }

        // --- 플레이어 스킬 로직 ---

        public void UseEquippedSkill(DimensionLooter.Core.Define.ItemType gearSlot)
        {
            if (!IsInCombat || !_playerStats.IsAlive) return;

            var equippedItem = GearManager.Instance.GetEquippedItem(gearSlot);
            if (equippedItem == null || string.IsNullOrEmpty(equippedItem.Instance.AssignedSkillId))
            {
                Debug.Log($"[CombatManager] 해당 슬롯({gearSlot}) 장비에 사용할 수 있는 스킬이 없습니다.");
                return;
            }

            string skillId = equippedItem.Instance.AssignedSkillId;
            if (!DataManager.Instance.SkillDict.TryGetValue(skillId, out SkillData skill))
            {
                Debug.LogError($"[CombatManager] 존재하지 않는 스킬 ID입니다: {skillId}");
                return;
            }

            if (_skillNextAvailableTime.TryGetValue(skillId, out float nextTime) && Time.time < nextTime)
            {
                Debug.LogWarning($"[CombatManager] {skill.Name} 스킬 쿨타임 중입니다. ({nextTime - Time.time:F1}초 남음)");
                return;
            }

            // 마나 체크 (쿨타임은 추후 확장 가능)
            if (_playerStats.ConsumeMana(skill.MpCost))
            {
                _skillNextAvailableTime[skillId] = Time.time + skill.Cooldown;
                Debug.Log($"[CombatManager] {skill.Name} 스킬 사용! (MP -{skill.MpCost})");
                
                // 스킬 효과 적용
                switch (skill.EffectType)
                {
                    case DimensionLooter.Core.Define.SkillEffectType.Stun:
                        if (_isMonsterCasting)
                        {
                            Debug.Log($"[CombatManager] 몬스터의 캐스팅을 끊었습니다! (인터럽트 성공)");
                            _isMonsterCasting = false;
                            _monsterCastTimer = 0f;
                            OnMonsterCastUpdated?.Invoke(0f);
                        }
                        else
                        {
                            Debug.Log($"[CombatManager] 몬스터 캐스팅 중이 아니어서 인터럽트는 없었지만 데미지 발동.");
                        }
                        _currentMonster.TakeDamage(_playerStats.AttackPower); // 기본 공격력 추가 피해
                        break;

                    case DimensionLooter.Core.Define.SkillEffectType.Heal:
                        _playerStats.Heal(skill.EffectValue);
                        Debug.Log($"[CombatManager] 체력을 {skill.EffectValue} 회복했습니다.");
                        break;

                    case DimensionLooter.Core.Define.SkillEffectType.Damage_Reduction:
                        Debug.Log($"[CombatManager] {skill.Duration}초간 데미지 감소 {skill.EffectValue}% 적용!");
                        break;

                    case DimensionLooter.Core.Define.SkillEffectType.True_Damage:
                        _currentMonster.TakeDamage(skill.EffectValue);
                        Debug.Log($"[CombatManager] 적에게 {skill.EffectValue}의 고정 피해를 입혔습니다.");
                        break;

                    case DimensionLooter.Core.Define.SkillEffectType.Armor_Break:
                        Debug.Log($"[CombatManager] {skill.Duration}초간 적의 방어력을 0으로 만듭니다.");
                        break;
                }
            }
            else
            {
                Debug.LogWarning($"[CombatManager] 마나가 부족하여 {skill.Name} 스킬을 사용할 수 없습니다. (필요: {skill.MpCost}, 현재: {_playerStats.CurrentMana})");
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

            // 전투 종료 후 원래 던전 좌표로 플레이어 및 몬스터 복귀
            if (_playerStats != null)
            {
                _playerStats.transform.position = _lastCombatPosition;
            }
            if (_currentMonster != null)
            {
                _currentMonster.transform.position = _lastMonsterPosition;
            }

            // 카메라 추적 복구
            var camFollow = Camera.main.GetComponent<Gameplay.CameraFollow>();
            if (camFollow != null) camFollow.enabled = true;

            _currentMonster = null;
            OnCombatEnded?.Invoke();
        }
    }
}
