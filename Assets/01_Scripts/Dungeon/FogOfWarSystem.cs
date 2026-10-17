using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using BagBack100.Core;
using BagBack100.Combat;

namespace BagBack100.Dungeon
{
    public enum FogState
    {
        Unexplored, // 미탐험 (완전 암전)
        Explored,   // 탐험 완료/기억 (반투명 어둠, 지형만 표시되고 몬스터 숨김)
        Visible     // 현재 시야 내 (투명, 지형 및 몬스터 모두 표시)
    }

    public class FogOfWarSystem : MonoBehaviour
    {
        public static FogOfWarSystem Instance { get; private set; }

        [Header("Sight Settings")]
        [Tooltip("플레이어 주변 시야 반경 (타일 수)")]
        [SerializeField] private int _sightRadius = 4;
        [Tooltip("벽 너머 시야 차단(Raycast/Bresenham) 적용 여부")]
        [SerializeField] private bool _useLineOfSight = true;
        [Tooltip("상단(북쪽) 벽면이 2칸 이상 높이일 때, 밝혀진 벽/바닥 위로 추가 개방할 수직 벽 타일 칸수 (기본 1 = 총 2칸 벽 높이 표시)")]
        [SerializeField] private int _topWallExtraHeight = 1;
        [Tooltip("시야를 차단할 장애물 레이어")]
        [SerializeField] private LayerMask _obstacleLayerMask;

        [Header("Fog Visuals")]
        [Tooltip("안개 오버레이용 타일맵 (비워두면 런타임에 Grid 하위에 자동 생성)")]
        [SerializeField] private Tilemap _fogTilemap;
        [Tooltip("미탐험 영역 색상 (완전 암전)")]
        [SerializeField] private Color _unexploredColor = new Color(0f, 0f, 0f, 1f);
        [Tooltip("탐험 완료했으나 현재 시야 밖인 영역 색상 (반투명 어둠)")]
        [SerializeField] private Color _exploredColor = new Color(0f, 0f, 0f, 0.65f);
        [Tooltip("현재 시야 내 영역 색상 (투명)")]
        [SerializeField] private Color _visibleColor = new Color(0f, 0f, 0f, 0f);
        [Tooltip("안개 타일맵 렌더링 정렬 순서 (몬스터/아이템보다 위에 그려지도록 설정)")]
        [SerializeField] private int _fogSortingOrder = 50;

        private Grid _grid;
        private Tilemap _wallTilemap;
        private Tile _runtimeFogTile;

        private readonly Dictionary<Vector3Int, FogState> _fogStates = new Dictionary<Vector3Int, FogState>();
        private readonly HashSet<Vector3Int> _currentlyVisibleCells = new HashSet<Vector3Int>();
        private readonly List<Vector3Int> _previousVisibleBuffer = new List<Vector3Int>();
        private readonly List<Vector3Int> _wallExtensionBuffer = new List<Vector3Int>();
        private readonly List<MonsterController> _trackedMonsters = new List<MonsterController>();

        private Camera _mainCamera;
        private Color _originalCameraBgColor;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void OnEnable()
        {
            GlobalEvents.OnPlayerCellChanged += UpdateVision;
        }

        private void OnDisable()
        {
            GlobalEvents.OnPlayerCellChanged -= UpdateVision;
        }

        private void OnDestroy()
        {
            if (_mainCamera != null)
            {
                _mainCamera.backgroundColor = _originalCameraBgColor;
            }
            if (Instance == this) Instance = null;
        }

        public void Initialize(Grid grid, Tilemap floorTilemap, Tilemap wallTilemap, LayerMask obstacleMask, Vector3Int playerStartCell)
        {
            _grid = grid != null ? grid : FindObjectOfType<Grid>();
            _wallTilemap = wallTilemap;
            if (obstacleMask.value != 0)
            {
                _obstacleLayerMask = obstacleMask;
            }

            if (_grid == null) return;

            // 카메라 배경을 미탐험 암전 색상과 맞춰 맵 바깥 여백도 어둠으로 통일
            _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                _originalCameraBgColor = _mainCamera.backgroundColor;
                _mainCamera.backgroundColor = _unexploredColor;
            }

            EnsureFogTilemap();
            CreateRuntimeWhiteTile();
            PopulateInitialFog(floorTilemap, wallTilemap, playerStartCell);
            UpdateVision(playerStartCell);
        }

