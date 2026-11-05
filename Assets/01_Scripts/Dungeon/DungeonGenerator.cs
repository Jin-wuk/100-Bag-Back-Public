using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using BagBack100.Core;
using BagBack100.Dungeon.Data;
using BagBack100.Combat;
using BagBack100.Player;
using BagBack100.Data.Core;

namespace BagBack100.Dungeon
{
    /// <summary>
    /// Role: 층계 던전 맵 생성과 엔티티 스폰. RegionConfigSO에서 층 디자인을 골라 층 프리팹을 런타임 생성하고
    ///       마커(시작점·계단·탈출구·상자 후보)와 몬스터 구역(DungeonZone)으로 배치한다.
    ///       위층 계단(FloorStairs)으로 같은 씬 안에서 다음 층을 다시 만든다(층 전환).
    ///       층 클리어(FloorClearTracker) 시 보스층 정리, 안개 해제, 전투 상태 해제, 계단 잠금 해제, 탈출구 굴림을 처리한다.
    /// Dependencies: RegionConfigSO, FloorDesignSO, FloorLayout, FloorMarker, FloorStairs, FloorChest, FloorDesignValidator, DungeonZone, PlayerStatManager(보스 처치 후 무적),
    ///               FloorClearTracker, FogOfWarSystem, TileSystem, GamePauseService, PlayerController, PlayerCombatState, PlayerSessionData
    /// Events: GlobalEvents.OnFloorTransitionStarted, GlobalEvents.OnFloorEntered, GlobalEvents.OnFloorCleared
    /// </summary>
    public class DungeonGenerator : MonoBehaviour
    {
        private static readonly Vector3Int[] CardinalDirs = new Vector3Int[]
        {
            Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left
        };

        [Header("Collision")]
        [Tooltip("벽/기둥 등 이동 불가 장애물 물리 레이어")]
        [SerializeField] private LayerMask _obstacleLayerMask;

        [Header("Safety & Spacing Rules")]
        [Tooltip("출발 지점으로부터 걸어서 N칸 이내에는 몬스터/함정 스폰 금지")]
        [SerializeField] private int _startSafeStepRadius = 5;
        [Tooltip("몬스터끼리 뭉치거나 공격 범위가 겹치지 않도록 보장할 최소 타일 간격 (맨해튼 거리)")]
        [SerializeField] private int _minMonsterSpacing = 3;

        [Header("Prefabs & Pools")]
        [Tooltip("던전 진입 시 스폰할 플레이어 프리팹")]
        [SerializeField] private GameObject _playerPrefab;
        [Tooltip("기본 몬스터 프리팹 풀")]
        [SerializeField] private List<GameObject> _monsterPrefabPool = new List<GameObject>();
        [Tooltip("함정 프리팹 풀 (함정은 피해 효과 구현 전까지 배치하지 않는다)")]
        [SerializeField] private List<GameObject> _trapPrefabPool = new List<GameObject>();
        [Tooltip("탈출 포탈 프리팹 (DungeonExit). 층 클리어 시 층별 확률 굴림에 성공하면 탈출구 마커에 생성 (보스층은 100%)")]
        [SerializeField] private GameObject _fixedExitPrefab;

        [Header("Floor Dungeon")]
        [Tooltip("지역 설정 (층 디자인 풀·층별 규칙). 필수")]
        [SerializeField] private RegionConfigSO _regionConfig;
        [Tooltip("탈출구·위층 계단 마커 주변 몬스터·함정 스폰 금지 반경 (체비쇼프, 1 = 3x3)")]
        [SerializeField, Min(0)] private int _markerSafeRadius = 1;
        [Tooltip("탈출구·위층 계단 마커 주변 바닥 아이템 드랍 금지 반경 (체비쇼프, 1 = 3x3, 기획 07 Q27)")]
        [SerializeField, Min(0)] private int _noDropRadius = 1;
        [Tooltip("위층 계단 프리팹 (FloorStairs). 10층은 생성하지 않는다")]
        [SerializeField] private FloorStairs _stairsPrefab;
        [Tooltip("상자 프리팹 (FloorChest). 층별 규칙의 굴림 횟수·확률로 상자 후보 마커에 놓는다")]
        [SerializeField] private FloorChest _chestPrefab;
        [Tooltip("층 전환 페이드 아웃·인 시간(초, 실시간). 이 동안 게임을 일시정지하고 입력을 잠근다")]
        [SerializeField, Min(0f)] private float _floorFadeSeconds = 0.3f;

