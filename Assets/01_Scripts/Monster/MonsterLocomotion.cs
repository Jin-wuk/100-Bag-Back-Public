using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BagBack100.Core;
using BagBack100.Data.Core;

namespace BagBack100.Monster
{
    /// <summary>
    /// 몬스터의 평상시 이동(배회, 직진 추적, 측면 우회, 원거리 카이팅) 및 회피 기동을 전담하는 정적 헬퍼 클래스.
    /// 행동 패턴 쿨타임이 도는 동안에도 독립적으로 타일 포지셔닝을 수행합니다.
    /// </summary>
    public static class MonsterLocomotion
    {
        private static readonly Vector3Int[] FourDirs =
        {
            Vector3Int.up,
            Vector3Int.down,
            Vector3Int.left,
            Vector3Int.right
        };

        /// <summary>
        /// 몬스터의 MovementStyle에 맞춰 1회 이동 스텝을 수행합니다.
        /// </summary>
        public static IEnumerator ExecuteMoveStepRoutine(MonsterController monster)
        {
            if (monster == null || monster.Grid == null || monster.Data == null)
            {
                yield break;
            }

            if (monster.Player == null)
            {
                yield return PatrolStepRoutine(monster);
                yield break;
            }

            Vector3Int monsterCell = monster.GetCurrentCell();
            Vector3Int playerCell = monster.GetPlayerCell();
            int gridDist = GetChebyshevDistance(monsterCell, playerCell);

            // ZOC(인식 반경) 밖이면 평화롭게 배회
            if (gridDist > monster.Data.zocRadius)
            {
                yield return PatrolStepRoutine(monster);
                yield break;
            }

            switch (monster.Data.movementStyle)
            {
                case MonsterMovementStyle.HeavyChase:
                    yield return HeavyChaseStepRoutine(monster, monsterCell, playerCell);
                    break;

                case MonsterMovementStyle.AgileFlank:
                    yield return AgileFlankStepRoutine(monster, monsterCell, playerCell);
                    break;

                case MonsterMovementStyle.RangedKiting:
                    yield return RangedKitingStepRoutine(monster, monsterCell, playerCell, gridDist);
                    break;
            }
        }

        private static IEnumerator PatrolStepRoutine(MonsterController monster)
        {
            if (Random.value < 0.4f && TileSystem.Instance != null)
            {
                Vector3Int currentCell = monster.GetCurrentCell();
                Vector3Int targetCell = currentCell + FourDirs[Random.Range(0, FourDirs.Length)];

                if (CanStepToCell(monster, targetCell) &&
                    TileSystem.Instance.TryOccupyTile(targetCell, monster.gameObject, currentCell))
                {
                    Vector3 endPos = monster.Grid.GetCellCenterWorld(targetCell);
                    yield return monster.StartCoroutine(monster.MoveToTileRoutine(endPos, monster.Data.GetEffectiveMoveDuration()));
                }
            }
        }

        /// <summary>
        /// 1. 묵직한 근거리: 플레이어를 향해 A* 경로로 우직하게 1칸 전진합니다.
        /// </summary>
        private static IEnumerator HeavyChaseStepRoutine(MonsterController monster, Vector3Int monsterCell, Vector3Int playerCell)
        {
            int manhattanDist = GetManhattanDistance(monsterCell, playerCell);
            if (manhattanDist <= 1)
            {
                // 이미 플레이어 바로 옆 칸에 붙어 있으면 이동 생략
                yield break;
            }

            yield return MoveOneStepAlongAStar(monster, monsterCell, playerCell);
        }

