using UnityEngine;

namespace BagBack100.Core
{
    /// <summary>
    /// Role: 전투 수치 공식(방어력 경감) 모음. 상태 없는 순수 함수만 둔다 (Docs/GDD/Roadmap.md 1절 → 구현 후 Combat.md로 이관).
    /// Dependencies: 없음
    /// Events: 없음
    /// </summary>
    public static class CombatMath
    {
        // 방어력 디버프 누적으로 인한 피해 증폭 상한 (기획: 최대 2.0배)
        public const float MaxNegativeDefAmplify = 2.0f;
        private const float DefenseConstant = 100f;

        /// <summary>
        /// 방어력으로 경감한 최종 피해. DEF ≥ 0이면 DEF/(100+DEF)만큼 감소(점감, 100% 무적 불가),
        /// DEF &lt; 0이면 2 - 100/(100-DEF)배로 증폭(상한 2.0배). 관통력은 양수 방어력에만 적용한다.
        /// </summary>
        /// <returns>최종 피해. 원본 피해가 0 이하면 0, 그 외에는 최소 1</returns>
        public static int Mitigate(int rawDamage, int defense, int armorPen = 0)
        {
            if (rawDamage <= 0) return 0;

            // 음수 방어력에 관통력을 적용하면 Max(0, ...)에 잘려 증폭 공식이 무력화되므로 양수일 때만 적용
            int effectiveDef = defense < 0 ? defense : Mathf.Max(0, defense - armorPen);

            float multiplier;
            if (effectiveDef >= 0)
            {
                multiplier = 1f - effectiveDef / (DefenseConstant + effectiveDef);
            }
            else
            {
                multiplier = Mathf.Min(MaxNegativeDefAmplify, 2f - DefenseConstant / (DefenseConstant - effectiveDef));
            }

            return Mathf.Max(1, Mathf.RoundToInt(rawDamage * multiplier));
        }
    }
}
