using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BagBack100.Core;

namespace BagBack100.Monster
{
    /// <summary>
    /// 일직선 경로에 경고 장판을 깔고 돌진하는 패턴 레고 블록.
    /// 플레이어가 옆으로 피해서 몬스터가 벽(Obstacle)에 충돌하면 기절(Stun)하여 플레이어에게 프리딜 타임을 제공합니다.
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

            Vector3Int startCell = monster.GetCurrentCell();
            Vector3 toPlayer = (monster.Player.transform.position - monster.transform.position).normalized;
            Vector3Int chargeDir = MonsterLocomotion.SnapTo4WayCell(toPlayer);

            List<Vector3Int> chargePathCells = monster.PatternCellsBuffer;
            chargePathCells.Clear();
            float telegraph = GetEffectiveTelegraphDuration(monster);
            bool willHitWall = false;

            // 1. 돌진 궤적 계산 및 바닥 장판 예고
            for (int step = 1; step <= chargeDistance; step++)
            {
                Vector3Int nextCell = startCell + (chargeDir * step);
                if (Pathfinder.IsObstacle(monster.Grid, new Vector2Int(nextCell.x, nextCell.y), monster.ObstacleLayerMask))
                {
                    willHitWall = true;
                    break;
                }
                chargePathCells.Add(nextCell);
                monster.SpawnWarning(monster.Grid.GetCellCenterWorld(nextCell), telegraph);
            }

            yield return new WaitForSeconds(telegraph);

            // 2. 고속 직선 돌진 실행
            bool hasHitPlayer = false;
            int damage = CalculateDamage(monster);
            Vector3 hitDirWorld = new Vector3(chargeDir.x, chargeDir.y, 0f).normalized;

            for (int i = 0; i < chargePathCells.Count; i++)
            {
                Vector3Int currentCell = monster.GetCurrentCell();
                Vector3Int stepCell = chargePathCells[i];

                // 경로상의 플레이어 충돌 판정
                if (!hasHitPlayer && monster.GetPlayerCell() == stepCell)
                {
                    hasHitPlayer = monster.DealDamageToPlayerAtCell(stepCell, damage, hitDirWorld, true);
                    break; // 플레이어와 부딪히면 그 앞 칸에서 돌진 정지
                }

                if (TileSystem.Instance.TryOccupyTile(stepCell, monster.gameObject, currentCell))
                {
                    Vector3 worldTarget = monster.Grid.GetCellCenterWorld(stepCell);
                    yield return monster.StartCoroutine(monster.MoveToTileRoutine(worldTarget, stepMoveDuration, 0.1f));
                }
                else
                {
                    break;
                }
            }

            // 3. 플레이어를 맞추지 못하고 벽에 들이박았을 때 -> 벽 충돌 기절(Stun) 발동!
            if (!hasHitPlayer && willHitWall && wallStunDuration > 0f)
            {
                GlobalEvents.OnCombatImpact?.Invoke(0.2f, 0.16f, 0.02f);
                monster.ApplyStun(wallStunDuration);
            }
        }
    }
}
