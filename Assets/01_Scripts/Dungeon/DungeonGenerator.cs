using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using BagBack100.Core;
using BagBack100.Dungeon.Data;

namespace BagBack100.Dungeon
{
    public enum DungeonMode
    {
        FixedMap,           // 씬에 직접 배치된 통짜 미로 맵 + 구역(DungeonZone) 기반 (MVP)
        ProceduralChunks    // DungeonData 기반 그리드 청크 조립형 (Post-MVP)
    }

    public class DungeonGenerator : MonoBehaviour
    {
        private static readonly Vector3Int[] CardinalDirs = new Vector3Int[]
        {
            Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left
        };

        [Header("Generation Mode")]
        [Tooltip("던전 생성 방식 (FixedMap: 통짜 미로 맵 / ProceduralChunks: 절차적 청크 조립)")]
        [SerializeField] private DungeonMode _mode = DungeonMode.FixedMap;

        [Header("Tilemap & Collision References (FixedMap)")]
        [Tooltip("바닥 타일맵 (비워두면 'Floor' 또는 'Ground' 이름의 Tilemap 자동 탐색)")]
        [SerializeField] private Tilemap _floorTilemap;
        [Tooltip("벽 타일맵 (비워두면 'Wall' 이름의 Tilemap 자동 탐색)")]
        [SerializeField] private Tilemap _wallTilemap;
        [Tooltip("벽/기둥 등 이동 불가 장애물 물리 레이어")]
        [SerializeField] private LayerMask _obstacleLayerMask;

        [Header("Safety & Spacing Rules")]
        [Tooltip("출발 지점으로부터 걸어서 N칸 이내에는 몬스터/함정 스폰 금지")]
        [SerializeField] private int _startSafeStepRadius = 5;
        [Tooltip("몬스터끼리 뭉치거나 공격 범위가 겹치지 않도록 보장할 최소 타일 간격 (맨해튼 거리)")]
        [SerializeField] private int _minMonsterSpacing = 3;
        [Tooltip("탈출구 후보 구역(ExitZone) 중 출발지에서 가장 먼 상위 N개 중 1곳 랜덤 선택")]
        [SerializeField] private int _farthestExitCandidateCount = 2;

        [Header("Auto-Corridor Spawn (박스 미지정 복도 전체)")]
        [Tooltip("Zone 박스가 안 쳐진 나머지 미로 복도 전체에 랜덤 배치할 몬스터 수")]
        [SerializeField] private int _corridorMonsterCount = 4;
        [Tooltip("Zone 박스가 안 쳐진 나머지 미로 복도 전체에 랜덤 배치할 함정 수")]
        [SerializeField] private int _corridorTrapCount = 2;

        [Header("Prefabs & Pools")]
        [Tooltip("던전 진입 시 스폰할 플레이어 프리팹")]
        [SerializeField] private GameObject _playerPrefab;
        [Tooltip("기본 몬스터 프리팹 풀")]
        [SerializeField] private List<GameObject> _monsterPrefabPool = new List<GameObject>();
        [Tooltip("보물상자/전리품 프리팹 풀")]
        [SerializeField] private List<GameObject> _chestPrefabPool = new List<GameObject>();
        [Tooltip("함정 프리팹 풀")]
        [SerializeField] private List<GameObject> _trapPrefabPool = new List<GameObject>();
        [Tooltip("탈출 포탈 프리팹")]
        [SerializeField] private GameObject _fixedExitPrefab;

        [Header("Legacy / Procedural Configuration")]
        [Tooltip("절차적 청크 모드 또는 기본 프리팹 폴백용 ScriptableObject")]
        [SerializeField] private DungeonData _dungeonData;
        [SerializeField] private DungeonChunk _fixedMapChunk;

        private Grid _grid;
        private List<DungeonChunk> _spawnedChunks = new List<DungeonChunk>();

        public static DungeonGenerator Instance { get; private set; }

        public DungeonMode Mode => _mode;
        public Tilemap FloorTilemap => _floorTilemap;
        public Tilemap WallTilemap => _wallTilemap;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (_grid == null) _grid = FindObjectOfType<Grid>();
            AutoBindTilemapsIfNeeded();
        }

