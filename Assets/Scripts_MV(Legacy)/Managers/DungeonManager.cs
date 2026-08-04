// File: Assets/Scripts/Managers/DungeonManager.cs
using System.Collections.Generic;
using UnityEngine;
using DimensionLooter.Core;

namespace DimensionLooter.Managers
{
    public class DungeonManager : MonoBehaviour
    {
        public static DungeonManager Instance { get; private set; }

        [Header("Dungeon Settings")]
        // 사용되지 않는 구버전 스폰 카운트 변수 등 제거됨

        [Header("Tilemap Settings")]
        [SerializeField] private UnityEngine.Tilemaps.Tilemap _floorTilemap;
        [SerializeField] private UnityEngine.Tilemaps.Tilemap _wallTilemap;
        [SerializeField] private UnityEngine.Tilemaps.TileBase _floorTile;
        [SerializeField] private UnityEngine.Tilemaps.TileBase _wallTile;

        [Header("Prefabs")]
        [SerializeField] private GameObject _returnPortalPrefab;
        [SerializeField] private GameObject _monsterPrefab;

        // 현재 던전에 생성된 오브젝트들을 추적하기 위한 리스트
        private List<GameObject> _spawnedObjects = new List<GameObject>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            Debug.Log("[DungeonManager] Start 함수 실행됨!");
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged += HandleStateChanged;
                Debug.Log("[DungeonManager] GameManager 상태 변경 이벤트 구독 성공!");
            }
            else
            {
                Debug.LogError("[DungeonManager] 치명적 에러: GameManager.Instance가 NULL입니다! 이벤트 구독 실패!");
            }
            LoadPrefabs();
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged -= HandleStateChanged;
            }
        }

        private void LoadPrefabs()
        {
            if (_returnPortalPrefab == null)
                _returnPortalPrefab = Resources.Load<GameObject>("Prefabs/ReturnPortal");
            if (_monsterPrefab == null)
                _monsterPrefab = Resources.Load<GameObject>("Prefabs/Monster_Dummy");
        }

        private void HandleStateChanged(Define.GameState state)
        {
            if (state == Define.GameState.Exploration)
            {
                GenerateDungeon();
            }
            else if (state == Define.GameState.Lobby)
            {
                ClearDungeon();
            }
        }

        private void GenerateDungeon()
        {
            Debug.Log("[DungeonManager] 던전 생성 시작...");
            ClearDungeon(); // 안전을 위해 기존 던전 정리

            if (_floorTilemap == null || _wallTilemap == null)
            {
                Debug.LogWarning("[DungeonManager] 타일맵이 할당되지 않아 런타임에 동적으로 생성합니다.");
                GameObject gridObj = GameObject.Find("Grid");
                if (gridObj == null)
                {
                    gridObj = new GameObject("Grid");
                    gridObj.AddComponent<Grid>();
                }
                
                if (_floorTilemap == null)
                {
                    GameObject tmObj = new GameObject("FloorTilemap");
                    tmObj.transform.SetParent(gridObj.transform);
                    _floorTilemap = tmObj.AddComponent<UnityEngine.Tilemaps.Tilemap>();
                    tmObj.AddComponent<UnityEngine.Tilemaps.TilemapRenderer>();
                }
                
                if (_wallTilemap == null)
                {
                    GameObject tmObj = new GameObject("WallTilemap");
                    tmObj.transform.SetParent(gridObj.transform);
                    _wallTilemap = tmObj.AddComponent<UnityEngine.Tilemaps.Tilemap>();
                    tmObj.AddComponent<UnityEngine.Tilemaps.TilemapRenderer>();
                }
            }

            if (_floorTile == null)
            {
                var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
                Texture2D tex = new Texture2D(32, 32);
                Color[] colors = new Color[32 * 32];
                for(int i = 0; i < colors.Length; i++) colors[i] = Color.gray; // 바닥은 회색
                tex.SetPixels(colors);
                tex.Apply();
                tile.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32);
                _floorTile = tile;
            }

            if (_wallTile == null)
            {
                var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
                Texture2D tex = new Texture2D(32, 32);
                Color[] colors = new Color[32 * 32];
                for(int i = 0; i < colors.Length; i++) colors[i] = Color.black; // 벽은 검은색
                tex.SetPixels(colors);
                tex.Apply();
                tile.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32);
                _wallTile = tile;
            }

            // 1. 타일맵 기반 던전 생성 (방 크기에 맞춰 바닥과 벽 배치)
            int minX = -15, maxX = 15, minY = -15, maxY = 15;

            for (int x = minX - 1; x <= maxX + 1; x++)
            {
                for (int y = minY - 1; y <= maxY + 1; y++)
                {
                    Vector3Int tilePos = new Vector3Int(x, y, 0);
                    
                    // 외곽은 벽, 내부는 바닥
                    if (x < minX || x > maxX || y < minY || y > maxY)
                    {
                        _wallTilemap.SetTile(tilePos, _wallTile);
                    }
                    else
                    {
                        _floorTilemap.SetTile(tilePos, _floorTile);
                    }
                }
            }

            // 2. 3x3 청크(Chunk) 조립 및 스폰 포인트 처리
            int chunkSize = 10;
            for (int cx = -1; cx <= 1; cx++)
            {
                for (int cy = -1; cy <= 1; cy++)
                {
                    Vector3 chunkPos = new Vector3(cx * chunkSize, cy * chunkSize, 0);
                    GameObject chunk = new GameObject($"MapChunk_{cx}_{cy}");
                    chunk.transform.position = chunkPos;
                    _spawnedObjects.Add(chunk);

                    HashSet<Vector3> usedPositions = new HashSet<Vector3>();

                    // 임시 Spawner 부착 (기획자가 프리팹에 미리 세팅할 구조 시뮬레이션)
                    for (int i = 0; i < 3; i++)
                    {
                        var spawnerObj = new GameObject("Spawner");
                        spawnerObj.transform.SetParent(chunk.transform);
                        
                        Vector3 randomPos;
                        int attempts = 0;
                        do
                        {
                            randomPos = new Vector3(Random.Range(-4, 5), Random.Range(-4, 5), 0f);
                            attempts++;
                        } while (usedPositions.Contains(randomPos) && attempts < 100);
                        usedPositions.Add(randomPos);

                        spawnerObj.transform.localPosition = randomPos;
                        
                        var spawner = spawnerObj.AddComponent<DimensionLooter.Gameplay.Spawner>();
                        
                        // 중앙 청크는 플레이어 시작 지점
                        if (cx == 0 && cy == 0 && i == 0)
                        {
                            spawner.type = DimensionLooter.Gameplay.SpawnerType.PlayerSpawn;
                        }
                        // 외곽 청크 모서리들은 포탈 스폰 지점
                        else if (Mathf.Abs(cx) == 1 && Mathf.Abs(cy) == 1 && i == 0)
                        {
                            spawner.type = DimensionLooter.Gameplay.SpawnerType.PortalSpawn;
                        }
                        else
                        {
                            // 30% 확률로 기믹(상자, 함정, 제단), 70% 확률로 몬스터 스폰
                            if (Random.value < 0.3f)
                            {
                                spawner.type = DimensionLooter.Gameplay.SpawnerType.Gimmick;
                                spawner.spawnProbability = 0.6f;
                                spawner.spawnPool = new List<DimensionLooter.Gameplay.SpawnWeight>()
                                {
                                    new DimensionLooter.Gameplay.SpawnWeight { entityId = "Chest_Normal", weight = 40 },
                                    new DimensionLooter.Gameplay.SpawnWeight { entityId = "Hazard_Spike", weight = 30 },
                                    new DimensionLooter.Gameplay.SpawnWeight { entityId = "Hazard_Poison", weight = 15 },
                                    new DimensionLooter.Gameplay.SpawnWeight { entityId = "Shrine_Heal", weight = 15 }
                                };
                            }
                            else
                            {
                                spawner.type = DimensionLooter.Gameplay.SpawnerType.Monster;
                                spawner.spawnProbability = 0.5f;
                                spawner.spawnPool = new List<DimensionLooter.Gameplay.SpawnWeight>()
                                {
                                    new DimensionLooter.Gameplay.SpawnWeight { entityId = "MOB_001", weight = 60 },
                                    new DimensionLooter.Gameplay.SpawnWeight { entityId = "MOB_002", weight = 30 },
                                    new DimensionLooter.Gameplay.SpawnWeight { entityId = "MOB_003", weight = 10 }
                                };
                            }
                            
                            // 히트(하드모드) 팩터 적용: 몹과 기믹의 생성 확률을 히트 배율만큼 증가
                            if (HeatManager.Instance != null && spawner.type != DimensionLooter.Gameplay.SpawnerType.PlayerSpawn && spawner.type != DimensionLooter.Gameplay.SpawnerType.PortalSpawn)
                            {
                                spawner.spawnProbability = Mathf.Clamp01(spawner.spawnProbability * (1f + HeatManager.Instance.RewardMultiplier));
                            }
                        }
                    }
                }
            }

            // 3. 맵 내의 모든 Spawner 수집 및 처리
            var allSpawners = FindObjectsOfType<DimensionLooter.Gameplay.Spawner>();
            List<DimensionLooter.Gameplay.Spawner> playerSpawns = new List<DimensionLooter.Gameplay.Spawner>();
            List<DimensionLooter.Gameplay.Spawner> portalSpawns = new List<DimensionLooter.Gameplay.Spawner>();
            List<DimensionLooter.Gameplay.Spawner> entitySpawns = new List<DimensionLooter.Gameplay.Spawner>();

            foreach(var s in allSpawners)
            {
                if (s.type == DimensionLooter.Gameplay.SpawnerType.PlayerSpawn) playerSpawns.Add(s);
                else if (s.type == DimensionLooter.Gameplay.SpawnerType.PortalSpawn) portalSpawns.Add(s);
                else entitySpawns.Add(s);
            }

            // 3-1. 플레이어 스폰 위치 결정 (지정된 스폰 위치 중 랜덤 1개)
            Vector3 playerStartPos = Vector3.zero;
            if (playerSpawns.Count > 0)
            {
                var chosenPlayerSpawn = playerSpawns[Random.Range(0, playerSpawns.Count)];
                playerStartPos = chosenPlayerSpawn.transform.position;
            }

            // 3-2. 포탈 스폰 (플레이어와 가장 먼 스폰 위치들 중 랜덤 선택)
            if (portalSpawns.Count > 0)
            {
                portalSpawns.Sort((a, b) => {
                    float distA = Vector3.Distance(a.transform.position, playerStartPos);
                    float distB = Vector3.Distance(b.transform.position, playerStartPos);
                    return distB.CompareTo(distA); // 내림차순 (먼 순서)
                });
                
                // 상위 3개 중 하나 랜덤
                int maxCandidate = Mathf.Min(3, portalSpawns.Count);
                var chosenPortal = portalSpawns[Random.Range(0, maxCandidate)];
                
                GameObject portalPrefab = Resources.Load<GameObject>("Prefabs/ReturnPortal");
                if (portalPrefab != null)
                {
                    GameObject portal = Instantiate(portalPrefab, chosenPortal.transform.position, Quaternion.identity);
                    _spawnedObjects.Add(portal);
                }
            }

            // 3-3. 일반 몬스터 및 기믹 스폰 실행
            foreach(var s in entitySpawns)
            {
                GameObject spawned = s.ExecuteSpawn();
                if (spawned != null) _spawnedObjects.Add(spawned);
            }

            // 4. 플레이어를 최종 스폰 위치로 이동
            var player = FindObjectOfType<Gameplay.PlayerController>();
            if (player != null)
            {
                player.transform.position = playerStartPos;
            }

            Debug.Log($"[DungeonManager] 던전 생성 완료! (청크 조립 기반)");
        }

        private void ClearDungeon()
        {
            Debug.Log("[DungeonManager] 던전 초기화 중...");
            
            // 타일맵 초기화
            if (_floorTilemap != null) _floorTilemap.ClearAllTiles();
            if (_wallTilemap != null) _wallTilemap.ClearAllTiles();

            // 스폰된 오브젝트 파괴
            foreach (var obj in _spawnedObjects)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            _spawnedObjects.Clear();

            // 필드에 떨어진 전리품/시체 파우치들 모두 청소
            if (LootManager.Instance != null)
            {
                LootManager.Instance.ClearAllPouches();
            }
        }

    }
}
