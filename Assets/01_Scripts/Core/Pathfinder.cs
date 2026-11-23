using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using BagBack100.Dungeon;

namespace BagBack100.Core
{
    /// <summary>
    /// Role: 타일 A* 경로 탐색과 칸 장애물 판정(바닥 없음·벽·물리 장애물·고정 점유물).
    /// Dependencies: DungeonGenerator(현재 층 타일맵), TileSystem(고정 장애물·위험 칸)
    /// Events: 없음
    /// </summary>
    public class Pathfinder
    {
        private class Node
        {
            public Vector2Int Position;
            public Node Parent;
            public float GCost;
            public float HCost;
            public float FCost => GCost + HCost;

            public Node(Vector2Int pos, Node parent, float gCost, float hCost)
            {
                Position = pos;
                Parent = parent;
                GCost = gCost;
                HCost = hCost;
            }
        }

        private static readonly Vector2Int[] Directions = new Vector2Int[]
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };

        private const int MaxSearchIterations = 250;
        private static Tilemap _cachedFloorTilemap;

        /// <summary>
        /// A* 알고리즘을 이용해 경로를 찾습니다.
        /// </summary>
        /// <param name="grid">씬 내의 Grid 컴포넌트</param>
        /// <param name="startWorldPos">시작 월드 좌표</param>
        /// <param name="targetWorldPos">목표 월드 좌표</param>
        /// <param name="obstacleLayerMask">장애물로 인식할 레이어 마스크 (Wall 등)</param>
        /// <param name="useDangerCost">
        /// 위험 칸(공격 경고) 회피 비용을 쓸지. AI용. 플레이어는 false: 입력한 칸으로 가야 하고, 위험 칸이 목표면 비용(+50)만큼
        /// 탐색이 넓게 퍼져 MaxSearchIterations를 넘기고 바로 옆 칸 이동도 실패한다
        /// </param>
        /// <returns>월드 좌표 기준 경로 리스트 (도달 불가능 시 빈 리스트 반환)</returns>
        public static List<Vector3> FindPath(Grid grid, Vector3 startWorldPos, Vector3 targetWorldPos, LayerMask obstacleLayerMask, bool useDangerCost = true)
        {
            if (grid == null) return new List<Vector3>();

            Vector3Int startCell = grid.WorldToCell(startWorldPos);
            Vector3Int targetCell = grid.WorldToCell(targetWorldPos);
            Vector2Int startPos = new Vector2Int(startCell.x, startCell.y);
            Vector2Int targetPos = new Vector2Int(targetCell.x, targetCell.y);

            if (startPos == targetPos || IsObstacle(grid, targetPos, obstacleLayerMask))
            {
                return new List<Vector3>();
            }

            List<Node> openList = new List<Node>();
            Dictionary<Vector2Int, Node> openDict = new Dictionary<Vector2Int, Node>();
            HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();

            Node startNode = new Node(startPos, null, 0, GetDistance(startPos, targetPos));
            openList.Add(startNode);
            openDict[startPos] = startNode;

            int iterations = 0;

            while (openList.Count > 0)
            {
                // 무한 루프 및 과도한 연산으로 인한 유니티 멈춤(Freezing) 방지 안전 캡
                if (++iterations > MaxSearchIterations)
                {
                    return new List<Vector3>();
                }

                int bestIndex = 0;
                Node currentNode = openList[0];
                for (int i = 1; i < openList.Count; i++)
                {
                    if (openList[i].FCost < currentNode.FCost || (openList[i].FCost == currentNode.FCost && openList[i].HCost < currentNode.HCost))
                    {
                        currentNode = openList[i];
                        bestIndex = i;
                    }
                }

                openList.RemoveAt(bestIndex);
                openDict.Remove(currentNode.Position);
                closedSet.Add(currentNode.Position);

                if (currentNode.Position == targetPos)
                {
                    return RetracePath(grid, currentNode);
                }

                for (int d = 0; d < Directions.Length; d++)
                {
                    Vector2Int neighborPos = currentNode.Position + Directions[d];

                    if (closedSet.Contains(neighborPos) || IsObstacle(grid, neighborPos, obstacleLayerMask))
                        continue;

                    float dangerCost = 0f;
                    if (useDangerCost && TileSystem.Instance != null)
                    {
                        dangerCost = TileSystem.Instance.GetDangerCost(new Vector3Int(neighborPos.x, neighborPos.y, 0));
                    }

                    float newCostToNeighbor = currentNode.GCost + GetDistance(currentNode.Position, neighborPos) + dangerCost;

                    if (!openDict.TryGetValue(neighborPos, out Node neighborNode))
                    {
                        neighborNode = new Node(neighborPos, currentNode, newCostToNeighbor, GetDistance(neighborPos, targetPos));
                        openList.Add(neighborNode);
                        openDict[neighborPos] = neighborNode;
                    }
                    else if (newCostToNeighbor < neighborNode.GCost)
                    {
                        neighborNode.GCost = newCostToNeighbor;
                        neighborNode.Parent = currentNode;
                    }
                }
            }

            return new List<Vector3>();
        }