        public bool IsCellWalkable(Vector3Int cell)
        {
            return IsTileWalkable(cell);
        }

        private void Start()
        {
            if (_grid == null) _grid = FindObjectOfType<Grid>();

            if (_mode == DungeonMode.FixedMap)
            {
                SetupFixedMap();
            }
            else
            {
                SetupProceduralDungeon();
            }
        }

        #region Fixed Map Setup (Zone + Flood Fill)

        private void SetupFixedMap()
        {
            AutoBindTilemapsIfNeeded();

            DungeonZone[] zones = FindObjectsOfType<DungeonZone>();
            if (zones != null && zones.Length > 0)
            {
                SetupZoneBasedMap(zones);
                return;
            }

            // 씬에 DungeonZone이 아직 없으면 기존 DungeonChunk 방식으로 폴백
            SetupLegacyChunkFixedMap();
        }

        private void AutoBindTilemapsIfNeeded()
        {
            if (_grid == null) return;
            if (_floorTilemap != null && _wallTilemap != null) return;

            Tilemap[] tilemaps = _grid.GetComponentsInChildren<Tilemap>();
            foreach (var tm in tilemaps)
            {
                string lowerName = tm.gameObject.name.ToLowerInvariant();
                if (_floorTilemap == null && (lowerName.Contains("floor") || lowerName.Contains("ground") || lowerName.Contains("바닥")))
                {
                    _floorTilemap = tm;
                }
                else if (_wallTilemap == null && (lowerName.Contains("wall") || lowerName.Contains("벽")))
                {
                    _wallTilemap = tm;
                }
            }
        }

