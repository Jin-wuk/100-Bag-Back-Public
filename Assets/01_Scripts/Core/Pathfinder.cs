using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using BagBack100.Dungeon;

namespace BagBack100.Core
{
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
        /// <returns>월드 좌표 기준 경로 리스트 (도달 불가능 시 빈 리스트 반환)</returns>
        public static List<Vector3> FindPath(Grid grid, Vector3 startWorldPos, Vector3 targetWorldPos, LayerMask obstacleLayerMask)
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
                    if (TileSystem.Instance != null)
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

        public static bool IsObstacle(Grid grid, Vector2Int position, LayerMask layerMask)
        {
            if (grid == null) return true;
            Vector3Int cellPos = new Vector3Int(position.x, position.y, 0);

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
