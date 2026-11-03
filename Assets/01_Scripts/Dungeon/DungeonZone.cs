using System.Collections.Generic;
using UnityEngine;

namespace BagBack100.Dungeon
{
    // 시작점·탈출구·상자 후보는 FloorMarker가 맡는다. 값은 기존 직렬화 데이터와 맞추려고 예전 번호를 유지한다
    public enum DungeonZoneType
    {
        Combat = 1,     // 전투 방/교차로 (몬스터 집중 스폰)
        Corridor = 3    // 수동 복도 구역 (특정 복도에 스폰 강제 시 사용)
    }

    /// <summary>
    /// Role: 층 프리팹 안에 사각형 영역으로 배치해 몬스터·함정 스폰 구역을 정의한다.
    ///       영역 내부에 벽이 있어도 DungeonGenerator의 Flood Fill이 이동 가능 타일만 추출한다.
    /// Dependencies: 없음
    /// Events: 없음
    /// </summary>
    public class DungeonZone : MonoBehaviour
    {
        [Header("Zone Identity")]
        [SerializeField] private DungeonZoneType _zoneType = DungeonZoneType.Combat;
        [Tooltip("BoxCollider2D가 없을 때 사용할 구역 가로/세로 크기 (타일 단위)")]
        [SerializeField] private Vector2 _size = new Vector2(6f, 6f);

        [Header("Spawn Counts")]
        [Tooltip("이 구역 내에 랜덤 스폰할 몬스터 마리 수")]
        [SerializeField] private int _monsterCount = 2;
        [Tooltip("이 구역 내에 랜덤 스폰할 함정 개수")]
        [SerializeField] private int _trapCount = 0;

        [Header("Optional Zone Override Pools")]
        [Tooltip("비워두면 DungeonGenerator의 기본 몬스터 풀을 사용")]
        [SerializeField] private List<GameObject> _overrideMonsterPool = new List<GameObject>();

        public DungeonZoneType ZoneType => _zoneType;
        public int MonsterCount => _monsterCount;
        public int TrapCount => _trapCount;
        public List<GameObject> OverrideMonsterPool => _overrideMonsterPool;

        private void Awake()
        {
            // 에디터에서 영역 조절 편의용으로 BoxCollider2D를 붙인 경우, 런타임 물리 충돌/레이캐스트 방해를 막기 위해 비활성화
            if (TryGetComponent<BoxCollider2D>(out var col))
            {
                col.enabled = false;
            }
        }

        public Bounds GetWorldBounds()
        {
            if (TryGetComponent<BoxCollider2D>(out var col))
            {
                Vector3 center = transform.TransformPoint(col.offset);
                Vector3 size = new Vector3(
                    Mathf.Abs(col.size.x * transform.lossyScale.x),
                    Mathf.Abs(col.size.y * transform.lossyScale.y),
                    1f
                );
                return new Bounds(center, size);
            }

            return new Bounds(transform.position, new Vector3(_size.x, _size.y, 1f));
        }

        /// <summary>
        /// 구역 사각형 내부에 포함되는 모든 그리드 셀 좌표를 반환한다.
        /// </summary>
        public List<Vector3Int> GetOverlappingCells(Grid grid)
        {
            List<Vector3Int> cells = new List<Vector3Int>();
            if (grid == null) return cells;

            Bounds bounds = GetWorldBounds();
            // 경계선 안쪽 타일만 정확히 포함하도록 약간의 여유값(0.05f) 축소
            Vector3 minWorld = bounds.min + new Vector3(0.05f, 0.05f, 0f);
            Vector3 maxWorld = bounds.max - new Vector3(0.05f, 0.05f, 0f);

            Vector3Int minCell = grid.WorldToCell(minWorld);
            Vector3Int maxCell = grid.WorldToCell(maxWorld);

            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                for (int y = minCell.y; y <= maxCell.y; y++)
                {
                    cells.Add(new Vector3Int(x, y, 0));
                }
            }

            return cells;
        }

        private void OnDrawGizmos()
        {
            Bounds bounds = GetWorldBounds();
            Color gizmoColor = GetZoneColor(_zoneType);

            // 반투명 내부 채우기
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.18f);
            Gizmos.DrawCube(bounds.center, bounds.size);

            // 외곽선 테두리
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.9f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }

        private static Color GetZoneColor(DungeonZoneType type)
        {
            switch (type)
            {
                case DungeonZoneType.Combat:   return new Color(1f, 0.25f, 0.25f);
                case DungeonZoneType.Corridor: return new Color(1f, 0.55f, 0.1f);
                default:                       return Color.white;
            }
        }
    }
}