        private Grid _grid;
        // 현재 층 타일맵 (층 프리팹에서 바인딩, 층 전환 때 교체)
        private Tilemap _floorTilemap;
        private Tilemap _wallTilemap;
        private FloorLayout _currentFloor;
        private Transform _spawnRoot;
        private readonly List<FloorMarker> _markerBuffer = new List<FloorMarker>();
        private PlayerController _player;
        private PlayerInput _playerInput;
        private int _currentFloorNumber;
        private bool _isTransitioning;
        // 이번 판에 이미 쓴 층 프리팹 (씬이 던전 입장마다 다시 로드되므로 판이 바뀌면 자연히 비워진다)
        private readonly HashSet<FloorLayout> _usedLayouts = new HashSet<FloorLayout>();
        private readonly List<FloorDesignSO> _designCandidates = new List<FloorDesignSO>();
        private readonly List<FloorMarker> _chestCandidates = new List<FloorMarker>();

        private FloorClearTracker _clearTracker;
        private FloorStairs _currentStairs;
        // 이 층 탈출구 마커 칸. 탈출구는 층 생성 때가 아니라 클리어 순간 굴려서 만든다 (기획 07 4절)
        private Vector3Int _exitCell;
        private bool _hasExitMarker;
        // 플레이어가 마지막으로 도착한 칸. 계단·탈출구가 열리는 순간 그 위에 서 있는지(겹침 가드) 판정에 쓴다
        private Vector3Int _lastPlayerCell;

        public static DungeonGenerator Instance { get; private set; }

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
            _clearTracker = new FloorClearTracker(HandleFloorCleared);

            // 층 경로는 Start 전에 타일맵을 바인딩해야 Pathfinder·FogOfWar가 새 층을 본다
            if (_regionConfig == null)
            {
                Debug.LogError("[DungeonGenerator] RegionConfigSO가 지정되지 않았습니다.");
                return;
            }

            _currentFloorNumber = 1;
            PlayerSessionData.SetCurrentFloor(_regionConfig.RegionId, _currentFloorNumber);
            BuildFloor(SelectFloorDesign(_currentFloorNumber));
        }

        private void OnEnable()
        {
            FloorStairs.OnStairsEntered += HandleStairsEntered;
            GlobalEvents.OnPlayerCellChanged += HandlePlayerCellChanged;
        }

        private void OnDisable()
        {
            FloorStairs.OnStairsEntered -= HandleStairsEntered;
            GlobalEvents.OnPlayerCellChanged -= HandlePlayerCellChanged;
            if (_isTransitioning) EndTransitionLock();
        }

        private void OnDestroy()
        {
            _clearTracker?.End();
        }

        private void HandlePlayerCellChanged(Vector3Int cell) => _lastPlayerCell = cell;

        public bool IsCellWalkable(Vector3Int cell)
        {
            return IsTileWalkable(cell);
        }

        private void Start()
        {
            if (_grid == null) _grid = FindObjectOfType<Grid>();

            if (_currentFloor == null)
            {
                Debug.LogError("[DungeonGenerator] 층 프리팹 생성에 실패해 던전을 만들 수 없습니다.");
                return;
            }

            SetupFloorLayout(_currentFloor);
            RaiseFloorEntered();
            // 층 진입 알림 뒤에 판정을 켠다 (몬스터 0마리 층의 클리어가 진입 알림보다 먼저 나가지 않게)
            _clearTracker.Arm();
        }

        #region Floor Dungeon Setup (FloorLayout + Marker)

