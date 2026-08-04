// File: Assets/Scripts/Gameplay/Pathfinder.cs
using System.Collections.Generic;
using UnityEngine;

namespace DimensionLooter.Gameplay
{
    public static class Pathfinder
    {
        private class Node
        {
            public Vector2Int Position;
            public Node Parent;
            public int G; // Cost from start
            public int H; // Heuristic cost to target
            public int F => G + H;

            public Node(Vector2Int pos) { Position = pos; }
        }

        private static readonly Vector2Int[] Directions = {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        /// <summary>
        /// A* 알고리즘을 사용하여 4방향 그리드 최단 경로를 탐색합니다.
        /// </summary>
        public static List<Vector2Int> FindPath(Vector2Int start, Vector2Int target, LayerMask obstacleMask)
        {
            var openList = new List<Node>();
            var closedList = new HashSet<Vector2Int>();
            var nodeDict = new Dictionary<Vector2Int, Node>();

            var startNode = new Node(start) { G = 0, H = GetDistance(start, target) };
            openList.Add(startNode);
            nodeDict[start] = startNode;

            int safetyCount = 0;

            while (openList.Count > 0 && safetyCount < 1000)
            {
                safetyCount++;
                
                // 가장 F 비용이 낮은 노드 선택
                Node current = openList[0];
                for (int i = 1; i < openList.Count; i++)
                {
                    if (openList[i].F < current.F || (openList[i].F == current.F && openList[i].H < current.H))
                    {
                        current = openList[i];
                    }
                }

                openList.Remove(current);
                closedList.Add(current.Position);

                // 타겟 도달
                if (current.Position == target)
                {
                    return RetracePath(startNode, current);
                }

                // 인접 노드 탐색 (4방향)
                foreach (var dir in Directions)
                {
                    Vector2Int neighborPos = current.Position + dir;

                    if (closedList.Contains(neighborPos)) continue;

                    // 타겟 위치 자체는 충돌체가 있어도(몬스터 등) 진입 시도 가능하도록 처리
                    if (neighborPos != target && !IsWalkable(neighborPos, obstacleMask))
                    {
                        closedList.Add(neighborPos); // 갈 수 없는 벽
                        continue;
                    }

                    int newCostToNeighbor = current.G + 10; // 타일 1칸 비용 10 (대각선이 없으므로 고정)

                    if (!nodeDict.TryGetValue(neighborPos, out Node neighborNode))
                    {
                        neighborNode = new Node(neighborPos);
                        nodeDict[neighborPos] = neighborNode;
                    }

                    if (newCostToNeighbor < neighborNode.G || !openList.Contains(neighborNode))
                    {
                        neighborNode.G = newCostToNeighbor;
                        neighborNode.H = GetDistance(neighborPos, target);
                        neighborNode.Parent = current;

                        if (!openList.Contains(neighborNode))
                        {
                            openList.Add(neighborNode);
                        }
                    }
                }
            }

            // 경로를 찾지 못함
            return null;
        }

        private static List<Vector2Int> RetracePath(Node start, Node end)
        {
            var path = new List<Vector2Int>();
            Node current = end;

            while (current != start)
            {
                path.Add(current.Position);
                current = current.Parent;
            }
            path.Reverse();
            return path;
        }

        private static int GetDistance(Vector2Int a, Vector2Int b)
        {
            // 맨해튼 거리 (4방향 전용 휴리스틱)
            return Mathf.Abs(a.x - b.x) * 10 + Mathf.Abs(a.y - b.y) * 10;
        }

        private static bool IsWalkable(Vector2Int pos, LayerMask mask)
        {
            // 타일 중앙점(pos)에 반지름 0.4f 원형 레이캐스트를 쏴서 장애물이 있는지 확인
            // (0.5f로 하면 인접 타일 벽에 걸릴 수 있으므로 0.4f 사용)
            Collider2D hit = Physics2D.OverlapCircle(new Vector2(pos.x, pos.y), 0.4f, mask);
            return hit == null;
        }
    }
}
