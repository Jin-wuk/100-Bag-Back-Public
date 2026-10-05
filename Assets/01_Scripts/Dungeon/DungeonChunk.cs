using System.Collections.Generic;
using UnityEngine;

namespace BagBack100.Dungeon
{
    public class DungeonChunk : MonoBehaviour
    {
        [Header("Spawn Points")]
        [SerializeField] private List<Transform> _monsterSpawnPoints = new List<Transform>();
        [SerializeField] private List<Transform> _trapSpawnPoints = new List<Transform>();

        [Header("Special Points")]
        [Tooltip("플레이어 시작 지점으로 사용할 Transform")]
        [SerializeField] private Transform _playerSpawnPoint;
        [Tooltip("탈출구로 사용할 Transform")]
        [SerializeField] private Transform _exitPoint;

        public Transform PlayerSpawnPoint => _playerSpawnPoint;
        public Transform ExitPoint => _exitPoint;

        public void Initialize(int monsterCount, int trapCount, GameObject monsterPrefab, GameObject trapPrefab)
        {
            // 스폰/탈출구는 우선 비활성화. Generator가 선택한 딱 1개씩만 나중에 켜짐.
            if (_playerSpawnPoint != null) _playerSpawnPoint.gameObject.SetActive(false);
            if (_exitPoint != null) _exitPoint.gameObject.SetActive(false);

            // 몬스터 및 함정 지정된 개수만큼 무작위 스폰
            SpawnRandomly(_monsterSpawnPoints, monsterCount, monsterPrefab);
            SpawnRandomly(_trapSpawnPoints, trapCount, trapPrefab);
        }

        private void SpawnRandomly(List<Transform> spawnPoints, int count, GameObject prefabToSpawn)
        {
            if (spawnPoints == null || spawnPoints.Count == 0) return;
            if (prefabToSpawn == null) return; 

            // 리스트 셔플링 (Fisher-Yates 알고리즘)
            List<Transform> shuffledPoints = new List<Transform>(spawnPoints);
            for (int i = 0; i < shuffledPoints.Count; i++)
            {
                int randomIndex = Random.Range(i, shuffledPoints.Count);
                Transform temp = shuffledPoints[i];
                shuffledPoints[i] = shuffledPoints[randomIndex];
                shuffledPoints[randomIndex] = temp;
            }

            // 요청된 카운트와 포인트의 개수 중 작은 값만큼 스폰 (포인트 초과 방지)
            int spawnLimit = Mathf.Min(count, shuffledPoints.Count);
            for (int i = 0; i < spawnLimit; i++)
            {
                // 실제 스폰 처리 
                // 향후 퍼포먼스 향상을 위해 Object Pooling으로 수정(Refactor)될 수 있음.
                Instantiate(prefabToSpawn, shuffledPoints[i].position, shuffledPoints[i].rotation, this.transform);
            }
        }

        public void ActivatePlayerSpawn()
        {
            if (_playerSpawnPoint != null)
            {
                _playerSpawnPoint.gameObject.SetActive(true);
            }
        }

        public void ActivateExitPoint(GameObject exitPrefab = null)
        {
            if (_exitPoint != null)
            {
                if (exitPrefab != null)
                {
                    Instantiate(exitPrefab, _exitPoint.position, _exitPoint.rotation, this.transform);
                }
                else
                {
                    // 프리팹이 명시되지 않았다면 기존 방식대로 트랜스폼 자체를 활성화 (Fallback)
                    _exitPoint.gameObject.SetActive(true);
                }
            }
        }
    }
}