        /// <summary>
        /// 층 디자인 추첨 (Q13). 1~4층은 Early 풀, 6~9층은 Late 풀에서 가중치로 뽑고, 5층·10층은 보스 디자인 고정.
        /// 한 판(던전 입장) 안에서는 같은 층 프리팹을 다시 쓰지 않는다. 회색 박스처럼 SO 여러 개가 같은 프리팹을
        /// 공유할 수 있으므로 SO가 아니라 프리팹으로 중복을 판정한다.
        /// </summary>
        private FloorDesignSO SelectFloorDesign(int floor)
        {
            if (floor == RegionConfigSO.MidBossFloor) return _regionConfig.MidBossFloorDesign;
            if (floor >= RegionConfigSO.BossFloor) return _regionConfig.BossFloorDesign;

            var pool = floor < RegionConfigSO.MidBossFloor ? _regionConfig.EarlyPool : _regionConfig.LatePool;
            if (pool == null || pool.Count == 0) return null;

            CollectDesignCandidates(pool, _regionConfig.DebugAllowDuplicateDesigns);
            if (_designCandidates.Count == 0)
            {
                // 에디터 풀 검증을 통과하지 못한 데이터. 진행이 막히지 않게 중복을 허용해 다시 뽑는다
                Debug.LogError($"[DungeonGenerator] {floor}층에 쓸 수 있는 미사용 디자인이 없습니다. 풀 장수를 확인하세요. 중복을 허용해 진행합니다.");
                CollectDesignCandidates(pool, true);
            }

            FloorDesignSO picked = PickWeighted(_designCandidates);
            if (picked != null && picked.LayoutPrefab != null) _usedLayouts.Add(picked.LayoutPrefab);
            return picked;
        }

        private void CollectDesignCandidates(IReadOnlyList<FloorDesignSO> pool, bool allowDuplicate)
        {
            _designCandidates.Clear();
            for (int i = 0; i < pool.Count; i++)
            {
                FloorDesignSO design = pool[i];
                if (design == null || design.LayoutPrefab == null) continue;
                if (!allowDuplicate && _usedLayouts.Contains(design.LayoutPrefab)) continue;
                _designCandidates.Add(design);
            }
        }

        private static FloorDesignSO PickWeighted(List<FloorDesignSO> candidates)
        {
            int totalWeight = 0;
            for (int i = 0; i < candidates.Count; i++) totalWeight += candidates[i].Weight;
            if (totalWeight <= 0) return null;

            int roll = Random.Range(0, totalWeight);
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= candidates[i].Weight;
                if (roll < 0) return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }

        private void HandleStairsEntered()
        {
            if (_isTransitioning || _regionConfig == null || _currentFloor == null) return;
            if (_currentFloorNumber >= RegionConfigSO.BossFloor) return;
            StartCoroutine(AscendRoutine());
        }

        private IEnumerator AscendRoutine()
        {
            _isTransitioning = true;

            // 암전 동안 몬스터·투사체·자폭 퓨즈가 돌지 않게 멈춘다. 일시정지 진입 시 플레이어 차징·선입력도 취소된다
            GamePauseService.Request(this);
            SetPlayerInputLocked(true);
            GlobalEvents.OnFloorTransitionStarted?.Invoke(_floorFadeSeconds);
            yield return new WaitForSecondsRealtime(_floorFadeSeconds);

            ClearFloor();
            _currentFloorNumber++;
            PlayerSessionData.SetCurrentFloor(_regionConfig.RegionId, _currentFloorNumber);
            BuildFloor(SelectFloorDesign(_currentFloorNumber));
            if (_currentFloor != null)
            {
                SetupFloorLayout(_currentFloor);
            }
            else
            {
                Debug.LogError($"[DungeonGenerator] {_currentFloorNumber}층을 만들지 못했습니다.");
            }

            RaiseFloorEntered();
            _clearTracker.Arm();
            yield return new WaitForSecondsRealtime(_floorFadeSeconds);
            EndTransitionLock();
        }

        private void EndTransitionLock()
        {
            SetPlayerInputLocked(false);
            GamePauseService.Release(this);
            _isTransitioning = false;
        }

        private void SetPlayerInputLocked(bool locked)
        {
            if (_playerInput != null) _playerInput.IsInputLocked = locked;
        }

        private void RaiseFloorEntered()
        {
            GlobalEvents.OnFloorEntered?.Invoke(_regionConfig.RegionId, _regionConfig.RegionName, _currentFloorNumber);
        }