        /// <summary>
        /// 2. 날렵한 근거리: 플레이어의 정면 시선(FacingDirection)을 피하고 측면/후방 인접 타일을 목표로 우회 기동합니다.
        /// </summary>
        private static IEnumerator AgileFlankStepRoutine(MonsterController monster, Vector3Int monsterCell, Vector3Int playerCell)
        {
            Vector3 playerFacing = monster.Player.FacingDirection.sqrMagnitude > 0.001f
                ? monster.Player.FacingDirection.normalized
                : Vector3.down;

            // 이미 플레이어 인접 타일에 있을 때: 정면에 서 있다면 측면 타일로 스텝 이동 시도
            if (GetManhattanDistance(monsterCell, playerCell) == 1)
            {
                Vector3 dirFromPlayer = ((Vector3)(monsterCell - playerCell)).normalized;
                float dot = Vector3.Dot(dirFromPlayer, playerFacing);

                // 이미 측면이나 후방(dot <= 0.2f)을 잡았다면 위치 유지
                if (dot <= 0.2f)
                {
                    yield break;
                }
            }

            // 플레이어 상하좌우 4칸 중 가장 유리한 측면/후방 타일 선정
            Vector3Int bestFlankCell = playerCell;
            float bestScore = float.MinValue;

            for (int i = 0; i < FourDirs.Length; i++)
            {
                Vector3Int candidate = playerCell + FourDirs[i];
                if (candidate != monsterCell && !CanStepToCell(monster, candidate))
                {
                    continue;
                }

                Vector3 offsetDir = new Vector3(FourDirs[i].x, FourDirs[i].y, 0f).normalized;
                float facingDot = Vector3.Dot(offsetDir, playerFacing); // -1: 등 뒤, 0: 측면, +1: 정면
                float distToMonster = GetManhattanDistance(monsterCell, candidate);
                float dangerPenalty = TileSystem.Instance != null ? TileSystem.Instance.GetDangerCost(candidate) : 0f;

                // 등 뒤/측면일수록 높은 점수, 현재 위치에서 가까울수록 가산점, 위험 타일 감점
                float score = (-facingDot * 10f) - (distToMonster * 1.5f) - dangerPenalty;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestFlankCell = candidate;
                }
            }

            if (bestFlankCell != monsterCell)
            {
                yield return MoveOneStepAlongAStar(monster, monsterCell, bestFlankCell);
            }
        }

        /// <summary>
        /// 3. 원거리 투사체: 적정 사거리(preferredRange)를 유지하며 가까워지면 후퇴/횡이동(카이팅), 멀어지면 접근합니다.
        /// </summary>
        private static IEnumerator RangedKitingStepRoutine(MonsterController monster, Vector3Int monsterCell, Vector3Int playerCell, int gridDist)
        {
            int preferredRange = Mathf.Max(3, monster.Data.range);

            // 1) 플레이어가 안전거리 안쪽으로 파고들었을 때 -> 반대 방향 또는 측면(벽 우회)으로 1칸 후퇴
            if (gridDist < preferredRange)
            {
                Vector3Int bestRetreatCell = monsterCell;
                float bestScore = float.MinValue;
                float currentRealDist = Vector3.Distance(monster.Grid.GetCellCenterWorld(monsterCell), monster.Player.transform.position);

                for (int i = 0; i < FourDirs.Length; i++)
                {
                    Vector3Int candidate = monsterCell + FourDirs[i];
                    if (!CanStepToCell(monster, candidate)) continue;

                    Vector3 candidateWorld = monster.Grid.GetCellCenterWorld(candidate);
                    float newDist = Vector3.Distance(candidateWorld, monster.Player.transform.position);
                    float dangerPenalty = TileSystem.Instance != null ? TileSystem.Instance.GetDangerCost(candidate) : 0f;

                    // 플레이어로부터 멀어지는 타일 우선 (뒤가 벽이면 옆 칸이라도 선택해 코너 탈출)
                    float score = (newDist - currentRealDist) * 10f - dangerPenalty;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestRetreatCell = candidate;
                    }
                }

                if (bestRetreatCell != monsterCell &&
                    TileSystem.Instance != null &&
                    TileSystem.Instance.TryOccupyTile(bestRetreatCell, monster.gameObject, monsterCell))
                {
                    Vector3 endPos = monster.Grid.GetCellCenterWorld(bestRetreatCell);
                    yield return monster.StartCoroutine(monster.MoveToTileRoutine(endPos, monster.Data.GetEffectiveMoveDuration()));
                }
                yield break;
            }

