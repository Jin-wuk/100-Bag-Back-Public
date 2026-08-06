using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DimensionLooter.Dungeon.Data;

namespace DimensionLooter.Dungeon
{
    public class DungeonGenerator : MonoBehaviour
    {
        [Header("Dungeon Configuration")]
        [Tooltip("던전 생성 규칙을 담고 있는 ScriptableObject 할당")]
        [SerializeField] private DungeonData _dungeonData;
        
        [Header("Player Settings")]
        [Tooltip("던전 진입 시 스폰할 플레이어 프리팹")]
        [SerializeField] private GameObject _playerPrefab;

        private List<DungeonChunk> _spawnedChunks = new List<DungeonChunk>();

        private void Start()
        {
            if (_dungeonData == null)
            {
                return; // Guard Clause: 의도치 않은 크래시 방지
            }

            GenerateDungeon();
            DetermineStartAndExit();
        }

        private void GenerateDungeon()
        {
            _spawnedChunks.Clear();
            int slotIndex = 0;

            // DungeonData에 설정된 가로/세로 사이즈 기반 그리드 생성 루프
            for (int y = 0; y < _dungeonData.gridHeight; y++)
            {
                for (int x = 0; x < _dungeonData.gridWidth; x++)
                {
                    if (slotIndex >= _dungeonData.chunkSlots.Length) break;

                    var slot = _dungeonData.chunkSlots[slotIndex];
                    if (slot.possibleChunkPrefabs != null && slot.possibleChunkPrefabs.Count > 0)
                    {
                        // 지정된 슬롯에서 프리팹 무작위 선택
                        int randomIndex = Random.Range(0, slot.possibleChunkPrefabs.Count);
                        GameObject selectedPrefab = slot.possibleChunkPrefabs[randomIndex];

                        if (selectedPrefab != null)
                        {
                            Vector2 spawnPosition = new Vector2(x * _dungeonData.chunkOffset.x, y * _dungeonData.chunkOffset.y);
                            GameObject chunkGo = Instantiate(selectedPrefab, spawnPosition, Quaternion.identity, this.transform);
                            
                            if (chunkGo.TryGetComponent<DungeonChunk>(out var chunkComponent))
                            {
                                // 청크 초기화 및 리스트 등록
                                chunkComponent.Initialize(
                                    _dungeonData.monsterCountPerChunk, 
                                    _dungeonData.trapCountPerChunk, 
                                    _dungeonData.monsterPrefab, 
                                    _dungeonData.trapPrefab
                                );
                                _spawnedChunks.Add(chunkComponent);
                            }
                        }
                    }
                    slotIndex++;
                }
            }
        }

        private void DetermineStartAndExit()
        {
            if (_spawnedChunks.Count == 0) return;

            // 1. 완전 무작위로 스폰 지점(청크) 1개 선택
            int spawnIndex = Random.Range(0, _spawnedChunks.Count);
            DungeonChunk spawnChunk = _spawnedChunks[spawnIndex];
            spawnChunk.ActivatePlayerSpawn();

            if (spawnChunk.PlayerSpawnPoint == null) return;

            Vector2 spawnPos = spawnChunk.PlayerSpawnPoint.position;
            
            // 플레이어 프리팹 인스턴스화
            if (_playerPrefab != null)
            {
                Instantiate(_playerPrefab, spawnPos, Quaternion.identity);
            }

            // 2. 본인을 제외한 나머지 청크 중, 탈출구가 할당되어 있는 청크들을 스폰 지점과의 거리를 기준으로 내림차순(먼 순서) 정렬
            var distanceList = _spawnedChunks
                .Where(c => c != spawnChunk && c.ExitPoint != null)
                .OrderByDescending(c => Vector2.Distance(spawnPos, c.ExitPoint.position))
                .ToList();

            if (distanceList.Count > 0)
            {
                // 상위 3개(미만이면 전체) 후보군 추출 후 그 중 랜덤 1개 활성화
                int candidateCount = Mathf.Min(3, distanceList.Count);
                int randomExitIndex = Random.Range(0, candidateCount);
                
                DungeonChunk exitChunk = distanceList[randomExitIndex];
                exitChunk.ActivateExitPoint();
            }
        }
    }
}