        /// <summary>
        /// 현재 층과 그 위의 모든 것을 지운다. 플레이어·UI·카메라·Grid는 유지된다.
        /// Destroy는 프레임 끝에 실행되므로 이전 층을 먼저 끄고 Grid에서 떼어 같은 프레임의
        /// 안개 경계 계산(Grid 자식 타일맵 순회)과 BFS 물리 판정에서 즉시 빠지게 한다.
        /// </summary>
        private void ClearFloor()
        {
            // 이전 층 몬스터가 파괴되며 들어오는 제거 이벤트가 클리어를 띄우지 않도록 판정부터 끈다
            _clearTracker.End();
            // 보스 처치 후 무적은 그 층을 떠나는 순간 끝난다 (기획 07 Q24)
            SetPlayerInvincible(false);
            _currentStairs = null;
            _hasExitMarker = false;
            Projectile.ReleaseAll();
            AttackWarningUI.ReleaseAll();

            if (_currentFloor != null)
            {
                GameObject oldFloor = _currentFloor.gameObject;
                oldFloor.SetActive(false);
                oldFloor.transform.SetParent(null, false);
                Destroy(oldFloor);
            }
            _currentFloor = null;
            _spawnRoot = null;
            _floorTilemap = null;
            _wallTilemap = null;

            Physics2D.SyncTransforms();

            // 점유·위험 칸과 바닥 아이템(층 루트 밖에 생성됨)을 비운다
            if (TileSystem.Instance != null) TileSystem.Instance.ClearAll();
        }

        private void BuildFloor(FloorDesignSO design)
        {
            if (_grid == null)
            {
                Debug.LogError("[DungeonGenerator] 씬에 영속 Grid가 없습니다. 층 프리팹은 Grid 자식으로 생성됩니다.");
                return;
            }
            if (design == null || design.LayoutPrefab == null)
            {
                Debug.LogError($"[DungeonGenerator] {_regionConfig.name}에 {_currentFloorNumber}층 디자인(층 프리팹)이 없습니다.");
                return;
            }

            _currentFloor = Instantiate(design.LayoutPrefab, _grid.transform);
            _currentFloor.transform.localPosition = Vector3.zero;
            _currentFloor.transform.localRotation = Quaternion.identity;
            _currentFloor.transform.localScale = Vector3.one;

            // 층 정리(T2)가 층 루트만 파괴하면 되도록 스폰물을 층 아래에 모은다
            _spawnRoot = new GameObject("Spawned").transform;
            _spawnRoot.SetParent(_currentFloor.transform, false);

            _floorTilemap = _currentFloor.FloorTilemap;
            _wallTilemap = _currentFloor.WallTilemap;

            // 새 벽 콜라이더를 물리 월드에 즉시 반영해야 BFS의 OverlapPoint 판정이 맞는다
            Physics2D.SyncTransforms();

            var errors = new List<string>();
            // 5층 중간 보스층은 6층으로 가는 계단이 필요하고, 10층만 계단이 없어도 된다
            StairsRequirement stairs = _currentFloorNumber >= RegionConfigSO.BossFloor ? StairsRequirement.Optional : StairsRequirement.Required;
            if (!FloorDesignValidator.ValidateLayout(_currentFloor, stairs, _regionConfig.GetRule(_currentFloorNumber).ChestRolls, errors, RegionConfigSO.IsBossFloor(_currentFloorNumber)))
            {
                Debug.LogError($"[DungeonGenerator] 층 프리팹 규격 오류 ({design.name}):\n{string.Join("\n", errors)}");
            }
        }

