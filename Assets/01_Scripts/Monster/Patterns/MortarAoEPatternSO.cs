using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BagBack100.Player;

namespace BagBack100.Monster
{
    /// <summary>
    /// 원거리 곡사/낙하 포격 패턴 레고 블록.
    /// 비행 궤적에는 장판을 깔지 않고, 탄환이 떨어질 '최종 착탄 타일(Landing Cell)'에만 경고 장판을 표시하여 이동 경로 제한(Area Denial)을 유도합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "Pattern_MortarAoE", menuName = "BagBack100/Monster/Patterns/Mortar AoE")]
    public class MortarAoEPatternSO : MonsterPatternSO
    {
        [Header("곡사 포격 설정")]
        [Tooltip("착탄 폭발 반경 (0이면 목표 타일 1칸, 1이면 십자 5칸 폭발)")]
        [Range(0, 2)]
        public int blastRadius = 0;
        [Tooltip("체크 시 플레이어가 이동 중인 방향 1칸 앞에 장판을 깔아 진행 경로를 차단합니다.")]
        public bool leadTargetTile = true;

        private readonly List<Vector3Int> _impactCellsBuffer = new List<Vector3Int>(13);

        public override IEnumerator ExecuteRoutine(MonsterController monster)
        {
            if (monster == null || monster.Grid == null || monster.Player == null)
            {
                yield break;
            }

            Vector3Int targetCenterCell = monster.GetPlayerCell();
            if (leadTargetTile)
            {
                PlayerMovement pm = monster.Player.GetComponent<PlayerMovement>();
                if (pm != null && pm.IsMoving)
                {
                    Vector3Int movingDestCell = monster.Grid.WorldToCell(pm.CurrentTargetTileWorldPos);
                    movingDestCell.z = 0;
                    targetCenterCell = movingDestCell;
                }
            }

            _impactCellsBuffer.Clear();
            float telegraph = GetEffectiveTelegraphDuration(monster);

            for (int x = -blastRadius; x <= blastRadius; x++)
            {
                for (int y = -blastRadius; y <= blastRadius; y++)
                {
                    if (Mathf.Abs(x) + Mathf.Abs(y) <= blastRadius)
                    {
                        Vector3Int cell = targetCenterCell + new Vector3Int(x, y, 0);
                        _impactCellsBuffer.Add(cell);
                        monster.SpawnWarning(monster.Grid.GetCellCenterWorld(cell), telegraph);
                    }
                }
            }

            yield return new WaitForSeconds(telegraph);

            Vector3 hitDir = (monster.Grid.GetCellCenterWorld(targetCenterCell) - monster.transform.position).normalized;
            int damage = CalculateDamage(monster);
            monster.DealDamageToPlayerIfInCells(_impactCellsBuffer, damage, hitDir, true);
        }
    }
}