        /// <param name="includeStaticBlockers">
        /// 상자 같은 고정 점유물을 막힌 칸으로 볼지. 이동·돌진·드랍 칸 계산은 true, 시야(LOS) 판정은 false
        /// (상자는 길은 막지만 시야는 가리지 않는다).
        /// </param>
        public static bool IsObstacle(Grid grid, Vector2Int position, LayerMask layerMask, bool includeStaticBlockers = true)
        {
            if (grid == null) return true;
            Vector3Int cellPos = new Vector3Int(position.x, position.y, 0);

            // 0. 상자처럼 칸을 영구히 막는 고정 점유물은 벽과 같이 취급 (몬스터 A*·돌진 궤적·드랍 칸 계산 공통)
            if (includeStaticBlockers && TileSystem.Instance != null && TileSystem.Instance.IsStaticBlocker(cellPos))
            {
                return true;
            }

            // 1. 던전 생성기 타일맵 참조를 통한 바닥/벽 타일 유효성 검사 (순환 호출 방지)
            if (DungeonGenerator.Instance != null)
            {
                var gen = DungeonGenerator.Instance;
                if (gen.FloorTilemap != null && !gen.FloorTilemap.HasTile(cellPos))
                {
                    return true;
                }
                if (gen.WallTilemap != null && gen.WallTilemap.HasTile(cellPos))
                {
                    return true;
                }
            }
            else
            {
                // 던전 제너레이터가 없는 씬을 위한 바닥 타일맵 캐시 및 유효성 검사 (공허 영역 무한 탐색 방지)
                if (_cachedFloorTilemap == null)
                {
                    Tilemap[] tilemaps = grid.GetComponentsInChildren<Tilemap>();
                    for (int i = 0; i < tilemaps.Length; i++)
                    {
                        string lower = tilemaps[i].gameObject.name.ToLowerInvariant();
                        if (lower.Contains("floor") || lower.Contains("ground") || lower.Contains("바닥"))
                        {
                            _cachedFloorTilemap = tilemaps[i];
                            break;
                        }
                    }
                }

                if (_cachedFloorTilemap != null && !_cachedFloorTilemap.HasTile(cellPos))
                {
                    return true;
                }
            }

            // 2. 물리적 벽/기둥 레이어 충돌 검사
            Vector3 worldPos = grid.GetCellCenterWorld(cellPos);
            return Physics2D.OverlapPoint(worldPos, layerMask) != null;
        }

