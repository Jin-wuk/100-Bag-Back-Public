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

            Vector3Int centerCell = monster.GetCurrentCell();
            float totalTelegraph = GetEffectiveTelegraphDuration(monster);
            List<Vector3Int> targetCellsBuffer = monster.PatternCellsBuffer;
            targetCellsBuffer.Clear();

            if (shape == AoESlamShape.ExpandingDiamond)
            {
                float stepTime = totalTelegraph / (radius + 1);
                for (int r = 0; r <= radius; r++)
                {
                    for (int x = -r; x <= r; x++)
                    {
                        for (int y = -r; y <= r; y++)
                        {
                            if (Mathf.Abs(x) + Mathf.Abs(y) == r)
                            {
                                Vector3Int cell = centerCell + new Vector3Int(x, y, 0);
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
                Vector3Int perp = new Vector3Int(-forward.y, forward.x, 0);

                for (int depth = 1; depth <= radius + 1; depth++)
                {
                    for (int width = -1; width <= 1; width++)
                    {
                        Vector3Int cell = centerCell + (forward * depth) + (perp * width);
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
                for (int x = -radius; x <= radius; x++)
                {
                    for (int y = -radius; y <= radius; y++)
                    {
                        Vector3Int cell = centerCell + new Vector3Int(x, y, 0);
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
    }
}