        private void SetupZoneBasedMap(DungeonZone[] zones)
        {
            if (_grid == null)
            {
                Debug.LogError("[DungeonGenerator] 씬에 Grid가 없습니다.");
                return;
            }

            // 1. 출발 구역(StartZone) 찾기 및 플레이어 시작 타일 확정
            DungeonZone startZone = zones.FirstOrDefault(z => z.ZoneType == DungeonZoneType.Start);
            if (startZone == null)
            {
                Debug.LogError("[DungeonGenerator] Start 타입의 DungeonZone이 씬에 없습니다.");
                return;
            }

            Vector3Int playerStartCell = FindBestStartCell(startZone);
            SpawnPlayerAtCell(playerStartCell);

            // 2. 플레이어 시작 타일로부터 Flood Fill (BFS) 수행 -> 벽 너머 빈 공간 완벽 배제 및 미로 실제 이동 거리 계산
            Dictionary<Vector3Int, int> reachableSteps = BuildReachableTileMap(playerStartCell, 5000);

            HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int> { playerStartCell };
            List<Vector3Int> spawnedMonsterCells = new List<Vector3Int>();
            HashSet<Vector3Int> allZonedCells = new HashSet<Vector3Int>();

            // 구역별 도달 가능 타일 캐싱
            Dictionary<DungeonZone, List<Vector3Int>> zoneValidCells = new Dictionary<DungeonZone, List<Vector3Int>>();
            foreach (var zone in zones)
            {
                List<Vector3Int> rawCells = zone.GetOverlappingCells(_grid);
                List<Vector3Int> valid = new List<Vector3Int>();
                foreach (var c in rawCells)
                {
                    if (reachableSteps.ContainsKey(c))
                    {
                        valid.Add(c);
                        allZonedCells.Add(c);
                    }
                }
                zoneValidCells[zone] = valid;
            }

            // 3. 탈출구(ExitZone) 선정 및 스폰 (실제 미로 걸음 수 기준 가장 먼 곳)
            SpawnExitInFarthestZone(zones, zoneValidCells, reachableSteps, occupiedCells);

            // 4. 보물방(ChestZone), 전투방(CombatZone), 수동 복도(CorridorZone) 내부 엔티티 스폰
            foreach (var zone in zones)
            {
                if (zone.ZoneType == DungeonZoneType.Start || zone.ZoneType == DungeonZoneType.Exit)
                    continue;

                List<Vector3Int> candidates = zoneValidCells[zone];
                if (candidates.Count == 0) continue;

                // 보물상자 스폰
                int chestCount = zone.ChestCount;
                if (chestCount > 0)
                {
                    List<GameObject> chestPool = (zone.OverrideChestPool != null && zone.OverrideChestPool.Count > 0)
                        ? zone.OverrideChestPool
                        : _chestPrefabPool;
                    SpawnEntitiesInCells(candidates, chestCount, chestPool, null, reachableSteps, occupiedCells, null, false);
                }

                // 구역 함정 스폰
                if (zone.TrapCount > 0)
                {
                    GameObject defaultTrap = _dungeonData != null ? _dungeonData.trapPrefab : null;
                    SpawnEntitiesInCells(candidates, zone.TrapCount, _trapPrefabPool, defaultTrap, reachableSteps, occupiedCells, null, true);
                }

                // 구역 몬스터 스폰 (최소 간격 유지)
                if (zone.MonsterCount > 0)
                {
                    List<GameObject> monsterPool = (zone.OverrideMonsterPool != null && zone.OverrideMonsterPool.Count > 0)
                        ? zone.OverrideMonsterPool
                        : _monsterPrefabPool;
                    GameObject defaultMonster = _dungeonData != null ? _dungeonData.monsterPrefab : null;
                    SpawnEntitiesInCells(candidates, zone.MonsterCount, monsterPool, defaultMonster, reachableSteps, occupiedCells, spawnedMonsterCells, true);
                }
            }

            // 5. B안: 구역 박스가 쳐지지 않은 나머지 모든 미로 복도 타일에 몬스터/함정 분산 스폰
            List<Vector3Int> unzonedCorridorCells = new List<Vector3Int>();
            foreach (var kvp in reachableSteps)
            {
                if (!allZonedCells.Contains(kvp.Key) && kvp.Value > _startSafeStepRadius)
                {
                    unzonedCorridorCells.Add(kvp.Key);
                }
            }

            if (unzonedCorridorCells.Count > 0)
            {
                GameObject defaultTrap = _dungeonData != null ? _dungeonData.trapPrefab : null;
                GameObject defaultMonster = _dungeonData != null ? _dungeonData.monsterPrefab : null;

                if (_corridorTrapCount > 0)
                {
                    SpawnEntitiesInCells(unzonedCorridorCells, _corridorTrapCount, _trapPrefabPool, defaultTrap, reachableSteps, occupiedCells, null, true);
                }

                if (_corridorMonsterCount > 0)
                {
                    SpawnEntitiesInCells(unzonedCorridorCells, _corridorMonsterCount, _monsterPrefabPool, defaultMonster, reachableSteps, occupiedCells, spawnedMonsterCells, true);
                }
            }

            InitializeFogOfWar(playerStartCell);
        }

        private void InitializeFogOfWar(Vector3Int playerStartCell)
        {
            FogOfWarSystem fog = FogOfWarSystem.Instance;
            if (fog == null)
            {
                fog = FindObjectOfType<FogOfWarSystem>();
            }
            if (fog == null)
            {
                fog = gameObject.AddComponent<FogOfWarSystem>();
            }
            fog.Initialize(_grid, _floorTilemap, _wallTilemap, _obstacleLayerMask, playerStartCell);
        }

        private Vector3Int FindBestStartCell(DungeonZone startZone)
        {
            List<Vector3Int> cells = startZone.GetOverlappingCells(_grid);
            Vector3Int centerCell = _grid.WorldToCell(startZone.GetWorldBounds().center);
            centerCell.z = 0;

            // 중심에서 가장 가까운 이동 가능 타일 선택
            cells.Sort((a, b) =>
                (Mathf.Abs(a.x - centerCell.x) + Mathf.Abs(a.y - centerCell.y))
                .CompareTo(Mathf.Abs(b.x - centerCell.x) + Mathf.Abs(b.y - centerCell.y)));

            foreach (var c in cells)
            {
                if (IsTileWalkable(c)) return c;
            }

            return centerCell;
        }