        private void EnsureFogTilemap()
        {
            if (_fogTilemap != null) return;

            Transform existing = _grid.transform.Find("FogOfWarTilemap");
            if (existing != null && existing.TryGetComponent<Tilemap>(out var tm))
            {
                _fogTilemap = tm;
                return;
            }

            GameObject fogGo = new GameObject("FogOfWarTilemap");
            fogGo.transform.SetParent(_grid.transform, false);
            _fogTilemap = fogGo.AddComponent<Tilemap>();
            var renderer = fogGo.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = _fogSortingOrder;
        }

        private void CreateRuntimeWhiteTile()
        {
            if (_runtimeFogTile != null) return;

            float cellSizeX = _grid != null ? _grid.cellSize.x : 1f;
            float ppu = 4f / Mathf.Max(0.01f, cellSizeX);

            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), ppu);
            _runtimeFogTile = ScriptableObject.CreateInstance<Tile>();
            _runtimeFogTile.sprite = sprite;
            _runtimeFogTile.color = Color.white;
            _runtimeFogTile.colliderType = Tile.ColliderType.None;
        }

        private void PopulateInitialFog(Tilemap floorTilemap, Tilemap wallTilemap, Vector3Int centerCell)
        {
            _fogStates.Clear();
            _currentlyVisibleCells.Clear();
            _fogTilemap.ClearAllTiles();

            BoundsInt bounds = CalculateMapBounds(floorTilemap, wallTilemap, centerCell);

            // 가장자리 여유분(Padding)까지 모두 안개 타일로 덮기
            int pad = _sightRadius + 6;
            for (int x = bounds.xMin - pad; x <= bounds.xMax + pad; x++)
            {
                for (int y = bounds.yMin - pad; y <= bounds.yMax + pad; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    SetCellFogStateInternal(cell, FogState.Unexplored, true);
                }
            }
        }

        private BoundsInt CalculateMapBounds(Tilemap floorTilemap, Tilemap wallTilemap, Vector3Int fallbackCenter)
        {
            bool hasBounds = false;
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            Tilemap[] maps = _grid.GetComponentsInChildren<Tilemap>();
            for (int i = 0; i < maps.Length; i++)
            {
                Tilemap tm = maps[i];
                if (tm == _fogTilemap) continue;

                tm.CompressBounds();
                BoundsInt b = tm.cellBounds;
                if (b.size.x <= 0 || b.size.y <= 0) continue;

                hasBounds = true;
                if (b.xMin < minX) minX = b.xMin;
                if (b.yMin < minY) minY = b.yMin;
                if (b.xMax > maxX) maxX = b.xMax;
                if (b.yMax > maxY) maxY = b.yMax;
            }

            if (!hasBounds)
            {
                return new BoundsInt(fallbackCenter.x - 40, fallbackCenter.y - 40, 0, 80, 80, 1);
            }

            return new BoundsInt(minX, minY, 0, maxX - minX, maxY - minY, 1);
        }

        public void UpdateVision(Vector3Int playerCell)
        {
            if (_grid == null || _fogTilemap == null) return;
            playerCell.z = 0;

            // 1. 이전에 Visible이었던 타일들을 Explored(탐험 완료 반투명 어둠)로 전환
            _previousVisibleBuffer.Clear();
            foreach (var cell in _currentlyVisibleCells)
            {
                _previousVisibleBuffer.Add(cell);
            }
            _currentlyVisibleCells.Clear();

            for (int i = 0; i < _previousVisibleBuffer.Count; i++)
            {
                Vector3Int prevCell = _previousVisibleBuffer[i];
                SetCellFogStateInternal(prevCell, FogState.Explored, false);
            }

            // 2. 현재 시야 반경 내 타일 검사 및 Visible 갱신
            int rSq = _sightRadius * _sightRadius;
            for (int dx = -_sightRadius; dx <= _sightRadius; dx++)
            {
                for (int dy = -_sightRadius; dy <= _sightRadius; dy++)
                {
                    if (dx * dx + dy * dy > rSq) continue;

                    Vector3Int targetCell = new Vector3Int(playerCell.x + dx, playerCell.y + dy, 0);
                    if (!_useLineOfSight || HasLineOfSight(playerCell, targetCell))
                    {
                        _currentlyVisibleCells.Add(targetCell);
                        SetCellFogStateInternal(targetCell, FogState.Visible, false);
                    }
                }
            }

            // 2.5. 상단(북쪽) 2칸 높이 외곽 벽 처리:
            // 현재 시야로 밝혀진 벽 타일의 바로 위쪽(y + 1 ~ _topWallExtraHeight)이 연속된 벽 타일이면 함께 밝혀줌
            if (_topWallExtraHeight > 0)
            {
                _wallExtensionBuffer.Clear();
                foreach (var visibleCell in _currentlyVisibleCells)
                {
                    if (IsSightBlockedAtCell(visibleCell))
                    {
                        _wallExtensionBuffer.Add(visibleCell);
                    }
                }

                for (int i = 0; i < _wallExtensionBuffer.Count; i++)
                {
                    Vector3Int baseWallCell = _wallExtensionBuffer[i];
                    for (int h = 1; h <= _topWallExtraHeight; h++)
                    {
                        Vector3Int upperCell = new Vector3Int(baseWallCell.x, baseWallCell.y + h, 0);
                        if (IsSightBlockedAtCell(upperCell))
                        {
                            if (_currentlyVisibleCells.Add(upperCell))
                            {
                                SetCellFogStateInternal(upperCell, FogState.Visible, false);
                            }
                        }
                        else
                        {
                            // 벽이 끝나고 빈 바닥이 나오면 즉시 중단 (벽 너머 방 노출 방지)
                            break;
                        }
                    }
                }
            }

            // 3. 몬스터 가시성 동기화
            RefreshMonstersVisibility();
        }

        /// <summary>
        /// Bresenham 직선 알고리즘을 사용하여 플레이어 타일에서 목표 타일까지 시야가 통하는지 검사한다.
        /// 목표 타일 자체가 벽인 경우에는 벽면까지는 보이도록 허용하고, 벽 너머 타일은 차단한다.
        /// </summary>
        private bool HasLineOfSight(Vector3Int from, Vector3Int to)
        {
            int x0 = from.x;
            int y0 = from.y;
            int x1 = to.x;
            int y1 = to.y;

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                if (x0 == x1 && y0 == y1)
                {
                    return true;
                }

                // 시작 타일이 아닌 중간 경로 타일이 시야 차단(벽/장애물)이면 너머는 보이지 않음
                if (!(x0 == from.x && y0 == from.y))
                {
                    Vector3Int stepCell = new Vector3Int(x0, y0, 0);
                    if (IsSightBlockedAtCell(stepCell))
                    {
                        return false;
                    }
                }

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private bool IsSightBlockedAtCell(Vector3Int cell)
        {
            if (_wallTilemap != null && _wallTilemap.HasTile(cell))
            {
                return true;
            }

            if (_obstacleLayerMask.value != 0 && _grid != null)
            {
                if (Pathfinder.IsObstacle(_grid, new Vector2Int(cell.x, cell.y), _obstacleLayerMask))
                {
                    return true;
                }
            }

            return false;
        }

        private void SetCellFogStateInternal(Vector3Int cell, FogState newState, bool forceCreateTile)
        {
            cell.z = 0;
            bool exists = _fogStates.TryGetValue(cell, out FogState currentState);
            if (!forceCreateTile && exists && currentState == newState) return;

            _fogStates[cell] = newState;

            if (forceCreateTile || !_fogTilemap.HasTile(cell))
            {
                _fogTilemap.SetTile(cell, _runtimeFogTile);
                _fogTilemap.SetTileFlags(cell, TileFlags.None);
            }

            Color targetColor = _unexploredColor;
            if (newState == FogState.Explored) targetColor = _exploredColor;
            else if (newState == FogState.Visible) targetColor = _visibleColor;

            _fogTilemap.SetColor(cell, targetColor);
        }

        public bool IsCellVisible(Vector3Int cell)
        {
            cell.z = 0;
            return _currentlyVisibleCells.Contains(cell);
        }

        public FogState GetCellFogState(Vector3Int cell)
        {
            cell.z = 0;
            return _fogStates.TryGetValue(cell, out FogState state) ? state : FogState.Unexplored;
        }

        public void RegisterMonster(MonsterController monster)
        {
            if (monster == null || _trackedMonsters.Contains(monster)) return;
            _trackedMonsters.Add(monster);
            UpdateSingleMonsterVisibility(monster);
        }

        public void UnregisterMonster(MonsterController monster)
        {
            if (monster == null) return;
            _trackedMonsters.Remove(monster);
        }

        public void UpdateSingleMonsterVisibility(MonsterController monster)
        {
            if (monster == null || _grid == null) return;
            Vector3Int monsterCell = _grid.WorldToCell(monster.transform.position);
            monsterCell.z = 0;
            monster.SetVisibleByFog(IsCellVisible(monsterCell));
        }

        private void RefreshMonstersVisibility()
        {
            for (int i = _trackedMonsters.Count - 1; i >= 0; i--)
            {
                MonsterController m = _trackedMonsters[i];
                if (m == null)
                {
                    _trackedMonsters.RemoveAt(i);
                    continue;
                }
                UpdateSingleMonsterVisibility(m);
            }
        }
    }
}
