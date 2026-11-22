using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BagBack100.Dungeon
{
    /// <summary>
    /// Role: 층 프리팹 루트. 바닥·벽 타일맵, 마커, 몬스터 구역을 묶고 칸 좌표(루트 기준)를 제공한다.
    ///       Grid는 씬에 하나 영속하고, 층은 그 Grid의 자식(원점)으로만 생성·교체한다.
    ///       벽은 Wall 타일맵 칸, Wall 레이어 벽 타일맵(숲 WallBack·WallFront·WallInner, 충돌 있는 타일만), Wall 레이어 BoxCollider2D 오브젝트로 둘 수 있다.
    ///       타일맵·콜라이더가 막는 칸은 물리 엔진 없이 계산해 프리팹 에셋 검증에서도 막힌 칸으로 본다.
    /// Dependencies: FloorMarker, DungeonZone
    /// Events: 없음
    /// </summary>
    public class FloorLayout : MonoBehaviour
    {
        [SerializeField] private Tilemap _floorTilemap;
        [SerializeField] private Tilemap _wallTilemap;

        private const string WallLayerName = "Wall";
        // 콜라이더 가장자리가 정수 칸 경계에서 이만큼 넘게 벗어나면 비정렬로 본다
        private const float GridAlignTolerance = 0.01f;

        private FloorMarker[] _markers;
        private DungeonZone[] _zones;
        private readonly HashSet<Vector3Int> _colliderBlockedCells = new HashSet<Vector3Int>();
        private readonly List<string> _colliderIssues = new List<string>();
        private bool _colliderCellsReady;

        /// <summary>마지막 RefreshBlockedCells에서 찾은 문제(칸 경계 비정렬, BoxCollider2D가 아닌 Wall 콜라이더). 에디터 검증 경고용.</summary>
        public IReadOnlyList<string> ColliderIssues => _colliderIssues;

        public Tilemap FloorTilemap => _floorTilemap;
        public Tilemap WallTilemap => _wallTilemap;

        public DungeonZone[] Zones
        {
            get { EnsureCached(); return _zones; }
        }

        public FloorMarker[] Markers
        {
            get { EnsureCached(); return _markers; }
        }

        // Edit Mode(LoadPrefabContents 검증)에서는 Awake가 불리지 않고 구조가 바뀔 수 있어 매번 다시 모은다.
        private void EnsureCached()
        {
            if (_markers != null && _zones != null && Application.isPlaying) return;
            _markers = GetComponentsInChildren<FloorMarker>(true);
            _zones = GetComponentsInChildren<DungeonZone>(true);
        }

        public void CollectMarkers(FloorMarkerType type, List<FloorMarker> result)
        {
            result.Clear();
            FloorMarker[] markers = Markers;
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i].Type == type) result.Add(markers[i]);
            }
        }

        /// <summary>해당 종류 마커가 정확히 1개일 때만 반환, 아니면 null.</summary>
        public FloorMarker GetSingleMarker(FloorMarkerType type)
        {
            FloorMarker found = null;
            FloorMarker[] markers = Markers;
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i].Type != type) continue;
                if (found != null) return null;
                found = markers[i];
            }
            return found;
        }

        /// <summary>마커가 놓인 칸(루트 기준, 셀 크기 1). 중간 그룹 오브젝트가 움직여도 어긋나지 않게 루트 역변환을 쓴다.</summary>
        public Vector3Int GetLocalCell(FloorMarker marker)
        {
            Vector3 local = transform.InverseTransformPoint(marker.transform.position);
            return new Vector3Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y), 0);
        }

        public bool IsLocalCellWalkable(Vector3Int cell)
        {
            if (_floorTilemap == null || !_floorTilemap.HasTile(cell)) return false;
            if (_wallTilemap != null && _wallTilemap.HasTile(cell)) return false;
            if (!_colliderCellsReady) RefreshBlockedCells();
            return !_colliderBlockedCells.Contains(cell);
        }

        /// <summary>
        /// Wall 레이어에서 막힌 칸을 다시 모은다: 벽 타일맵(TilemapCollider2D, 충돌 종류가 None이 아닌 타일 칸)과 BoxCollider2D(칸 중심을 덮는 칸).
        /// 런타임 판정(Pathfinder·DungeonGenerator·안개)은 같은 규칙을 Physics2D.OverlapPoint(칸 중심)로 본다. 이 계산은 그 결과와 맞추기 위한 것.
        /// 층 루트 기준이며 회전은 없다고 가정한다. 에디터 검증은 시작할 때마다 호출한다(구조가 바뀔 수 있음).
        /// </summary>
        public void RefreshBlockedCells()
        {
            _colliderBlockedCells.Clear();
            _colliderIssues.Clear();
            _colliderCellsReady = true;

            int wallLayer = LayerMask.NameToLayer(WallLayerName);
            if (wallLayer < 0) return;

            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D col = colliders[i];
                if (col.gameObject.layer != wallLayer || col.isTrigger || !col.enabled || !IsActiveUnderRoot(col.transform)) continue;
                if (col is CompositeCollider2D composite)
                {
                    // Outlines는 속이 빈 테두리라 칸 중심 OverlapPoint가 내부 칸을 놓쳐 벽이 뚫린다
                    if (composite.geometryType == CompositeCollider2D.GeometryType.Outlines)
                        _colliderIssues.Add($"{col.name}: CompositeCollider2D Geometry Type이 Outlines라 런타임 칸 판정이 내부를 놓칩니다(Polygons로).");
                    continue;
                }
                if (col is TilemapCollider2D tileCol)
                {
                    // 기존 1칸 Wall 타일맵은 IsLocalCellWalkable이 HasTile로 이미 본다
                    if (col.TryGetComponent(out Tilemap wallMap) && wallMap != _wallTilemap) CollectTilemapBlockedCells(wallMap);
                    continue;
                }
                if (!(col is BoxCollider2D box))
                {
                    _colliderIssues.Add($"{col.name}: BoxCollider2D가 아닌 Wall 콜라이더({col.GetType().Name})는 검증 칸 계산에서 빠집니다.");
                    continue;
                }

                Vector3 center = transform.InverseTransformPoint(box.transform.TransformPoint(box.offset));
                Vector3 scale = box.transform.lossyScale;
                Vector3 rootScale = transform.lossyScale;
                // 층 루트는 스케일 1이 규격(CheckIdentityTransform). 0이면 나누지 않는다
                if (Mathf.Approximately(rootScale.x, 0f)) rootScale.x = 1f;
                if (Mathf.Approximately(rootScale.y, 0f)) rootScale.y = 1f;
                float w = Mathf.Abs(box.size.x * scale.x / rootScale.x);
                float h = Mathf.Abs(box.size.y * scale.y / rootScale.y);
                float xMin = center.x - w * 0.5f, xMax = center.x + w * 0.5f;
                float yMin = center.y - h * 0.5f, yMax = center.y + h * 0.5f;

                if (!IsOnGridLine(xMin) || !IsOnGridLine(xMax) || !IsOnGridLine(yMin) || !IsOnGridLine(yMax))
                {
                    _colliderIssues.Add($"{col.name}: 콜라이더 가장자리가 칸 경계에 맞지 않습니다 (x {xMin:0.##}~{xMax:0.##}, y {yMin:0.##}~{yMax:0.##}).");
                }

                // 칸 중심 (x+0.5, y+0.5)이 사각형 안에 있는 칸
                int cx0 = Mathf.CeilToInt(xMin - 0.5f), cx1 = Mathf.FloorToInt(xMax - 0.5f);
                int cy0 = Mathf.CeilToInt(yMin - 0.5f), cy1 = Mathf.FloorToInt(yMax - 0.5f);
                for (int y = cy0; y <= cy1; y++)
                    for (int x = cx0; x <= cx1; x++)
                        _colliderBlockedCells.Add(new Vector3Int(x, y, 0));
            }
        }

        // 아래쪽 외곽 벽 윗줄·벽 앞면 조각처럼 충돌 종류가 None인 타일은 이동 가능 칸으로 둔다
        private void CollectTilemapBlockedCells(Tilemap map)
        {
            map.CompressBounds();
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            {
                if (!map.HasTile(cell)) continue;
                Tile.ColliderType colliderType = map.GetColliderType(cell);
                if (colliderType == Tile.ColliderType.None) continue;
                // 프리팹 에셋에는 부모 Grid가 없어 GetCellCenterWorld를 쓸 수 없다. 셀 크기 1 규격으로 칸 중심을 직접 계산한다
                Vector3 world = map.transform.TransformPoint(new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f));
                Vector3 local = transform.InverseTransformPoint(world);
                _colliderBlockedCells.Add(new Vector3Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y), 0));
            }
        }

        // 프리팹 에셋 안의 오브젝트는 씬에 없어 activeInHierarchy가 항상 false다. 층 루트까지 activeSelf로 따라 올라가 판정한다
        private bool IsActiveUnderRoot(Transform t)
        {
            for (Transform cur = t; cur != null; cur = cur.parent)
            {
                if (!cur.gameObject.activeSelf) return false;
                if (cur == transform) return true;
            }
            return true;
        }

        private static bool IsOnGridLine(float v) => Mathf.Abs(v - Mathf.Round(v)) <= GridAlignTolerance;

#if UNITY_EDITOR
        public void EditorSetTilemaps(Tilemap floorTilemap, Tilemap wallTilemap)
        {
            _floorTilemap = floorTilemap;
            _wallTilemap = wallTilemap;
        }
#endif
    }
}
