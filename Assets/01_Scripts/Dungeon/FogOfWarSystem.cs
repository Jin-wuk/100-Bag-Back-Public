using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using BagBack100.Core;
using BagBack100.Combat;
using BagBack100.Monster;

namespace BagBack100.Dungeon
{
    public enum FogState
    {
        Unexplored, // 미탐험 (완전 암전)
        Explored,   // 탐험 완료/기억 (반투명 안개, 지형 및 고정 오브젝트만 표시)
        Visible     // 현재 시야 내 (투명, 지형·몬스터·아이템 모두 표시)
    }

    public class FogOfWarSystem : MonoBehaviour
    {
        public static FogOfWarSystem Instance { get; private set; }

        [Header("Sight Settings")]
        [Tooltip("플레이어 주변 시야 반경 (타일 수)")]
        [SerializeField] private int _sightRadius = 4;
        [Tooltip("시야 가장자리를 부드럽게 흐릴 그라데이션 폭 (타일 단위)")]
        [SerializeField] private float _softEdgeWidth = 1.5f;
        [Tooltip("벽 너머 시야 차단(Raycast/Bresenham) 적용 여부")]
        [SerializeField] private bool _useLineOfSight = true;
        [Tooltip("상단(북쪽) 벽면이 2칸 이상 높이일 때, 밝혀진 벽/바닥 위로 추가 개방할 수직 벽 타일 칸수")]
        [SerializeField] private int _topWallExtraHeight = 1;
        [Tooltip("시야를 차단할 장애물 레이어")]
        [SerializeField] private LayerMask _obstacleLayerMask;

        [Header("Fog Visuals")]
        [Tooltip("안개 오버레이용 타일맵 (비워두면 런타임에 Grid 하위에 자동 생성)")]
        [SerializeField] private Tilemap _fogTilemap;
        [Tooltip("커스텀 안개 셰이더 머티리얼 (할당 시 네모 각 제거 및 뿌연 구름 효과 활성화)")]
        [SerializeField] private Material _fogMaterial;
        [Tooltip("안개 농도 변화 속도 (0이면 즉시 변경, 높을수록 빠르게 페이드)")]
        [SerializeField] private float _fadeSpeed = 6f;

        [Tooltip("미탐험 영역 색상 (완전 암전)")]
        [SerializeField] private Color _unexploredColor = new Color(0.03f, 0.03f, 0.05f, 1f);
        [Tooltip("탐험 완료했으나 현재 시야 밖인 영역 색상 (뿌연 청회색빛 어둠 추천)")]
        [SerializeField] private Color _exploredColor = new Color(0.12f, 0.14f, 0.20f, 0.72f);
        [Tooltip("현재 시야 내 영역 색상 (투명)")]
        [SerializeField] private Color _visibleColor = new Color(0.12f, 0.14f, 0.20f, 0f);
        [Tooltip("안개 타일맵 렌더링 정렬 순서 (몬스터/아이템보다 위에 그려지도록 설정)")]
        [SerializeField] private int _fogSortingOrder = 50;

        private Grid _grid;
        private Tilemap _wallTilemap;
        private Tile _runtimeFogTile;
        private Vector3Int _currentPlayerCell;
        private bool _isInitialized;

        private readonly Dictionary<Vector3Int, FogState> _fogStates = new Dictionary<Vector3Int, FogState>();
        private readonly Dictionary<Vector3Int, float> _targetAlphas = new Dictionary<Vector3Int, float>();
        private readonly Dictionary<Vector3Int, float> _currentAlphas = new Dictionary<Vector3Int, float>();
        private readonly Dictionary<Vector3Int, float> _visibleCellBestAlphas = new Dictionary<Vector3Int, float>();

        private readonly HashSet<Vector3Int> _currentlyVisibleCells = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> _fadingCells = new HashSet<Vector3Int>();

        private readonly List<Vector3Int> _previousVisibleBuffer = new List<Vector3Int>();
        private readonly List<Vector3Int> _wallExtensionBuffer = new List<Vector3Int>();
        private readonly List<Vector3Int> _completedFadeBuffer = new List<Vector3Int>();

