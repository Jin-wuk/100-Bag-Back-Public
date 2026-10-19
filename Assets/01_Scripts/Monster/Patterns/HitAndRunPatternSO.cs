using System.Collections;
using UnityEngine;
using BagBack100.Core;

namespace BagBack100.Monster
{
    /// <summary>
    /// 날렵한 근거리 몬스터 전용: 짧은 전조 후 1칸 찌르기를 가한 뒤, 즉시 후방(또는 측면)으로 백스텝하여 빠지는 히트앤런 패턴 레고 블록.
    /// </summary>
    [CreateAssetMenu(fileName = "Pattern_HitAndRun", menuName = "BagBack100/Monster/Patterns/Hit And Run")]
    public class HitAndRunPatternSO : MonsterPatternSO
    {
        [Header("치고 빠지기(백스텝) 설정")]
        [Tooltip("타격 직후 뒤로 물러날 칸 수")]
        [Range(1, 3)]
        public int retreatSteps = 2;
        [Tooltip("백스텝 1칸당 이동 소요 시간(초)")]
        [Min(0.05f)]
        public float backstepDuration = 0.12f;

        public override IEnumerator ExecuteRoutine(MonsterController monster)
        {
            if (monster == null || monster.Grid == null || monster.Player == null || TileSystem.Instance == null)
            {
                yield break;
            }

            // 1. 기습 타격 전조
            Vector3Int targetCell = monster.GetPlayerCell();
            Vector3 cellCenterWorld = monster.Grid.GetCellCenterWorld(targetCell);
            float telegraph = GetEffectiveTelegraphDuration(monster);

            monster.SpawnWarning(cellCenterWorld, telegraph);
            yield return new WaitForSeconds(telegraph);

            // 2. 타격 판정
            Vector3 hitDir = (cellCenterWorld - monster.transform.position).normalized;
            int damage = CalculateDamage(monster);
            monster.DealDamageToPlayerAtCell(targetCell, damage, hitDir, false);

            // 3. 즉각 후방 백스텝 이탈 (뒤가 벽으로 막혀 있으면 좌/우 측면으로 이탈)
            Vector3 awayFromPlayer = (monster.transform.position - monster.Player.transform.position).normalized;
            Vector3Int primaryRetreatDir = MonsterLocomotion.SnapTo4WayCell(awayFromPlayer);
            Vector3Int sideDirA = new Vector3Int(-primaryRetreatDir.y, primaryRetreatDir.x, 0);
            Vector3Int sideDirB = new Vector3Int(primaryRetreatDir.y, -primaryRetreatDir.x, 0);

            for (int step = 0; step < retreatSteps; step++)
            {
                Vector3Int currentCell = monster.GetCurrentCell();
                Vector3Int nextCell = currentCell;

                if (MonsterLocomotion.CanStepToCell(monster, currentCell + primaryRetreatDir))
                {
                    nextCell = currentCell + primaryRetreatDir;
                }
                else if (MonsterLocomotion.CanStepToCell(monster, currentCell + sideDirA))
                {
                    nextCell = currentCell + sideDirA;
                }
                else if (MonsterLocomotion.CanStepToCell(monster, currentCell + sideDirB))
                {
                    nextCell = currentCell + sideDirB;
                }

                if (nextCell != currentCell && TileSystem.Instance.TryOccupyTile(nextCell, monster.gameObject, currentCell))
                {
                    Vector3 worldTarget = monster.Grid.GetCellCenterWorld(nextCell);
                    yield return monster.StartActionCoroutine(monster.MoveToTileRoutine(worldTarget, backstepDuration, 0.35f));
                }
                else
                {
                    break;
                }
            }
        }
    }
}
