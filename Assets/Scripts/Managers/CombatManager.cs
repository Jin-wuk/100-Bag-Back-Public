// File: Assets/Scripts/Managers/CombatManager.cs
using UnityEngine;
using DimensionLooter.Data;
using DimensionLooter.Gameplay.Combat;

namespace DimensionLooter.Managers
{
    /// <summary>
    /// 반자동 전투(Auto-Battler) 시스템의 웨이브와 몬스터 스폰을 총괄하는 매니저입니다.
    /// </summary>
    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance { get; private set; }

        [Header("Combat Settings")]
        [Tooltip("몬스터가 소환될 위치 좌표")]
        [SerializeField] private Transform _monsterSpawnPoint;
        
        [Tooltip("임시 몬스터 프리팹 (향후 Addressables로 대체)")]
        [SerializeField] private GameObject _monsterPrefab;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Fail-Fast 방어막
            Debug.Assert(_monsterSpawnPoint != null, "[CombatManager] _monsterSpawnPoint가 할당되지 않았습니다!", this);
            Debug.Assert(_monsterPrefab != null, "[CombatManager] _monsterPrefab이 할당되지 않았습니다!", this);
        }

        /// <summary>
        /// ID를 기반으로 몬스터 데이터를 로드하여 소환합니다.
        /// </summary>
        public void SpawnMonster(int monsterId)
        {
            if (!DataManager.Instance.MonsterDict.TryGetValue(monsterId, out MonsterData data))
            {
                Debug.LogError($"[CombatManager] 몬스터 데이터({monsterId})를 DataManager에서 찾을 수 없습니다.", this);
                return;
            }

            // TODO: 추후 PoolManager.SpawnFromPool로 변경
            GameObject monsterObj = Instantiate(_monsterPrefab, _monsterSpawnPoint.position, Quaternion.identity);
            
            if (monsterObj.TryGetComponent<MonsterHealth>(out var health))
            {
                health.Init(data.MaxHealth);
                // 구독해제는 MonsterHealth 자체나 사망 시 매니저가 담당
                health.OnDied += HandleMonsterDeath; 
            }
            
            Debug.Log($"[CombatManager] {data.Name} 소환 완료!", this);
        }

        private void HandleMonsterDeath(GameObject deadMonster)
        {
            Debug.Log($"[CombatManager] 몬스터 처치! 보상 지급 및 다음 웨이브 준비", this);
            
            if (deadMonster.TryGetComponent<MonsterHealth>(out var health))
            {
                health.OnDied -= HandleMonsterDeath;
            }
            
            // TODO: PoolManager.ReturnToPool로 변경
            Destroy(deadMonster);
        }
    }
}