        private void SetupFloorLayout(FloorLayout layout)
        {
            FloorMarker startMarker = layout.GetSingleMarker(FloorMarkerType.PlayerStart);
            if (startMarker == null)
            {
                Debug.LogError($"[DungeonGenerator] {layout.name}에 PlayerStart 마커가 정확히 1개 있어야 합니다.");
                return;
            }

            Vector3Int playerStartCell = LayoutToGridCell(layout, startMarker);
            // 몬스터 생성(Awake의 OnSpawned)보다 먼저 등록부를 연다
            _clearTracker.Begin(RegionConfigSO.IsBossFloor(_currentFloorNumber));
            SpawnPlayerAtCell(playerStartCell);

            Dictionary<Vector3Int, int> reachableSteps = BuildReachableTileMap(playerStartCell, 5000);
            HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int> { playerStartCell };

            // 탈출구·계단 주변은 스폰 금지, 상자 후보 칸은 상자 자리로 예약 (상자가 안 나와도 비워 둔다)
            // 계단은 10층에 만들지 않으므로, 보스층에 마커가 남아 있어도 스폰·드랍 금지 영역을 만들지 않는다
            bool hasStairs = _currentFloorNumber < RegionConfigSO.BossFloor;
            ReserveMarkerCells(layout, FloorMarkerType.Exit, _markerSafeRadius, occupiedCells);
            if (hasStairs) ReserveMarkerCells(layout, FloorMarkerType.UpStairs, _markerSafeRadius, occupiedCells);
            ReserveMarkerCells(layout, FloorMarkerType.ChestCandidate, 0, occupiedCells);
            RegisterNoDropCells(layout, FloorMarkerType.Exit);
            if (hasStairs) RegisterNoDropCells(layout, FloorMarkerType.UpStairs);

            // 위층 계단은 잠긴 채로 만들고 층 클리어 시 연다. 10층은 계단 없음.
            FloorMarker stairsMarker = layout.GetSingleMarker(FloorMarkerType.UpStairs);
            if (hasStairs && stairsMarker != null && _stairsPrefab != null)
            {
                Vector3Int stairsCell = LayoutToGridCell(layout, stairsMarker);
                _currentStairs = Instantiate(_stairsPrefab, _grid.GetCellCenterWorld(stairsCell), Quaternion.identity, _spawnRoot);
                _currentStairs.Initialize(stairsCell);
            }

            SpawnChests(layout);

            // 탈출구는 클리어 순간 굴림으로 만들므로 마커 칸만 기억한다
            FloorMarker exitMarker = layout.GetSingleMarker(FloorMarkerType.Exit);
            _hasExitMarker = exitMarker != null;
            if (_hasExitMarker) _exitCell = LayoutToGridCell(layout, exitMarker);

            DungeonZone[] monsterZones = layout.Zones;
            HashSet<Vector3Int> allZonedCells = new HashSet<Vector3Int>();
            Dictionary<DungeonZone, List<Vector3Int>> zoneValidCells = BuildZoneValidCells(monsterZones, reachableSteps, allZonedCells);
            // 잡몹이 보스와도 간격을 지키도록 보스를 먼저 놓고 같은 목록을 넘긴다
            List<Vector3Int> spawnedMonsterCells = new List<Vector3Int>();
            SpawnBoss(layout, reachableSteps, occupiedCells, spawnedMonsterCells);
            SpawnZoneEntities(monsterZones, zoneValidCells, allZonedCells, reachableSteps, occupiedCells, spawnedMonsterCells, _spawnRoot);
            InitializeFogOfWar(playerStartCell);
            // 보스층은 안개 없이 시작한다 (기획 07 Q20). 전환 일시정지 중이라 페이드 없이 바로 걷는다
            if (RegionConfigSO.IsBossFloor(_currentFloorNumber) && FogOfWarSystem.Instance != null)
            {
                FogOfWarSystem.Instance.RevealAll(true);
            }
        }

        /// <summary>
        /// 보스층의 BossSpawn 마커에 지역 설정의 보스를 놓는다. 몬스터 Awake에서 클리어 등록부에 자동 등록된다.
        /// 프리팹·마커가 없거나 칸을 쓸 수 없으면 보스 없이 진행한다(FloorClearTracker가 전원 처치 규칙으로 동작).
        /// </summary>
        private void SpawnBoss(FloorLayout layout, Dictionary<Vector3Int, int> reachableSteps, HashSet<Vector3Int> occupiedCells, List<Vector3Int> spawnedMonsterCells)
        {
            if (!RegionConfigSO.IsBossFloor(_currentFloorNumber)) return;

            GameObject prefab = _regionConfig.GetBossPrefab(_currentFloorNumber);
            FloorMarker marker = layout.GetSingleMarker(FloorMarkerType.BossSpawn);
            if (prefab == null || marker == null)
            {
                Debug.LogWarning($"[DungeonGenerator] {_currentFloorNumber}층 보스 프리팹 또는 BossSpawn 마커가 없어 전원 처치 규칙으로 진행합니다.");
                return;
            }

            Vector3Int cell = LayoutToGridCell(layout, marker);
            // 마커는 층 디자인에서 직접 찍은 위치이므로, 씬마다 다를 수 있는 잡몹용 _startSafeStepRadius 대신 검증기와 같은 기준을 쓴다
            bool isUnusableCell = !reachableSteps.TryGetValue(cell, out int steps) || steps <= FloorDesignValidator.StartSafeSteps || occupiedCells.Contains(cell);
            if (isUnusableCell)
            {
                Debug.LogWarning($"[DungeonGenerator] {_currentFloorNumber}층 BossSpawn 칸({cell.x},{cell.y})에 보스를 놓을 수 없어 전원 처치 규칙으로 진행합니다.");
                return;
            }

            Instantiate(prefab, _grid.GetCellCenterWorld(cell), Quaternion.identity, _spawnRoot);
            occupiedCells.Add(cell);
            spawnedMonsterCells.Add(cell);
        }