        private readonly List<MonsterController> _trackedMonsters = new List<MonsterController>();
        private readonly List<FogVisibilityObject> _trackedObjects = new List<FogVisibilityObject>();
        private readonly List<FogVisionSource> _trackedVisionSources = new List<FogVisionSource>();

        // Bilinear 보간용 마스크 텍스처 데이터
        private Texture2D _fogMaskTexture;
        private Color32[] _maskPixels;
        private Vector2Int _maskOriginCell;
        private int _maskWidth;
        private int _maskHeight;
        private bool _maskDirty;

        private Camera _mainCamera;
        private Color _originalCameraBgColor;

        private static readonly int FogMaskTexProp = Shader.PropertyToID("_FogMaskTex");
        private static readonly int FogWorldBoundsProp = Shader.PropertyToID("_FogWorldBounds");
        private static readonly int UnexploredColorProp = Shader.PropertyToID("_UnexploredColor");
        private static readonly int ExploredColorProp = Shader.PropertyToID("_ExploredColor");

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
            if (_fogMaskTexture != null)
            {
                Destroy(_fogMaskTexture);
            }
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_fadingCells.Count == 0) return;

            _completedFadeBuffer.Clear();
            float step = _fadeSpeed > 0f ? Time.deltaTime * _fadeSpeed : 1f;

            foreach (Vector3Int cell in _fadingCells)
            {
                float current = _currentAlphas.TryGetValue(cell, out float c) ? c : 1f;
                float target = _targetAlphas.TryGetValue(cell, out float t) ? t : 1f;

                float next = _fadeSpeed > 0f ? Mathf.MoveTowards(current, target, step) : target;
                _currentAlphas[cell] = next;
                ApplyCellVisualAlpha(cell, next);

                if (Mathf.Approximately(next, target))
                {
                    _completedFadeBuffer.Add(cell);
                }
            }

            for (int i = 0; i < _completedFadeBuffer.Count; i++)
            {
                _fadingCells.Remove(_completedFadeBuffer[i]);
            }

            if (_maskDirty && _fogMaskTexture != null)
            {
                _fogMaskTexture.SetPixels32(_maskPixels);
                _fogMaskTexture.Apply(false);
                _maskDirty = false;
            }
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

