using System.Collections;
using UnityEngine;

namespace BagBack100.Monster
{
    /// <summary>
    /// 플레이어가 위치한 인접 1칸 타일을 타격하는 단일 근접 공격 패턴 레고 블록.
    /// telegraphDuration을 길게(0.8초) 설정하면 '묵직한 단일 강타', 짧게(0.25초) 설정하면 '날렵한 기습 찌르기'로 조립 가능합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "Pattern_SingleMelee", menuName = "BagBack100/Monster/Patterns/Single Melee")]
    public class SingleMeleePatternSO : MonsterPatternSO
    {
        [Header("단일 근접 설정")]
        [Tooltip("체크 시 강한 타격 피드백(카메라 쉐이크 및 큰 경직 연출) 부여")]
        public bool isHeavyHit = false;

        public override IEnumerator ExecuteRoutine(MonsterController monster)
        {
            if (monster == null || monster.Grid == null || monster.Player == null)
            {
                yield break;
            }

            Vector3Int targetCell = monster.GetPlayerCell();
            Vector3 cellCenterWorld = monster.Grid.GetCellCenterWorld(targetCell);
            float telegraph = GetEffectiveTelegraphDuration(monster);

            monster.SpawnWarning(cellCenterWorld, telegraph);

            yield return new WaitForSeconds(telegraph);

            Vector3 hitDir = (cellCenterWorld - monster.transform.position).normalized;
            int damage = CalculateDamage(monster);
            monster.DealDamageToPlayerAtCell(targetCell, damage, hitDir, isHeavyHit);
        }
    }
}