        // 층 프리팹은 Grid 원점에 생성되지만, 원점이 바뀌어도 맞도록 월드 좌표를 거쳐 Grid 칸으로 바꾼다.
        private Vector3Int LayoutToGridCell(FloorLayout layout, FloorMarker marker)
        {
            Vector3Int local = layout.GetLocalCell(marker);
            Vector3 world = layout.transform.TransformPoint(new Vector3(local.x + 0.5f, local.y + 0.5f, 0f));
            Vector3Int cell = _grid.WorldToCell(world);
            cell.z = 0;
            return cell;
        }

        /// <summary>
        /// 상자 B안(Q30): 층 규칙의 굴림 횟수만큼 확률 판정을 하고, 당첨될 때마다 남은 상자 후보 중 무작위 1곳에 놓는다.
        /// 후보를 몇 곳 찍든 상자 기대 개수는 같다. 보스층(5·10층)은 규칙이 0이라 놓이지 않는다.
        /// </summary>
        private void SpawnChests(FloorLayout layout)
        {
            if (_chestPrefab == null) return;

            FloorRule rule = _regionConfig.GetRule(_currentFloorNumber);
            if (rule.ChestRolls <= 0 || rule.ChestChance <= 0f) return;

            layout.CollectMarkers(FloorMarkerType.ChestCandidate, _chestCandidates);
            for (int roll = 0; roll < rule.ChestRolls; roll++)
            {
                if (_chestCandidates.Count == 0) break; // 검증을 우회한 데이터: 후보보다 굴림이 많음
                if (Random.value >= rule.ChestChance) continue;

                int index = Random.Range(0, _chestCandidates.Count);
                FloorMarker marker = _chestCandidates[index];
                _chestCandidates.RemoveAt(index);

                Vector3Int chestCell = LayoutToGridCell(layout, marker);
                FloorChest chest = Instantiate(_chestPrefab, _grid.GetCellCenterWorld(chestCell), Quaternion.identity, _spawnRoot);
                chest.Initialize(_regionConfig.ChestDropPoolId, chestCell);
            }
        }

        private void RegisterNoDropCells(FloorLayout layout, FloorMarkerType type)
        {
            if (TileSystem.Instance == null) return;
            layout.CollectMarkers(type, _markerBuffer);
            for (int i = 0; i < _markerBuffer.Count; i++)
            {
                Vector3Int center = LayoutToGridCell(layout, _markerBuffer[i]);
                for (int dx = -_noDropRadius; dx <= _noDropRadius; dx++)
                {
                    for (int dy = -_noDropRadius; dy <= _noDropRadius; dy++)
                    {
                        TileSystem.Instance.RegisterNoDropCell(new Vector3Int(center.x + dx, center.y + dy, 0));
                    }
                }
            }
        }

        /// <summary>
        /// 층 클리어 (FloorClearTracker 콜백, 층마다 1회). 보스층이면 남은 잡몹·소환수를 전리품 없이 제거하고
        /// 날아가는 투사체·예고 장판을 지운다. 이어서 안개 해제, 전투 상태 해제, 계단 잠금 해제.
        /// </summary>
        private void HandleFloorCleared(IReadOnlyList<Monster.MonsterController> remaining)
        {
            bool isBossFloor = RegionConfigSO.IsBossFloor(_currentFloorNumber);
            if (isBossFloor)
            {
                for (int i = 0; i < remaining.Count; i++)
                {
                    if (remaining[i] != null) remaining[i].DespawnWithoutLoot();
                }
                Projectile.ReleaseAll();
                AttackWarningUI.ReleaseAll();
                SetPlayerInvincible(true);
            }

            if (FogOfWarSystem.Instance != null) FogOfWarSystem.Instance.RevealAll();

            if (_player != null && _player.TryGetComponent<PlayerCombatState>(out var combatState))
            {
                combatState.ExitCombat();
            }

            if (_currentStairs != null) _currentStairs.Unlock(_lastPlayerCell);

            bool exitOpened = TryOpenExit(isBossFloor);
            GlobalEvents.OnFloorCleared?.Invoke(_currentFloorNumber, exitOpened);
        }

