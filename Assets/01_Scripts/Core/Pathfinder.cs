using System.Collections.Generic;
using UnityEngine;

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
            Vector3Int startCell = grid.WorldToCell(startWorldPos);
            Vector3Int targetCell = grid.WorldToCell(targetWorldPos);
            Vector2Int startPos = new Vector2Int(startCell.x, startCell.y);
            Vector2Int targetPos = new Vector2Int(targetCell.x, targetCell.y);

            if (startPos == targetPos || IsObstacle(grid, targetPos, obstacleLayerMask))
            {
                return new List<Vector3>();
            }

            List<Node> openList = new List<Node>();
            HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();

            openList.Add(new Node(startPos, null, 0, GetDistance(startPos, targetPos)));

            while (openList.Count > 0)
            {
                Node currentNode = openList[0];
                for (int i = 1; i < openList.Count; i++)
                {
                    if (openList[i].FCost < currentNode.FCost || (openList[i].FCost == currentNode.FCost && openList[i].HCost < currentNode.HCost))
                    {
                        currentNode = openList[i];
                    }
                }

                openList.Remove(currentNode);
                closedSet.Add(currentNode.Position);

                if (currentNode.Position == targetPos)
                {
                    return RetracePath(grid, currentNode);
                }

                foreach (Vector2Int dir in Directions)
                {
                    Vector2Int neighborPos = currentNode.Position + dir;

                    if (closedSet.Contains(neighborPos) || IsObstacle(grid, neighborPos, obstacleLayerMask))
                        continue;

                    float dangerCost = 0f;
                    if (TileSystem.Instance != null)
                    {
                        dangerCost = TileSystem.Instance.GetDangerCost(new Vector3Int(neighborPos.x, neighborPos.y, 0));
                    }

                    float newCostToNeighbor = currentNode.GCost + GetDistance(currentNode.Position, neighborPos) + dangerCost;
                    Node neighborNode = openList.Find(n => n.Position == neighborPos);

                    if (neighborNode == null || newCostToNeighbor < neighborNode.GCost)
                    {
                        if (neighborNode == null)
                        {
                            neighborNode = new Node(neighborPos, currentNode, newCostToNeighbor, GetDistance(neighborPos, targetPos));
                            openList.Add(neighborNode);
                        }
                        else
                        {
                            neighborNode.GCost = newCostToNeighbor;
                            neighborNode.Parent = currentNode;
                        }
                    }
                }
            }

            return new List<Vector3>();
        }

        public static bool IsObstacle(Grid grid, Vector2Int position, LayerMask layerMask)
        {
            Vector3 worldPos = grid.GetCellCenterWorld(new Vector3Int(position.x, position.y, 0));
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
