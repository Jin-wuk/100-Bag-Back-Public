using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BagBack100.Core;

namespace BagBack100.Monster
{
    public enum AoESlamShape
    {
        SquareAroundMonster, // 몬스터 주변 (2*radius+1)x(2*radius+1) 정사각형 (예: radius=1 이면 3x3)
        ExpandingDiamond,    // 몬스터 중심에서 마름모 형태로 바깥으로 퍼져나가는 파동 충격파
        ForwardBox           // 플레이어 방향 전방 박스형 (예: 전방 2x3 타일)
    }

    /// <summary>
    /// 바닥에 다중 타일 경고 장판(Telegraph)을 깔고 일정 시간 뒤 광역 즉발 피해를 주는 내려치기 패턴 레고 블록 (Stateless Flyweight).
    /// </summary>
    [CreateAssetMenu(fileName = "Pattern_AoESlam", menuName = "BagBack100/Monster/Patterns/AoE Slam")]
    public class AoESlamPatternSO : MonsterPatternSO
    {
        [Header("광역 장판 형태 설정")]
        public AoESlamShape shape = AoESlamShape.SquareAroundMonster;
        [Tooltip("장판 반경 (SquareAroundMonster=1이면 3x3, ExpandingDiamond=2이면 반경 2칸 마름모)")]
        [Range(1, 4)]
        public int radius = 1;

        public override IEnumerator ExecuteRoutine(MonsterController monster)
        {
            if (monster == null || monster.Grid == null || monster.Player == null)
            {
                yield break;
            }

            // 범위는 몸 사각형 기준으로 잡는다(1칸 몸이면 기존과 같음)
            Vector3Int anchor = monster.GetCurrentCell();
            int size = monster.Footprint;
            float totalTelegraph = GetEffectiveTelegraphDuration(monster);
            // 퍼지는 마름모는 장판을 다 깐 뒤 0.15초 더 기다렸다 판정하므로 타격 모션도 그만큼 늦춘다
            monster.NotifyAttackWindup(shape == AoESlamShape.ExpandingDiamond ? totalTelegraph + 0.15f : totalTelegraph);
            List<Vector3Int> targetCellsBuffer = monster.PatternCellsBuffer;
            targetCellsBuffer.Clear();

            if (shape == AoESlamShape.ExpandingDiamond)
            {
                float stepTime = totalTelegraph / (radius + 1);
                for (int r = 0; r <= radius; r++)
                {
                    for (int x = anchor.x - r; x <= anchor.x + size - 1 + r; x++)
                    {
                        for (int y = anchor.y - r; y <= anchor.y + size - 1 + r; y++)
                        {
                            Vector3Int cell = new Vector3Int(x, y, 0);
                            // 여러 칸 몸은 몸 안(r = 0)에 장판을 깔지 않는다
                            if (MonsterController.GetBodyManhattanDistance(anchor, size, cell) == r && !(size > 1 && r == 0))
                            {
                                if (Pathfinder.IsObstacle(monster.Grid, new Vector2Int(cell.x, cell.y), monster.ObstacleLayerMask))
                                {
                                    continue;
                                }
                                targetCellsBuffer.Add(cell);
                                float remainingTime = totalTelegraph - (stepTime * r) + 0.15f;
                                monster.SpawnWarning(monster.Grid.GetCellCenterWorld(cell), remainingTime);
                            }
                        }
                    }
                    yield return new WaitForSeconds(stepTime);
                }
                yield return new WaitForSeconds(0.15f);
            }
            else if (shape == AoESlamShape.ForwardBox)
            {
                Vector3 toPlayer = (monster.Player.transform.position - monster.transform.position).normalized;
                Vector3Int forward = MonsterLocomotion.SnapTo4WayCell(toPlayer);

                // 몸 앞쪽 가장자리에서 depth칸, 폭은 몸 폭 + 양옆 1칸
                for (int depth = 1; depth <= radius + 1; depth++)
                {
                    for (int k = -1; k <= size; k++)
                    {
                        Vector3Int cell = GetForwardCell(anchor, size, forward, depth, k);
                        if (Pathfinder.IsObstacle(monster.Grid, new Vector2Int(cell.x, cell.y), monster.ObstacleLayerMask))
                        {
                            continue;
                        }
                        targetCellsBuffer.Add(cell);
                        monster.SpawnWarning(monster.Grid.GetCellCenterWorld(cell), totalTelegraph);
                    }
                }
                yield return new WaitForSeconds(totalTelegraph);
            }
            else
            {
                // SquareAroundMonster (기본 3x3 주변 슬램)
                for (int x = anchor.x - radius; x <= anchor.x + size - 1 + radius; x++)
                {
                    for (int y = anchor.y - radius; y <= anchor.y + size - 1 + radius; y++)
                    {
                        Vector3Int cell = new Vector3Int(x, y, 0);
                        if (size > 1 && MonsterController.GetBodyChebyshevDistance(anchor, size, cell) == 0) continue;
                        if (Pathfinder.IsObstacle(monster.Grid, new Vector2Int(cell.x, cell.y), monster.ObstacleLayerMask))
                        {
                            continue;
                        }
                        targetCellsBuffer.Add(cell);
                        monster.SpawnWarning(monster.Grid.GetCellCenterWorld(cell), totalTelegraph);
                    }
                }
                yield return new WaitForSeconds(totalTelegraph);
            }

            Vector3 hitDir = (monster.Player.transform.position - monster.transform.position).normalized;
            int damage = CalculateDamage(monster);
            monster.DealDamageToPlayerIfInCells(targetCellsBuffer, damage, hitDir, true);
        }

        private static Vector3Int GetForwardCell(Vector3Int anchor, int size, Vector3Int forward, int depth, int k)
        {
            if (forward.x > 0) return new Vector3Int(anchor.x + size - 1 + depth, anchor.y + k, 0);
            if (forward.x < 0) return new Vector3Int(anchor.x - depth, anchor.y + k, 0);
            if (forward.y > 0) return new Vector3Int(anchor.x + k, anchor.y + size - 1 + depth, 0);
            return new Vector3Int(anchor.x + k, anchor.y - depth, 0);
        }
    }
}