        private void SetPlayerInvincible(bool value)
        {
            if (_player != null && _player.TryGetComponent<PlayerStatManager>(out var stats)) stats.SetInvincible(value);
        }

        /// <summary>
        /// 층별 확률로 탈출구를 1회 굴린다. 보스층(5·10층)은 100%. 보정 규칙 없음(기획 07 U8).
        /// 플레이어가 마커 칸에 선 채로 클리어하면 포탈이 바로 확인창을 띄우지 않게 겹침 가드를 건다.
        /// </summary>
        private bool TryOpenExit(bool isBossFloor)
        {
            if (!_hasExitMarker || _fixedExitPrefab == null || _spawnRoot == null) return false;

            float chance = isBossFloor ? 1f : _regionConfig.GetRule(_currentFloorNumber).ExitChance;
            if (Random.value >= chance) return false;

            GameObject exitGo = Instantiate(_fixedExitPrefab, _grid.GetCellCenterWorld(_exitCell), Quaternion.identity, _spawnRoot);
            if (exitGo.TryGetComponent<DungeonExit>(out var exit))
            {
                exit.Activate(_exitCell, _lastPlayerCell);
            }
            else
            {
                Debug.LogError("[DungeonGenerator] 탈출 포탈 프리팹에 DungeonExit가 없습니다.");
            }
            return true;
        }