            _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                _originalCameraBgColor = _mainCamera.backgroundColor;
                _mainCamera.backgroundColor = _unexploredColor;
            }

            _currentPlayerCell = new Vector3Int(playerStartCell.x, playerStartCell.y, 0);
            _isInitialized = true;

            EnsureFogTilemap();
            CreateRuntimeSoftFogTile();
            PopulateInitialFog(floorTilemap, wallTilemap, _currentPlayerCell);
            UpdateVision(_currentPlayerCell);
        }

        private void EnsureFogTilemap()
        {
            if (_fogTilemap == null)
            {
                Transform existing = _grid.transform.Find("FogOfWarTilemap");
                if (existing != null && existing.TryGetComponent<Tilemap>(out var tm))
                {
                    _fogTilemap = tm;
                }
                else
                {
                    GameObject fogGo = new GameObject("FogOfWarTilemap");
                    fogGo.transform.SetParent(_grid.transform, false);
                    _fogTilemap = fogGo.AddComponent<Tilemap>();
                    fogGo.AddComponent<TilemapRenderer>();
                }
            }

            if (_fogTilemap.TryGetComponent<TilemapRenderer>(out var renderer))
            {
                renderer.sortingOrder = _fogSortingOrder;
                if (_fogMaterial != null)
                {
                    renderer.material = _fogMaterial;
                }
            }
        }

        private void CreateRuntimeSoftFogTile()
        {
            if (_runtimeFogTile != null) return;

            const int res = 32;
            float cellSizeX = _grid != null ? _grid.cellSize.x : 1f;
            float overlapScale = _fogMaterial != null ? 1.0f : 1.25f;
            float ppu = res / (Mathf.Max(0.01f, cellSizeX) * overlapScale);

            Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[res * res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    if (_fogMaterial != null)
                    {
                        pixels[y * res + x] = Color.white;
                    }
                    else
                    {
                        float nx = (x / (float)(res - 1)) * 2f - 1f;
                        float ny = (y / (float)(res - 1)) * 2f - 1f;
                        float dist = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));
                        float edgeSoftness = 1f - Mathf.SmoothStep(0.65f, 1.0f, dist);
                        float noise = Mathf.Lerp(0.88f, 1.0f, Mathf.PerlinNoise(x * 0.25f, y * 0.25f));
                        pixels[y * res + x] = new Color(noise, noise, noise, edgeSoftness);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), ppu);
            _runtimeFogTile = ScriptableObject.CreateInstance<Tile>();
            _runtimeFogTile.sprite = sprite;
            _runtimeFogTile.color = Color.white;
            _runtimeFogTile.colliderType = Tile.ColliderType.None;
        }

        private void PopulateInitialFog(Tilemap floorTilemap, Tilemap wallTilemap, Vector3Int centerCell)
        {
            _fogStates.Clear();
            _targetAlphas.Clear();
            _currentAlphas.Clear();
            _visibleCellBestAlphas.Clear();
            _currentlyVisibleCells.Clear();
            _fadingCells.Clear();
            _fogTilemap.ClearAllTiles();

            BoundsInt bounds = CalculateMapBounds(floorTilemap, wallTilemap, centerCell);
            int pad = _sightRadius + 6;

            int minX = bounds.xMin - pad;
            int minY = bounds.yMin - pad;
            int maxX = bounds.xMax + pad;
            int maxY = bounds.yMax + pad;

            _maskOriginCell = new Vector2Int(minX, minY);
            _maskWidth = (maxX - minX) + 1;
            _maskHeight = (maxY - minY) + 1;

            if (_fogMaskTexture != null) Destroy(_fogMaskTexture);
            _fogMaskTexture = new Texture2D(_maskWidth, _maskHeight, TextureFormat.RGBA32, false);
            _fogMaskTexture.filterMode = FilterMode.Bilinear;
            _fogMaskTexture.wrapMode = TextureWrapMode.Clamp;
            _maskPixels = new Color32[_maskWidth * _maskHeight];

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    _fogStates[cell] = FogState.Unexplored;
                    _targetAlphas[cell] = 1f;
                    _currentAlphas[cell] = 1f;

                    _fogTilemap.SetTile(cell, _runtimeFogTile);
                    _fogTilemap.SetTileFlags(cell, TileFlags.None);
                    ApplyCellVisualAlpha(cell, 1f);
                }
            }

            _fogMaskTexture.SetPixels32(_maskPixels);
            _fogMaskTexture.Apply(false);
            _maskDirty = false;

            UpdateShaderProperties();
        }

        private void UpdateShaderProperties()
        {
            if (_fogTilemap == null || !_fogTilemap.TryGetComponent<TilemapRenderer>(out var renderer)) return;
            Material mat = renderer.material;
            if (mat == null) return;

            Vector3 worldOrigin = _grid.CellToWorld(new Vector3Int(_maskOriginCell.x, _maskOriginCell.y, 0));
            Vector2 worldSize = new Vector2(_maskWidth * _grid.cellSize.x, _maskHeight * _grid.cellSize.y);

            mat.SetTexture(FogMaskTexProp, _fogMaskTexture);
            mat.SetVector(FogWorldBoundsProp, new Vector4(worldOrigin.x, worldOrigin.y, worldSize.x, worldSize.y));
            mat.SetColor(UnexploredColorProp, _unexploredColor);
            mat.SetColor(ExploredColorProp, _exploredColor);
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
            _currentPlayerCell = new Vector3Int(playerCell.x, playerCell.y, 0);
            CalculateAndApplyVision();
        }

        public void RequestVisionUpdate()
        {
            if (!_isInitialized) return;
            CalculateAndApplyVision();
        }

        private void CalculateAndApplyVision()
        {
            if (_grid == null || _fogTilemap == null) return;

            // 1. 이전 시야 셀 백업 및 초기화
            _previousVisibleBuffer.Clear();
            foreach (var cell in _currentlyVisibleCells)
            {
                _previousVisibleBuffer.Add(cell);
            }
            _currentlyVisibleCells.Clear();
            _visibleCellBestAlphas.Clear();

            // 2. 플레이어 본체 시야 계산
            ComputeVisionFromSource(_currentPlayerCell, _sightRadius, _softEdgeWidth, _useLineOfSight);

            // 2-1. 등록된 추가 광원(횃불, 성소 등) 시야 합산
            for (int i = _trackedVisionSources.Count - 1; i >= 0; i--)
            {
                FogVisionSource src = _trackedVisionSources[i];
                if (src == null)
                {
                    _trackedVisionSources.RemoveAt(i);
                    continue;
                }
                if (!src.gameObject.activeInHierarchy) continue;

                Vector3Int srcCell = _grid.WorldToCell(src.transform.position);
                srcCell.z = 0;

                // '발견된 후에만 점등' 옵션이 켜져 있으면, 해당 타일이 미탐험 상태일 때는 빛을 내지 않음
                if (src.RequireDiscoveredToEmit && GetCellFogState(srcCell) == FogState.Unexplored)
                {
                    continue;
                }

                ComputeVisionFromSource(srcCell, src.VisionRadius, src.SoftEdgeWidth, src.UseLineOfSight);
            }

            // 2.5. 상단(북쪽) 2칸 높이 외곽 벽 처리
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
                    float baseAlpha = _visibleCellBestAlphas.TryGetValue(baseWallCell, out float a) ? a : 0f;

                    for (int h = 1; h <= _topWallExtraHeight; h++)
                    {
                        Vector3Int upperCell = new Vector3Int(baseWallCell.x, baseWallCell.y + h, 0);
                        if (IsSightBlockedAtCell(upperCell))
                        {
                            _currentlyVisibleCells.Add(upperCell);
                            if (!_visibleCellBestAlphas.TryGetValue(upperCell, out float existingAlpha) || baseAlpha < existingAlpha)
                            {
                                _visibleCellBestAlphas[upperCell] = baseAlpha;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            // 3. 시야에서 벗어난 타일은 Explored(반투명 안개)로 전환
            for (int i = 0; i < _previousVisibleBuffer.Count; i++)
            {
                Vector3Int prevCell = _previousVisibleBuffer[i];
                if (!_currentlyVisibleCells.Contains(prevCell))
                {
                    SetCellFogStateInternal(prevCell, FogState.Explored, _exploredColor.a);
                }
            }

            // 4. 현재 시야 내 타일들에 부드러운 알파값 적용
            foreach (var kvp in _visibleCellBestAlphas)
            {
                SetCellFogStateInternal(kvp.Key, FogState.Visible, kvp.Value);
            }

            // 5. 몬스터 및 던전 오브젝트 가시성 동기화
            RefreshMonstersVisibility();
            RefreshObjectsVisibility();
        }

        private void ComputeVisionFromSource(Vector3Int centerCell, int radius, float softEdge, bool useLOS)
        {
            float outerRadius = radius + 0.5f;
            float innerRadius = Mathf.Max(0f, outerRadius - softEdge);
            int checkRadius = Mathf.CeilToInt(outerRadius);

            for (int dx = -checkRadius; dx <= checkRadius; dx++)
            {
                for (int dy = -checkRadius; dy <= checkRadius; dy++)
                {
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > outerRadius) continue;

                    Vector3Int targetCell = new Vector3Int(centerCell.x + dx, centerCell.y + dy, 0);
                    if (!useLOS || HasLineOfSight(centerCell, targetCell))
                    {
                        _currentlyVisibleCells.Add(targetCell);

                        float edgeT = Mathf.InverseLerp(innerRadius, outerRadius, dist);
                        float smoothAlpha = Mathf.Lerp(_visibleColor.a, _exploredColor.a * 0.85f, edgeT * edgeT);

                        // 여러 광원이 겹칠 때는 더 밝은 쪽(알파값이 작은 쪽)을 채택
                        if (!_visibleCellBestAlphas.TryGetValue(targetCell, out float existingAlpha) || smoothAlpha < existingAlpha)
                        {
                            _visibleCellBestAlphas[targetCell] = smoothAlpha;
                        }
                    }
                }
            }
        }

        private void SetCellFogStateInternal(Vector3Int cell, FogState newState, float targetAlpha)
        {
            cell.z = 0;
            _fogStates[cell] = newState;
            _targetAlphas[cell] = targetAlpha;

            if (!_fogTilemap.HasTile(cell))
            {
                _fogTilemap.SetTile(cell, _runtimeFogTile);
                _fogTilemap.SetTileFlags(cell, TileFlags.None);
                _currentAlphas[cell] = 1f;
            }

            if (_fadeSpeed <= 0f)
            {
                _currentAlphas[cell] = targetAlpha;
                ApplyCellVisualAlpha(cell, targetAlpha);
            }
            else
            {
                _fadingCells.Add(cell);
            }
        }

        private void ApplyCellVisualAlpha(Vector3Int cell, float alpha)
        {
            float exploredRatio = Mathf.InverseLerp(_exploredColor.a, 1f, alpha);
            Color baseColor = Color.Lerp(_exploredColor, _unexploredColor, exploredRatio);
            baseColor.a = alpha;
            _fogTilemap.SetColor(cell, baseColor);

            int ix = cell.x - _maskOriginCell.x;
            int iy = cell.y - _maskOriginCell.y;
            if (ix >= 0 && ix < _maskWidth && iy >= 0 && iy < _maskHeight)
            {
                byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
                byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(exploredRatio * 255f), 0, 255);
                _maskPixels[iy * _maskWidth + ix] = new Color32(r, g, 0, 255);
                _maskDirty = true;
            }
        }

        private bool HasLineOfSight(Vector3Int from, Vector3Int to)
        {
            int x0 = from.x, y0 = from.y;
            int x1 = to.x, y1 = to.y;

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                if (x0 == x1 && y0 == y1) return true;

                if (!(x0 == from.x && y0 == from.y))
                {
                    Vector3Int stepCell = new Vector3Int(x0, y0, 0);
                    if (IsSightBlockedAtCell(stepCell)) return false;
                }

                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }

        private bool IsSightBlockedAtCell(Vector3Int cell)
        {
            if (_wallTilemap != null && _wallTilemap.HasTile(cell)) return true;
            if (_obstacleLayerMask.value != 0 && _grid != null)
            {
                if (Pathfinder.IsObstacle(_grid, new Vector2Int(cell.x, cell.y), _obstacleLayerMask))
                    return true;
            }
            return false;
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

        // --- 몬스터 가시성 관리 ---
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

        // --- 던전 오브젝트(상자, 아이템, 계단 등) 가시성 관리 ---
        public void RegisterObject(FogVisibilityObject obj)
        {
            if (obj == null) return;
            if (!_trackedObjects.Contains(obj))
            {
                _trackedObjects.Add(obj);
            }
            UpdateSingleObjectVisibility(obj);
        }

        public void UnregisterObject(FogVisibilityObject obj)
        {
            if (obj == null) return;
            _trackedObjects.Remove(obj);
        }

        public void UpdateSingleObjectVisibility(FogVisibilityObject obj)
        {
            if (obj == null || _grid == null) return;
            Vector3Int objCell = _grid.WorldToCell(obj.transform.position);
            objCell.z = 0;
            obj.ApplyFogState(GetCellFogState(objCell));
        }

        private void RefreshObjectsVisibility()
        {
            for (int i = _trackedObjects.Count - 1; i >= 0; i--)
            {
                FogVisibilityObject obj = _trackedObjects[i];
                if (obj == null)
                {
                    _trackedObjects.RemoveAt(i);
                    continue;
                }
                UpdateSingleObjectVisibility(obj);
            }
        }

        // --- 추가 광원(횃불, 성소 등) 관리 ---
        public void RegisterVisionSource(FogVisionSource source)
        {
            if (source == null) return;
            if (!_trackedVisionSources.Contains(source))
            {
                _trackedVisionSources.Add(source);
                RequestVisionUpdate();
            }
        }

        public void UnregisterVisionSource(FogVisionSource source)
        {
            if (source == null) return;
            if (_trackedVisionSources.Remove(source))
            {
                RequestVisionUpdate();
            }
        }
    }
}