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
        [Tooltip("던전 크기 (타일 단위)")]
        [SerializeField] private int _dungeonWidth = 20;
        [SerializeField] private int _dungeonHeight = 20;
        [SerializeField] private int _monsterCount = 5;

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
            int minX = -_dungeonWidth / 2;
            int maxX = _dungeonWidth / 2;
            int minY = -_dungeonHeight / 2;
            int maxY = _dungeonHeight / 2;

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

            // 2. 귀환 포탈 스폰 (1개)
            if (_returnPortalPrefab != null)
            {
                Vector3 portalPos = GetRandomValidPosition(minX, maxX, minY, maxY);
                GameObject portal = Instantiate(_returnPortalPrefab, portalPos, Quaternion.identity);
                _spawnedObjects.Add(portal);
            }

            // 3. 몬스터 스폰 (N마리)
            if (_monsterPrefab != null)
            {
                for (int i = 0; i < _monsterCount; i++)
                {
                    Vector3 monsterPos = GetRandomValidPosition(minX, maxX, minY, maxY);
                    GameObject monster = Instantiate(_monsterPrefab, monsterPos, Quaternion.identity);
                    _spawnedObjects.Add(monster);
                }
            }

            Debug.Log($"[DungeonManager] 던전 생성 완료! (포탈 1개, 몬스터 {_monsterCount}마리 스폰)");
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

        private Vector3 GetRandomValidPosition(int minX, int maxX, int minY, int maxY)
        {
            // 타일맵 상의 유효한 그리드 좌표를 랜덤으로 선택
            int randX = Random.Range(minX, maxX + 1);
            int randY = Random.Range(minY, maxY + 1);
            Vector3Int gridPos = new Vector3Int(randX, randY, 0);
            
            // Floor Tilemap의 해당 그리드 좌표 중심(World Position)을 반환
            if (_floorTilemap != null)
            {
                return _floorTilemap.GetCellCenterWorld(gridPos);
            }
            
            return new Vector3(randX, randY, 0f);
        }
    }
}
