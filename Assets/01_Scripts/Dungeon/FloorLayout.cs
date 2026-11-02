using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BagBack100.Dungeon
{
    /// <summary>
    /// Role: 층 프리팹 루트. 바닥·벽 타일맵, 마커, 몬스터 구역을 묶고 칸 좌표(루트 기준)를 제공한다.
    ///       Grid는 씬에 하나 영속하고, 층은 그 Grid의 자식(원점)으로만 생성·교체한다.
    /// Dependencies: FloorMarker, DungeonZone
    /// Events: 없음
    /// </summary>
    public class FloorLayout : MonoBehaviour
    {
        [SerializeField] private Tilemap _floorTilemap;
        [SerializeField] private Tilemap _wallTilemap;

        private FloorMarker[] _markers;
        private DungeonZone[] _zones;

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
            return _wallTilemap == null || !_wallTilemap.HasTile(cell);
        }

#if UNITY_EDITOR
        public void EditorSetTilemaps(Tilemap floorTilemap, Tilemap wallTilemap)
        {
            _floorTilemap = floorTilemap;
            _wallTilemap = wallTilemap;
        }
#endif
    }
}
