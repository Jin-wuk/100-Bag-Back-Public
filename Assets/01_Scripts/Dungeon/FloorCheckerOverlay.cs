using UnityEngine;
using UnityEngine.Tilemaps;
using BagBack100.Core;

namespace BagBack100.Dungeon
{
    /// <summary>
    /// Role: 층에 들어갈 때마다 바닥 칸을 따라 이동 칸 체크무늬((x+y) 짝수 칸, 옅은 어두운 색)를 깐다. 바닥 그림에는 칸 선이 없어서 칸을 구분하는 용도.
    ///       체크 타일맵은 현재 층 루트의 자식으로 만들어 층이 바뀌면 함께 사라진다.
    /// Dependencies: DungeonGenerator(현재 층 Floor 타일맵)
    /// Events: 없음 (구독: GlobalEvents.OnFloorEntered)
    /// </summary>
    public class FloorCheckerOverlay : MonoBehaviour
    {
        private const string CheckerName = "Checker";

        [SerializeField] private TileBase _checkerTile;
        [SerializeField] private Color _checkerColor = new Color(0f, 6f / 255f, 30f / 255f, 0.08f);
        [Tooltip("바닥 타일맵(-20) 바로 위, 외곽 벽(0)·계단(-10)보다 아래")]
        [SerializeField] private int _sortingOrder = -19;

        private void OnEnable()
        {
            GlobalEvents.OnFloorEntered += HandleFloorEntered;
        }

        private void OnDisable()
        {
            GlobalEvents.OnFloorEntered -= HandleFloorEntered;
        }

        private void HandleFloorEntered(int regionId, string regionName, int floorNumber)
        {
            if (_checkerTile == null || DungeonGenerator.Instance == null) return;
            Tilemap floor = DungeonGenerator.Instance.FloorTilemap;
            if (floor == null) return;
            Build(floor);
        }

        private void Build(Tilemap floor)
        {
            Transform layoutRoot = floor.transform.parent;
            if (layoutRoot == null) return;

            Transform existing = layoutRoot.Find(CheckerName);
            Tilemap checker;
            if (existing != null && existing.TryGetComponent(out checker))
            {
                checker.ClearAllTiles();
            }
            else
            {
                var go = new GameObject(CheckerName, typeof(Tilemap), typeof(TilemapRenderer));
                go.transform.SetParent(layoutRoot, false);
                checker = go.GetComponent<Tilemap>();
                var renderer = go.GetComponent<TilemapRenderer>();
                renderer.sortingOrder = _sortingOrder;
                // 바닥과 같은 정렬 레이어에 둔다(나중에 레이어를 나눠도 바닥 바로 위에 오도록)
                if (floor.TryGetComponent(out TilemapRenderer floorRenderer)) renderer.sortingLayerID = floorRenderer.sortingLayerID;
            }
            checker.color = _checkerColor;

            floor.CompressBounds();
            foreach (Vector3Int cell in floor.cellBounds.allPositionsWithin)
            {
                if (((cell.x + cell.y) & 1) != 0) continue;
                if (!floor.HasTile(cell)) continue;
                checker.SetTile(cell, _checkerTile);
            }
        }
    }
}
