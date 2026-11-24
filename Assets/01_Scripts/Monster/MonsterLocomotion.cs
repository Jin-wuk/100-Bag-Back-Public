using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BagBack100.Core;
using BagBack100.Data.Core;
using BagBack100.Dungeon;

namespace BagBack100.Monster
{
    /// <summary>
    /// Role: 몬스터의 평상시 이동(배회, 직진 추적, 측면 우회, 원거리 카이팅) 및 회피 기동을 전담하는 정적 헬퍼 클래스.
    /// 행동 패턴 쿨타임이 도는 동안에도 독립적으로 타일 포지셔닝을 수행합니다.
    /// Dependencies: MonsterController, TileSystem, Pathfinder
    /// Events: 없음
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

            if (monster.Player == null || !monster.IsAggroed)
            {
                yield return PatrolStepRoutine(monster);
                yield break;
            }

            Vector3Int monsterCell = monster.GetCurrentCell();
            Vector3Int playerCell = monster.GetPlayerCell();
            int gridDist = monster.GetBodyChebyshevDistance(playerCell);

            // 여러 칸 몸은 측면 우회·카이팅을 지원하지 않는다(보스 전용). 스타일과 무관하게 직진 추적
            if (monster.Footprint > 1)
            {
                yield return HeavyChaseBodyStepRoutine(monster, monsterCell, playerCell);
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

                if (CanPlaceBody(monster, targetCell) && monster.TryMoveBody(targetCell))
                {
                    Vector3 endPos = monster.GetBodyCenterWorld(targetCell);
                    yield return monster.StartActionCoroutine(monster.MoveToTileRoutine(endPos, monster.Data.GetEffectiveMoveDuration()));
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

                if (bestRetreatCell != monsterCell && monster.TryMoveBody(bestRetreatCell))
                {
                    Vector3 endPos = monster.Grid.GetCellCenterWorld(bestRetreatCell);
                    yield return monster.StartActionCoroutine(monster.MoveToTileRoutine(endPos, monster.Data.GetEffectiveMoveDuration()));
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
                    monster.TryMoveBody(strafeCell))
                {
                    Vector3 endPos = monster.Grid.GetCellCenterWorld(strafeCell);
                    yield return monster.StartActionCoroutine(monster.MoveToTileRoutine(endPos, monster.Data.GetEffectiveMoveDuration()));
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

            // 위험 칸을 피하는 길이 너무 길면(1칸 통로에서 플레이어 등 뒤로 도는 길 등) 돌아가지 않고 위험이 풀릴 때까지 기다린다.
            // 맨해튼 거리는 위험 무시 경로 길이의 하한이라, 그 안쪽이면 두 번째 A*를 생략한다
            int detourLimit = monster.Data.dangerDetourLimit;
            int manhattan = Mathf.Abs(targetCell.x - startCell.x) + Mathf.Abs(targetCell.y - startCell.y);
            if (path.Count > manhattan + detourLimit)
            {
                List<Vector3> plainPath = Pathfinder.FindPath(monster.Grid, startWorld, targetWorld, monster.ObstacleLayerMask, false);
                if (plainPath.Count > 0 && path.Count > plainPath.Count + detourLimit)
                {
                    yield break;
                }
            }

            Vector3 nextStepWorld = path[0];
            Vector3Int nextCell = monster.Grid.WorldToCell(nextStepWorld);
            nextCell.z = 0;

            // 플레이어가 서 있는 타일 자체를 밟지는 않도록 방지
            if (nextCell == monster.GetPlayerCell())
            {
                yield break;
            }

            if (monster.TryMoveBody(nextCell))
            {
                yield return monster.StartActionCoroutine(monster.MoveToTileRoutine(nextStepWorld, monster.Data.GetEffectiveMoveDuration()));
            }
        }

        /// <summary>
        /// 여러 칸 몸: 플레이어 쪽으로 몸 단위 A* 한 걸음. 몸이 플레이어와 변으로 맞닿으면(맨해튼 1) 멈춘다.
        /// </summary>
        private static IEnumerator HeavyChaseBodyStepRoutine(MonsterController monster, Vector3Int anchor, Vector3Int playerCell)
        {
            int size = monster.Footprint;
            if (MonsterController.GetBodyManhattanDistance(anchor, size, playerCell) <= 1)
            {
                yield break;
            }

            // 첫 걸음은 다른 몬스터가 막은 칸도 피한다. 장애물만 보는 경로가 잡몹 쪽으로 나면 매번 점유에 실패해 제자리에 서게 된다
            bool found = Pathfinder.TryFindNextBodyStep(monster.Grid, anchor, size, playerCell, monster.ObstacleLayerMask,
                next => !CanPlaceBody(monster, next), out Vector3Int nextAnchor);
            if (!found || !CanPlaceBody(monster, nextAnchor)) yield break;

            if (monster.TryMoveBody(nextAnchor))
            {
                yield return monster.StartActionCoroutine(monster.MoveToTileRoutine(monster.GetBodyCenterWorld(nextAnchor), monster.Data.GetEffectiveMoveDuration()));
            }
        }

        /// <summary>몸(anchor = 좌하단)을 그 자리에 놓을 수 있는지: 몸 칸 전부 걸을 수 있고, 다른 엔티티가 없고, 플레이어 칸이 아님. 1칸이면 CanStepToCell과 같다.</summary>
        public static bool CanPlaceBody(MonsterController monster, Vector3Int anchor)
        {
            int size = monster.Footprint;
            if (size <= 1) return CanStepToCell(monster, anchor);

            Vector3Int playerCell = monster.GetPlayerCell();
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    Vector3Int cell = new Vector3Int(anchor.x + x, anchor.y + y, 0);
                    if (monster.Player != null && cell == playerCell) return false;
                    if (!CanStepToCell(monster, cell)) return false;
                }
            }
            return true;
        }

        public static bool CanStepToCell(MonsterController monster, Vector3Int cell)
        {
            cell.z = 0;

            // 1. 던전 타일맵 유효성 (바닥 존재 여부 & 벽 타일 여부 & 물리 콜라이더)
            if (DungeonGenerator.Instance != null)
            {
                if (!DungeonGenerator.Instance.IsCellWalkable(cell))
                {
                    return false;
                }
            }
            else
            {
                // 던전 제너레이터가 없는 씬용 폴백
                if (Pathfinder.IsObstacle(monster.Grid, new Vector2Int(cell.x, cell.y), monster.ObstacleLayerMask))
                {
                    return false;
                }
            }

            // 2. 다른 엔티티(몬스터/플레이어 등) 타일 점유 여부
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
