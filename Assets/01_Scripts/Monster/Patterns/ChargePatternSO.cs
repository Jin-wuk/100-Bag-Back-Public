using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BagBack100.Core;

namespace BagBack100.Monster
{
    /// <summary>
    /// Role: 일직선 경로에 경고 장판을 깔고 돌진하는 패턴 레고 블록.
    /// 플레이어가 옆으로 피해서 몬스터가 벽(Obstacle)에 충돌하면 기절(Stun)하여 플레이어에게 프리딜 타임을 제공합니다.
    /// Dependencies: MonsterController, MonsterLocomotion, TileSystem, Pathfinder
    /// Events: GlobalEvents.OnCombatImpact (벽 충돌 시)
    /// </summary>
    [CreateAssetMenu(fileName = "Pattern_LinearCharge", menuName = "BagBack100/Monster/Patterns/Linear Charge")]
    public class ChargePatternSO : MonsterPatternSO
    {
        [Header("돌진 패턴 설정")]
        [Tooltip("최대 돌진 타일 거리")]
        [Range(2, 8)]
        public int chargeDistance = 4;
        [Tooltip("돌진 중 타일 1칸 돌파에 걸리는 시간(초)")]
        [Min(0.04f)]
        public float stepMoveDuration = 0.08f;
        [Tooltip("돌진 중 벽(장애물)에 박았을 때 기절(Stun)하는 시간(초) - 유저의 파훼 보상")]
        [Min(0f)]
        public float wallStunDuration = 1.5f;

        public override IEnumerator ExecuteRoutine(MonsterController monster)
        {
            if (monster == null || monster.Grid == null || monster.Player == null || TileSystem.Instance == null)
            {
                yield break;
            }

            Vector3Int startAnchor = monster.GetCurrentCell();
            int size = monster.Footprint;
            Vector3 toPlayer = (monster.Player.transform.position - monster.transform.position).normalized;
            Vector3Int chargeDir = MonsterLocomotion.SnapTo4WayCell(toPlayer);

            // 스텝마다 몸 좌하단 칸(anchor)과, 몸이 새로 들어가는 앞쪽 가장자리 칸(예고 장판)을 따로 모은다
            List<Vector3Int> chargeAnchors = monster.PatternAnchorsBuffer;
            List<Vector3Int> sweptCells = monster.PatternCellsBuffer;
            chargeAnchors.Clear();
            sweptCells.Clear();
            float telegraph = GetEffectiveTelegraphDuration(monster);
            monster.NotifyAttackWindup(telegraph);
            bool willHitWall = false;

            // 1. 돌진 궤적 계산 및 바닥 장판 예고 (1칸 몸이면 앞 칸 하나씩 = 기존과 같음)
            for (int step = 1; step <= chargeDistance; step++)
            {
                Vector3Int nextAnchor = startAnchor + (chargeDir * step);
                int edgeStart = sweptCells.Count;
                AddLeadingEdge(nextAnchor, size, chargeDir, sweptCells);
                bool blocked = false;
                for (int i = edgeStart; i < sweptCells.Count; i++)
                {
                    Vector3Int c = sweptCells[i];
                    if (Pathfinder.IsObstacle(monster.Grid, new Vector2Int(c.x, c.y), monster.ObstacleLayerMask))
                    {
                        blocked = true;
                        break;
                    }
                }
                if (blocked)
                {
                    sweptCells.RemoveRange(edgeStart, sweptCells.Count - edgeStart);
                    willHitWall = true;
                    break;
                }
                chargeAnchors.Add(nextAnchor);
            }
            for (int i = 0; i < sweptCells.Count; i++)
            {
                monster.SpawnWarning(monster.Grid.GetCellCenterWorld(sweptCells[i]), telegraph);
            }

            yield return new WaitForSeconds(telegraph);

            // 2. 고속 직선 돌진 실행
            bool hasHitPlayer = false;
            int damage = CalculateDamage(monster);
            Vector3 hitDirWorld = new Vector3(chargeDir.x, chargeDir.y, 0f).normalized;
            List<Vector3Int> edgeBuffer = monster.PatternCellsBuffer;
            // 다른 몬스터에 막혀 중간에 멈추면 벽에 박은 것이 아니므로 기절하지 않는다
            bool stoppedByEntity = false;

            for (int i = 0; i < chargeAnchors.Count; i++)
            {
                Vector3Int stepAnchor = chargeAnchors[i];

                // 몸이 들어갈 앞줄에 플레이어가 있으면 점유하기 전에 때리고 그 앞에서 멈춘다(플레이어 칸을 덮지 않음)
                edgeBuffer.Clear();
                AddLeadingEdge(stepAnchor, size, chargeDir, edgeBuffer);
                Vector3Int playerCell = monster.GetPlayerCell();
                if (!hasHitPlayer && edgeBuffer.Contains(playerCell))
                {
                    hasHitPlayer = monster.DealDamageToPlayerAtCell(playerCell, damage, hitDirWorld, true);
                    break;
                }

                // 예고 동안 상자 같은 고정 장애물이 새로 생겼으면 그 앞을 벽으로 본다
                if (IsAnyObstacle(monster, edgeBuffer))
                {
                    willHitWall = true;
                    break;
                }

                if (monster.TryMoveBody(stepAnchor))
                {
                    Vector3 worldTarget = monster.GetBodyCenterWorld(stepAnchor);
                    yield return monster.StartActionCoroutine(monster.MoveToTileRoutine(worldTarget, stepMoveDuration));
                }
                else
                {
                    stoppedByEntity = true;
                    break;
                }
            }

            // 3. 플레이어를 맞추지 못하고 벽에 들이박았을 때 -> 벽 충돌 기절(Stun) 발동!
            if (!hasHitPlayer && !stoppedByEntity && willHitWall && wallStunDuration > 0f)
            {
                GlobalEvents.OnCombatImpact?.Invoke(0.2f, 0.16f, 0.02f);
                monster.ApplyStun(wallStunDuration);
            }
        }

        private static bool IsAnyObstacle(MonsterController monster, List<Vector3Int> cells)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (Pathfinder.IsObstacle(monster.Grid, new Vector2Int(cells[i].x, cells[i].y), monster.ObstacleLayerMask)) return true;
            }
            return false;
        }

        // anchor 몸에서 dir 쪽 가장자리 N칸(한 칸 전진할 때 새로 들어가는 칸)
        private static void AddLeadingEdge(Vector3Int anchor, int size, Vector3Int dir, List<Vector3Int> result)
        {
            for (int k = 0; k < size; k++)
            {
                if (dir.x > 0) result.Add(new Vector3Int(anchor.x + size - 1, anchor.y + k, 0));
                else if (dir.x < 0) result.Add(new Vector3Int(anchor.x, anchor.y + k, 0));
                else if (dir.y > 0) result.Add(new Vector3Int(anchor.x + k, anchor.y + size - 1, 0));
                else result.Add(new Vector3Int(anchor.x + k, anchor.y, 0));
            }
        }
    }
}