        /// <summary>
        /// 플레이어 시작 셀에서 4방향 BFS(Flood Fill)를 수행하여
        /// 벽 안쪽의 실제로 걸어갈 수 있는 타일과 이동 걸음 수를 반환한다.
        /// 벽 뒤의 빈 공간이나 고립된 영역은 탐색이 닿지 않아 100% 자동 배제된다.
        /// </summary>
        private Dictionary<Vector3Int, int> BuildReachableTileMap(Vector3Int startCell, int maxCells)
        {
            Dictionary<Vector3Int, int> distances = new Dictionary<Vector3Int, int>();
            Queue<Vector3Int> queue = new Queue<Vector3Int>();

            startCell.z = 0;
            distances[startCell] = 0;
            queue.Enqueue(startCell);

            while (queue.Count > 0 && distances.Count < maxCells)
            {
                Vector3Int current = queue.Dequeue();
                int nextStep = distances[current] + 1;

                for (int i = 0; i < CardinalDirs.Length; i++)
                {
                    Vector3Int neighbor = current + CardinalDirs[i];
                    if (distances.ContainsKey(neighbor)) continue;

                    if (IsTileWalkable(neighbor))
                    {
                        distances[neighbor] = nextStep;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return distances;
        }

        private bool IsTileWalkable(Vector3Int cell)
        {
            cell.z = 0;

            // 1. 바닥 타일맵이 지정된 경우 바닥 타일이 반드시 존재해야 함
            if (_floorTilemap != null && !_floorTilemap.HasTile(cell))
            {
                return false;
            }

            // 2. 벽 타일맵이 지정된 경우 벽 타일이 없어야 함
            if (_wallTilemap != null && _wallTilemap.HasTile(cell))
            {
                return false;
            }

            // 3. 물리 장애물(기둥, 벽 콜라이더 등)이 없어야 함
            if (_grid != null && _obstacleLayerMask.value != 0)
            {
                if (Pathfinder.IsObstacle(_grid, new Vector2Int(cell.x, cell.y), _obstacleLayerMask))
                {
                    return false;
                }
            }

            return true;
        }

        private void SpawnPlayerAtCell(Vector3Int cell)
        {
            if (_playerPrefab == null || _grid == null) return;
            Vector3 worldPos = _grid.GetCellCenterWorld(cell);
            Instantiate(_playerPrefab, worldPos, Quaternion.identity);
        }

        private void SpawnExitInFarthestZone(
            DungeonZone[] zones,
            Dictionary<DungeonZone, List<Vector3Int>> zoneValidCells,
            Dictionary<Vector3Int, int> reachableSteps,
            HashSet<Vector3Int> occupiedCells)
        {
            var exitCandidates = new List<(DungeonZone zone, Vector3Int bestCell, int stepDist)>();

            foreach (var zone in zones)
            {
                if (zone.ZoneType != DungeonZoneType.Exit) continue;
                List<Vector3Int> valid = zoneValidCells[zone];
                if (valid.Count == 0) continue;

                // 해당 Exit 구역의 중심에서 가장 가까운 유효 타일 선택
                Vector3Int center = _grid.WorldToCell(zone.GetWorldBounds().center);
                center.z = 0;
                valid.Sort((a, b) =>
                    (Mathf.Abs(a.x - center.x) + Mathf.Abs(a.y - center.y))
                    .CompareTo(Mathf.Abs(b.x - center.x) + Mathf.Abs(b.y - center.y)));

                Vector3Int chosenCell = valid[0];
                int dist = reachableSteps[chosenCell];
                exitCandidates.Add((zone, chosenCell, dist));
            }

            if (exitCandidates.Count == 0)
            {
                Debug.LogWarning("[DungeonGenerator] 도달 가능한 Exit 구역이 없습니다.");
                return;
            }

            // 미로 실제 걸음 수 기준 내림차순 정렬 (가장 먼 곳 우선)
            exitCandidates.Sort((a, b) => b.stepDist.CompareTo(a.stepDist));

            int pickRange = Mathf.Clamp(_farthestExitCandidateCount, 1, exitCandidates.Count);
            var selected = exitCandidates[Random.Range(0, pickRange)];

            GameObject exitPrefab = _fixedExitPrefab != null
                ? _fixedExitPrefab
                : (_dungeonData != null ? _dungeonData.exitPrefab : null);

            if (exitPrefab != null)
            {
                Vector3 exitWorldPos = _grid.GetCellCenterWorld(selected.bestCell);
                Instantiate(exitPrefab, exitWorldPos, Quaternion.identity, transform);
            }

            occupiedCells.Add(selected.bestCell);
        }

        private void SpawnEntitiesInCells(
            List<Vector3Int> candidateCells,
            int count,
            List<GameObject> pool,
            GameObject fallbackPrefab,
            Dictionary<Vector3Int, int> reachableSteps,
            HashSet<Vector3Int> occupiedCells,
            List<Vector3Int> spawnedMonsterCells,
            bool applySafeRadius)
        {
            if (candidateCells == null || candidateCells.Count == 0 || count <= 0) return;
            bool hasPool = pool != null && pool.Count > 0;
            if (!hasPool && fallbackPrefab == null) return;

            // 셔플 복사본 생성
            List<Vector3Int> shuffled = new List<Vector3Int>(candidateCells);
            for (int i = 0; i < shuffled.Count; i++)
            {
                int r = Random.Range(i, shuffled.Count);
                Vector3Int tmp = shuffled[i];
                shuffled[i] = shuffled[r];
                shuffled[r] = tmp;
            }

            int spawned = 0;
            foreach (var cell in shuffled)
            {
                if (spawned >= count) break;
                if (occupiedCells.Contains(cell)) continue;

                // 출발지 안전 거리(걸음 수) 이내 스폰 차단
                if (applySafeRadius && reachableSteps.TryGetValue(cell, out int steps) && steps <= _startSafeStepRadius)
                {
                    continue;
                }

                // 몬스터인 경우 기존 스폰된 몬스터들과 최소 거리(_minMonsterSpacing) 유지
                if (spawnedMonsterCells != null && _minMonsterSpacing > 1)
                {
                    bool tooClose = false;
                    for (int i = 0; i < spawnedMonsterCells.Count; i++)
                    {
                        Vector3Int mCell = spawnedMonsterCells[i];
                        int manhattan = Mathf.Abs(cell.x - mCell.x) + Mathf.Abs(cell.y - mCell.y);
                        if (manhattan < _minMonsterSpacing)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose) continue;
                }

                GameObject prefab = hasPool ? pool[Random.Range(0, pool.Count)] : fallbackPrefab;
                if (prefab == null) continue;

                Vector3 worldPos = _grid.GetCellCenterWorld(cell);
                Instantiate(prefab, worldPos, Quaternion.identity, transform);

                occupiedCells.Add(cell);
                if (spawnedMonsterCells != null)
                {
                    spawnedMonsterCells.Add(cell);
                }
                spawned++;
            }
        }

        private void SetupLegacyChunkFixedMap()
        {
            if (_fixedMapChunk == null)
            {
                _fixedMapChunk = FindObjectOfType<DungeonChunk>();
            }

            if (_fixedMapChunk == null)
            {
                Debug.LogError("[DungeonGenerator] 씬에 DungeonZone 또는 DungeonChunk가 없습니다.");
                return;
            }

            GameObject monsterDefault = _dungeonData != null ? _dungeonData.monsterPrefab : null;
            GameObject trapDefault = _dungeonData != null ? _dungeonData.trapPrefab : null;
            int trapCount = _dungeonData != null ? _dungeonData.trapCountPerChunk : 0;

            _fixedMapChunk.Initialize(0, trapCount, monsterDefault, trapDefault, _monsterPrefabPool);

            if (_fixedMapChunk.PlayerSpawnPoint == null) return;
            _fixedMapChunk.ActivatePlayerSpawn();
            Vector2 spawnPos = _fixedMapChunk.PlayerSpawnPoint.position;

            if (_playerPrefab != null && _grid != null)
            {
                Vector3 playerPos = _grid.GetCellCenterWorld(_grid.WorldToCell(spawnPos));
                Instantiate(_playerPrefab, playerPos, Quaternion.identity);
            }

            GameObject exitPrefab = _fixedExitPrefab != null ? _fixedExitPrefab : (_dungeonData != null ? _dungeonData.exitPrefab : null);
            _fixedMapChunk.ActivateFarthestExitPoint(spawnPos, exitPrefab, _farthestExitCandidateCount);

            if (_grid != null)
            {
                Vector3Int startCell = _grid.WorldToCell(spawnPos);
                startCell.z = 0;
                InitializeFogOfWar(startCell);
            }
        }

        #endregion

        #region Procedural Chunks Setup (Post-MVP)

        private void SetupProceduralDungeon()
        {
            if (_dungeonData == null)
            {
                Debug.LogError("[DungeonGenerator] ProceduralChunks 모드에는 DungeonData가 필수입니다.");
                return;
            }

            GenerateProceduralChunks();
            DetermineProceduralStartAndExit();
        }

        private void GenerateProceduralChunks()
        {
            _spawnedChunks.Clear();
            int slotIndex = 0;

            for (int y = 0; y < _dungeonData.gridHeight; y++)
            {
                for (int x = 0; x < _dungeonData.gridWidth; x++)
                {
                    if (slotIndex >= _dungeonData.chunkSlots.Length) break;

                    var slot = _dungeonData.chunkSlots[slotIndex];
                    if (slot.possibleChunkPrefabs != null && slot.possibleChunkPrefabs.Count > 0)
                    {
                        int randomIndex = Random.Range(0, slot.possibleChunkPrefabs.Count);
                        GameObject selectedPrefab = slot.possibleChunkPrefabs[randomIndex];

                        if (selectedPrefab != null)
                        {
                            Vector2 spawnPosition = new Vector2(x * _dungeonData.chunkOffset.x, y * _dungeonData.chunkOffset.y);
                            GameObject chunkGo = Instantiate(selectedPrefab, spawnPosition, Quaternion.identity, this.transform);
                            
                            if (chunkGo.TryGetComponent<DungeonChunk>(out var chunkComponent))
                            {
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

        private void DetermineProceduralStartAndExit()
        {
            if (_spawnedChunks.Count == 0) return;

            int spawnIndex = Random.Range(0, _spawnedChunks.Count);
            DungeonChunk spawnChunk = _spawnedChunks[spawnIndex];
            spawnChunk.ActivatePlayerSpawn();

            if (spawnChunk.PlayerSpawnPoint == null) return;

            Vector2 spawnPos = spawnChunk.PlayerSpawnPoint.position;
            
            if (_playerPrefab != null)
            {
                Instantiate(_playerPrefab, spawnPos, Quaternion.identity);
            }

            var distanceList = _spawnedChunks
                .Where(c => c != spawnChunk && (c.ExitPoint != null || (c.ExitCandidatePoints != null && c.ExitCandidatePoints.Count > 0)))
                .OrderByDescending(c => {
                    Vector3 exitPos = c.ExitPoint != null ? c.ExitPoint.position : c.ExitCandidatePoints[0].position;
                    return Vector2.Distance(spawnPos, exitPos);
                })
                .ToList();

            if (distanceList.Count > 0)
            {
                int candidateCount = Mathf.Min(3, distanceList.Count);
                int randomExitIndex = Random.Range(0, candidateCount);
                
                DungeonChunk exitChunk = distanceList[randomExitIndex];
                exitChunk.ActivateFarthestExitPoint(spawnPos, _dungeonData.exitPrefab, 1);
            }

            if (_grid != null)
            {
                Vector3Int startCell = _grid.WorldToCell(spawnPos);
                startCell.z = 0;
                InitializeFogOfWar(startCell);
            }
        }

        #endregion
    }
}