            // 2) 플레이어가 적정 사거리보다 멀리 있을 때 -> 사거리 안으로 1칸 접근
            if (gridDist > preferredRange + 1)
            {
                yield return MoveOneStepAlongAStar(monster, monsterCell, playerCell);
                yield break;
            }

            // 3) 적정 사거리 유지 중일 때 -> 35% 확률로 좌우 횡이동(Strafing)하여 고정 과녁이 되지 않게 함
            if (Random.value < 0.35f && TileSystem.Instance != null)
            {
                Vector3 toPlayer = (monster.Player.transform.position - monster.transform.position).normalized;
                Vector3 perp = Random.value < 0.5f ? new Vector3(-toPlayer.y, toPlayer.x, 0f) : new Vector3(toPlayer.y, -toPlayer.x, 0f);
                Vector3Int stepDir = SnapTo4WayCell(perp);
                Vector3Int strafeCell = monsterCell + stepDir;

                if (CanStepToCell(monster, strafeCell) &&
                    TileSystem.Instance.GetDangerCost(strafeCell) <= 0f &&
                    TileSystem.Instance.TryOccupyTile(strafeCell, monster.gameObject, monsterCell))
                {
                    Vector3 endPos = monster.Grid.GetCellCenterWorld(strafeCell);
                    yield return monster.StartCoroutine(monster.MoveToTileRoutine(endPos, monster.Data.GetEffectiveMoveDuration()));
                }
            }
        }

        private static IEnumerator MoveOneStepAlongAStar(MonsterController monster, Vector3Int startCell, Vector3Int targetCell)
        {
            Vector3 startWorld = monster.Grid.GetCellCenterWorld(startCell);
            Vector3 targetWorld = monster.Grid.GetCellCenterWorld(targetCell);

            List<Vector3> path = Pathfinder.FindPath(monster.Grid, startWorld, targetWorld, monster.ObstacleLayerMask);
            if (path == null || path.Count == 0)
            {
                yield break;
            }

            Vector3 nextStepWorld = path[0];
            Vector3Int nextCell = monster.Grid.WorldToCell(nextStepWorld);
            nextCell.z = 0;

            // 플레이어가 서 있는 타일 자체를 밟지는 않도록 방지
            if (nextCell == monster.GetPlayerCell())
            {
                yield break;
            }

            if (TileSystem.Instance != null && TileSystem.Instance.TryOccupyTile(nextCell, monster.gameObject, startCell))
            {
                yield return monster.StartCoroutine(monster.MoveToTileRoutine(nextStepWorld, monster.Data.GetEffectiveMoveDuration()));
            }
        }

        public static bool CanStepToCell(MonsterController monster, Vector3Int cell)
        {
            cell.z = 0;
            if (Pathfinder.IsObstacle(monster.Grid, new Vector2Int(cell.x, cell.y), monster.ObstacleLayerMask))
            {
                return false;
            }
            if (TileSystem.Instance != null && TileSystem.Instance.IsTileOccupied(cell))
            {
                GameObject occupant = TileSystem.Instance.GetEntityAtTile(cell);
                if (occupant != null && occupant != monster.gameObject)
                {
                    return false;
                }
            }
            return true;
        }

        public static Vector3Int SnapTo4WayCell(Vector3 dir)
        {
            if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            {
                return dir.x >= 0f ? Vector3Int.right : Vector3Int.left;
            }
            return dir.y >= 0f ? Vector3Int.up : Vector3Int.down;
        }

        public static Vector3Int SnapTo8WayCell(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return Vector3Int.down;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float snappedAngle = Mathf.Round(angle / 45f) * 45f * Mathf.Deg2Rad;
            return new Vector3Int(Mathf.RoundToInt(Mathf.Cos(snappedAngle)), Mathf.RoundToInt(Mathf.Sin(snappedAngle)), 0);
        }

        public static int GetManhattanDistance(Vector3Int a, Vector3Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        public static int GetChebyshevDistance(Vector3Int a, Vector3Int b)
        {
            return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }
    }
}
