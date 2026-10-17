using System.Collections.Generic;
using UnityEngine;

namespace BagBack100.Dungeon
{
    public class DungeonChunk : MonoBehaviour
    {
        [Header("Spawn Points")]
        [Tooltip("몬스터 스폰 위치 목록")]
        [SerializeField] private List<Transform> _monsterSpawnPoints = new List<Transform>();
        [Tooltip("함정 스폰 위치 목록")]
        [SerializeField] private List<Transform> _trapSpawnPoints = new List<Transform>();

        [Header("Special Points")]
        [Tooltip("플레이어 시작 지점으로 사용할 Transform")]
        [SerializeField] private Transform _playerSpawnPoint;
        [Tooltip("단일 탈출구 지점 (후보 목록이 비어있을 때 사용)")]
        [SerializeField] private Transform _exitPoint;
        [Tooltip("탈출구 후보 지점 목록 (통짜맵 또는 다중 후보 지원)")]
        [SerializeField] private List<Transform> _exitCandidatePoints = new List<Transform>();

        public Transform PlayerSpawnPoint => _playerSpawnPoint;
        public Transform ExitPoint => _exitPoint;
        public List<Transform> ExitCandidatePoints => _exitCandidatePoints;
        public int MonsterSpawnPointCount => _monsterSpawnPoints != null ? _monsterSpawnPoints.Count : 0;
        public int TrapSpawnPointCount => _trapSpawnPoints != null ? _trapSpawnPoints.Count : 0;

        public void Initialize(int monsterCount, int trapCount, GameObject monsterPrefab, GameObject trapPrefab, List<GameObject> monsterPool = null)
        {
            // 스폰/탈출구는 우선 비활성화. Generator가 선택한 것만 나중에 활성화
            if (_playerSpawnPoint != null) _playerSpawnPoint.gameObject.SetActive(false);
            if (_exitPoint != null) _exitPoint.gameObject.SetActive(false);
            foreach (var exit in _exitCandidatePoints)
            {
                if (exit != null) exit.gameObject.SetActive(false);
            }

            // 몬스터 및 함정 지정된 개수만큼 무작위 스폰
            SpawnRandomly(_monsterSpawnPoints, monsterCount, monsterPrefab, monsterPool);
            SpawnRandomly(_trapSpawnPoints, trapCount, trapPrefab, null);
        }

        private void SpawnRandomly(List<Transform> spawnPoints, int count, GameObject defaultPrefab, List<GameObject> pool)
        {
            if (spawnPoints == null || spawnPoints.Count == 0) return;

            // 스폰 포인트 유효성 필터링 (null 제외)
            List<Transform> validPoints = new List<Transform>();
            foreach (var pt in spawnPoints)
            {
                if (pt != null) validPoints.Add(pt);
            }
            if (validPoints.Count == 0) return;

            // 리스트 셔플링 (Fisher-Yates 알고리즘)
            for (int i = 0; i < validPoints.Count; i++)
            {
                int randomIndex = Random.Range(i, validPoints.Count);
                Transform temp = validPoints[i];
                validPoints[i] = validPoints[randomIndex];
                validPoints[randomIndex] = temp;
            }

            // count가 0 이하이면 유효 포인트 전체에 스폰, 아니면 min(count, validPoints.Count)만큼 스폰
            int spawnLimit = (count <= 0) ? validPoints.Count : Mathf.Min(count, validPoints.Count);
            
            Grid grid = FindObjectOfType<Grid>();

            for (int i = 0; i < spawnLimit; i++)
            {
                // 프리팹 풀이 지정되어 있으면 풀에서 랜덤 선택, 아니면 기본 프리팹 사용
                GameObject prefabToSpawn = defaultPrefab;
                if (pool != null && pool.Count > 0)
                {
                    prefabToSpawn = pool[Random.Range(0, pool.Count)];
                }

                if (prefabToSpawn == null) continue;

                Vector3 spawnPos = validPoints[i].position;
                if (grid != null)
                {
                    // 타일 중앙 좌표로 자동 스냅
                    spawnPos = grid.GetCellCenterWorld(grid.WorldToCell(spawnPos));
                }

                Instantiate(prefabToSpawn, spawnPos, validPoints[i].rotation, this.transform);
            }
        }

        public void ActivatePlayerSpawn()
        {
            if (_playerSpawnPoint != null)
            {
                _playerSpawnPoint.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 단일 탈출구 지점 활성화 (기존 절차적 청크 호환)
        /// </summary>
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
                    _exitPoint.gameObject.SetActive(true);
                }
            }
        }

        /// <summary>
        /// 플레이어 시작 위치 기준, 가장 먼 N개 후보 중 1곳에 탈출구 생성 (통짜맵 및 다중 후보 지원)
        /// </summary>
        public void ActivateFarthestExitPoint(Vector2 playerSpawnPos, GameObject exitPrefab, int candidateCount = 3)
        {
            List<Transform> allCandidates = new List<Transform>();

            // 후보 목록에 등록된 지점들 추가
            if (_exitCandidatePoints != null && _exitCandidatePoints.Count > 0)
            {
                foreach (var pt in _exitCandidatePoints)
                {
                    if (pt != null) allCandidates.Add(pt);
                }
            }

            // 후보 목록이 비어 있고 단일 _exitPoint가 있다면 fallback 추가
            if (allCandidates.Count == 0 && _exitPoint != null)
            {
                allCandidates.Add(_exitPoint);
            }

            if (allCandidates.Count == 0)
            {
                Debug.LogWarning($"[DungeonChunk] {gameObject.name}에 활성화할 탈출구 후보가 없습니다.");
                return;
            }

            // 플레이어 스폰 위치로부터 먼 순으로 정렬
            allCandidates.Sort((a, b) =>
                Vector2.Distance(playerSpawnPos, b.position).CompareTo(Vector2.Distance(playerSpawnPos, a.position)));

            int pickRange = Mathf.Clamp(candidateCount, 1, allCandidates.Count);
            Transform selectedExit = allCandidates[Random.Range(0, pickRange)];

            Grid grid = FindObjectOfType<Grid>();
            Vector3 exitPos = selectedExit.position;
            if (grid != null)
            {
                exitPos = grid.GetCellCenterWorld(grid.WorldToCell(exitPos));
            }

            if (exitPrefab != null)
            {
                Instantiate(exitPrefab, exitPos, selectedExit.rotation, this.transform);
            }
            else
            {
                selectedExit.gameObject.SetActive(true);
            }
        }
    }
}