        private void ReserveMarkerCells(FloorLayout layout, FloorMarkerType type, int radius, HashSet<Vector3Int> occupiedCells)
        {
            layout.CollectMarkers(type, _markerBuffer);
            for (int i = 0; i < _markerBuffer.Count; i++)
            {
                Vector3Int center = LayoutToGridCell(layout, _markerBuffer[i]);
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        occupiedCells.Add(new Vector3Int(center.x + dx, center.y + dy, 0));
                    }
                }
            }
        }

        #endregion

        #region Spawning (Zone + Flood Fill)

        // 구역별 도달 가능 타일 캐싱 (벽 너머·고립 칸 배제)
        private Dictionary<DungeonZone, List<Vector3Int>> BuildZoneValidCells(
            IList<DungeonZone> zones,
            Dictionary<Vector3Int, int> reachableSteps,
            HashSet<Vector3Int> allZonedCells)
        {
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
            return zoneValidCells;
        }

        private void SpawnZoneEntities(
            IList<DungeonZone> zones,
            Dictionary<DungeonZone, List<Vector3Int>> zoneValidCells,
            HashSet<Vector3Int> allZonedCells,
            Dictionary<Vector3Int, int> reachableSteps,
            HashSet<Vector3Int> occupiedCells,
            List<Vector3Int> spawnedMonsterCells,
            Transform parent)
        {
            // 층계 던전: 몬스터 수는 지역의 층별 규칙(FloorRule.MonsterCount)이 정하고, 층 디자인의 구역은 배치 가능 영역으로만 쓴다.
            // 같은 디자인이 다른 층에 나와도 층에 맞는 수가 나오게 하기 위함. 예전 통맵의 "구역 밖 복도 추가 스폰"은 쓰지 않는다.
            // 규칙 표가 없는 10층(보스층)은 F6 전까지 구역에 적힌 수를 그대로 쓴다.
            // 함정은 피해 효과가 구현되기 전까지 배치하지 않는다 (효과 없는 함정이 바닥에 놓여 혼란만 준다).
            bool useFloorRule = _currentFloorNumber >= 1 && _currentFloorNumber <= RegionConfigSO.RuleFloorCount;
            if (!useFloorRule)
            {
                foreach (var zone in zones)
                {
                    List<Vector3Int> candidates = zoneValidCells[zone];
                    if (candidates.Count == 0 || zone.MonsterCount <= 0) continue;
                    SpawnEntitiesInCells(candidates, zone.MonsterCount, GetZoneMonsterPool(zone), reachableSteps, occupiedCells, spawnedMonsterCells, true, parent);
                }
                return;
            }

            int total = _regionConfig.GetRule(_currentFloorNumber).MonsterCount;
            if (total <= 0) return;

            // 유효 칸 수 비율로 나누고(최대 나머지 방식), 반올림으로 남는 마릿수는 나머지가 큰 구역부터 1마리씩 준다
            _zoneSpawnBuffer.Clear();
            int totalCells = 0;
            foreach (var zone in zones)
            {
                int cells = zoneValidCells[zone].Count;
                if (cells <= 0) continue;
                _zoneSpawnBuffer.Add(new ZoneShare { Zone = zone, Cells = cells });
                totalCells += cells;
            }
            if (totalCells == 0)
            {
                Debug.LogWarning($"[DungeonGenerator] {_currentFloorNumber}층 디자인에 몬스터를 놓을 구역 칸이 없습니다.");
                return;
            }

            int assigned = 0;
            for (int i = 0; i < _zoneSpawnBuffer.Count; i++)
            {
                ZoneShare share = _zoneSpawnBuffer[i];
                float exact = (float)total * share.Cells / totalCells;
                share.Count = Mathf.FloorToInt(exact);
                share.Remainder = exact - share.Count;
                assigned += share.Count;
                _zoneSpawnBuffer[i] = share;
            }
            _zoneSpawnBuffer.Sort((a, b) => b.Remainder.CompareTo(a.Remainder));
            for (int i = 0; assigned < total; i = (i + 1) % _zoneSpawnBuffer.Count)
            {
                ZoneShare share = _zoneSpawnBuffer[i];
                share.Count++;
                _zoneSpawnBuffer[i] = share;
                assigned++;
            }

            for (int i = 0; i < _zoneSpawnBuffer.Count; i++)
            {
                ZoneShare share = _zoneSpawnBuffer[i];
                if (share.Count <= 0) continue;
                SpawnEntitiesInCells(zoneValidCells[share.Zone], share.Count, GetZoneMonsterPool(share.Zone), reachableSteps, occupiedCells, spawnedMonsterCells, true, parent);
            }
        }

        private struct ZoneShare
        {
            public DungeonZone Zone;
            public int Cells;
            public int Count;
            public float Remainder;
        }

        private readonly List<ZoneShare> _zoneSpawnBuffer = new List<ZoneShare>();

        private List<GameObject> GetZoneMonsterPool(DungeonZone zone)
        {
            return zone.OverrideMonsterPool != null && zone.OverrideMonsterPool.Count > 0 ? zone.OverrideMonsterPool : _monsterPrefabPool;
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
                Vector3 worldPos = _grid.GetCellCenterWorld(cell);
                if (Physics2D.OverlapPoint(worldPos, _obstacleLayerMask) != null)
                {
                    return false;
                }
            }

            return true;
        }

        // 1층에서만 생성하고, 2층부터는 같은 플레이어를 옮긴다 (새로 만들면 HP가 다시 가득 찬다 — 기획 Q2)
        private void SpawnPlayerAtCell(Vector3Int cell)
        {
            if (_grid == null) return;
            Vector3 worldPos = _grid.GetCellCenterWorld(cell);

            // 1층은 PlayerController.Start(첫 OnPlayerCellChanged)보다 층 배치가 먼저라 마지막 도착 칸을 직접 넣는다
            _lastPlayerCell = cell;

            if (_player != null)
            {
                _player.ResetForFloorTransition(worldPos, cell);
                return;
            }

            if (_playerPrefab == null) return;
            GameObject playerGo = Instantiate(_playerPrefab, worldPos, Quaternion.identity);
            playerGo.TryGetComponent(out _player);
            playerGo.TryGetComponent(out _playerInput);
        }

        private void SpawnEntitiesInCells(
            List<Vector3Int> candidateCells,
            int count,
            List<GameObject> pool,
            Dictionary<Vector3Int, int> reachableSteps,
            HashSet<Vector3Int> occupiedCells,
            List<Vector3Int> spawnedMonsterCells,
            bool applySafeRadius,
            Transform parent)
        {
            if (candidateCells == null || candidateCells.Count == 0 || count <= 0) return;
            if (pool == null || pool.Count == 0) return;

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

                GameObject prefab = pool[Random.Range(0, pool.Count)];
                if (prefab == null) continue;

                Vector3 worldPos = _grid.GetCellCenterWorld(cell);
                Instantiate(prefab, worldPos, Quaternion.identity, parent);

                occupiedCells.Add(cell);
                if (spawnedMonsterCells != null)
                {
                    spawnedMonsterCells.Add(cell);
                }
                spawned++;
            }
        }

        #endregion
    }
}