        /// <summary>
        /// 여러 칸 몸(N×N, anchor = 좌하단)용 A*. 노드는 anchor이고, 몸 칸이 모두 장애물이 아닌 anchor만 지난다.
        /// 목표는 몸이 targetCell과 변으로 맞닿는 위치(몸–목표 맨해튼 거리 1). 목표 칸 자체는 장애물 검사하지 않는다(플레이어가 벽 옆에 서도 찾도록).
        /// isFirstStepBlocked: 시작 anchor의 이웃(첫 걸음)에만 추가로 적용하는 막힘 조건(다른 몬스터 점유 등).
        /// 경로 대신 첫 걸음 anchor만 돌려준다(한 걸음씩 다시 계산).
        /// </summary>
        public static bool TryFindNextBodyStep(Grid grid, Vector3Int startAnchor, int size, Vector3Int targetCell, LayerMask obstacleLayerMask,
            System.Func<Vector3Int, bool> isFirstStepBlocked, out Vector3Int nextAnchor)
        {
            nextAnchor = startAnchor;
            if (grid == null) return false;

            Vector2Int startPos = new Vector2Int(startAnchor.x, startAnchor.y);
            Vector2Int target = new Vector2Int(targetCell.x, targetCell.y);
            if (BodyGap(startPos, size, target) <= 1) return false;

            List<Node> openList = new List<Node>();
            Dictionary<Vector2Int, Node> openDict = new Dictionary<Vector2Int, Node>();
            HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();

            Node startNode = new Node(startPos, null, 0, BodyGap(startPos, size, target) - 1);
            openList.Add(startNode);
            openDict[startPos] = startNode;

            int iterations = 0;
            while (openList.Count > 0)
            {
                if (++iterations > MaxSearchIterations) return false;

                int bestIndex = 0;
                Node currentNode = openList[0];
                for (int i = 1; i < openList.Count; i++)
                {
                    if (openList[i].FCost < currentNode.FCost || (openList[i].FCost == currentNode.FCost && openList[i].HCost < currentNode.HCost))
                    {
                        currentNode = openList[i];
                        bestIndex = i;
                    }
                }

                openList.RemoveAt(bestIndex);
                openDict.Remove(currentNode.Position);
                closedSet.Add(currentNode.Position);

                if (BodyGap(currentNode.Position, size, target) == 1)
                {
                    Node step = currentNode;
                    while (step.Parent != null && step.Parent != startNode) step = step.Parent;
                    if (step == startNode) return false;
                    nextAnchor = new Vector3Int(step.Position.x, step.Position.y, 0);
                    return true;
                }

                for (int d = 0; d < Directions.Length; d++)
                {
                    Vector2Int neighborPos = currentNode.Position + Directions[d];
                    if (closedSet.Contains(neighborPos)) continue;
                    // 몸이 목표 칸을 덮는 위치는 지나지 않는다
                    if (BodyGap(neighborPos, size, target) == 0) continue;
                    if (IsBodyBlocked(grid, neighborPos, size, obstacleLayerMask, out float dangerCost)) continue;
                    if (currentNode == startNode && isFirstStepBlocked != null && isFirstStepBlocked(new Vector3Int(neighborPos.x, neighborPos.y, 0))) continue;

                    float newCost = currentNode.GCost + 1f + dangerCost;
                    if (!openDict.TryGetValue(neighborPos, out Node neighborNode))
                    {
                        neighborNode = new Node(neighborPos, currentNode, newCost, BodyGap(neighborPos, size, target) - 1);
                        openList.Add(neighborNode);
                        openDict[neighborPos] = neighborNode;
                    }
                    else if (newCost < neighborNode.GCost)
                    {
                        neighborNode.GCost = newCost;
                        neighborNode.Parent = currentNode;
                    }
                }
            }
            return false;
        }

        // 몸 사각형에서 목표 칸까지의 맨해튼 거리(몸 안이면 0)
        private static int BodyGap(Vector2Int anchor, int size, Vector2Int target)
        {
            int dx = target.x < anchor.x ? anchor.x - target.x : (target.x > anchor.x + size - 1 ? target.x - (anchor.x + size - 1) : 0);
            int dy = target.y < anchor.y ? anchor.y - target.y : (target.y > anchor.y + size - 1 ? target.y - (anchor.y + size - 1) : 0);
            return dx + dy;
        }

        // 몸 칸 중 하나라도 장애물이면 막힘. 위험 칸 비용은 몸 칸 합
        private static bool IsBodyBlocked(Grid grid, Vector2Int anchor, int size, LayerMask layerMask, out float dangerCost)
        {
            dangerCost = 0f;
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    Vector2Int cell = new Vector2Int(anchor.x + x, anchor.y + y);
                    if (IsObstacle(grid, cell, layerMask)) return true;
                    if (TileSystem.Instance != null) dangerCost += TileSystem.Instance.GetDangerCost(new Vector3Int(cell.x, cell.y, 0));
                }
            }
            return false;
        }

        private static float GetDistance(Vector2Int nodeA, Vector2Int nodeB)
        {
            int dstX = Mathf.Abs(nodeA.x - nodeB.x);
            int dstY = Mathf.Abs(nodeA.y - nodeB.y);
            return dstX + dstY;
        }

        private static List<Vector3> RetracePath(Grid grid, Node endNode)
        {
            List<Vector3> path = new List<Vector3>();
            Node currentNode = endNode;

            while (currentNode.Parent != null)
            {
                path.Add(grid.GetCellCenterWorld(new Vector3Int(currentNode.Position.x, currentNode.Position.y, 0)));
                currentNode = currentNode.Parent;
            }
            
            path.Reverse();
            return path;
        }
    }
}
