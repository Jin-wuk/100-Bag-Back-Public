using System.Collections;
using UnityEngine;

namespace BagBack100.Monster
{
    /// <summary>
    /// Role: 모든 몬스터 공격/스킬 패턴의 기반이 되는 추상 ScriptableObject (조립식 레고 블록).
    ///       MonsterDataSO의 patterns 리스트에 조합하여 코드 수정 없이 다양한 몬스터를 생성합니다.
    /// Dependencies: MonsterController (층 배율이 적용된 Attack)
    /// Events: 없음
    /// </summary>
    public abstract class MonsterPatternSO : ScriptableObject
    {
        [Header("패턴 기본 정보 & 가중치")]
        public string patternName = "New Pattern";
        [Tooltip("가중치 랜덤 추첨 시 사용할 가중치 값 (우선순위 고정이 아닌 확률 비례 추첨)")]
        [Min(0.1f)]
        public float weight = 10f;

        [Header("발동 거리 조건 (타일 칸 수 기준)")]
        [Tooltip("패턴 발동 가능한 최소 그리드 거리 (체비쇼프/타일 거리)")]
        [Min(0)]
        public int minRange = 1;
        [Tooltip("패턴 발동 가능한 최대 그리드 거리")]
        [Min(1)]
        public int maxRange = 1;
        [Tooltip("체크 시 거리를 맨해튼(상하좌우)으로 잰다. 근접 패턴이 대각선이나 2칸 밖에서 시작하지 않게 한다 (09 기획안)")]
        public bool orthogonalOnly = false;

        [Header("타이밍 및 딜타임 (공정성 제어)")]
        [Tooltip("이 패턴 자체의 개별 재사용 대기시간(초)")]
        [Min(0.1f)]
        public float patternCooldown = 3.0f;
        [Tooltip("공격 전 바닥 장판 또는 조준선으로 예고하는 시간(초)")]
        [Min(0.1f)]
        public float telegraphDuration = 0.7f;
        [Tooltip("공격 직후 무기를 거두며 멈춰 있는 후딜레이/빈틈 시간(초) - 플레이어의 확정 딜타임")]
        [Min(0f)]
        public float recoveryDuration = 0.5f;

        [Header("위력 설정")]
        [Tooltip("몬스터 기본 공격력(atk)에 곱해지는 데미지 배율")]
        [Min(0.1f)]
        public float damageMultiplier = 1.0f;

        /// <summary>
        /// 현재 몬스터와 플레이어의 상태에서 이 패턴을 후보군에 올릴 수 있는지 검사합니다.
        /// </summary>
        public virtual bool CanExecute(MonsterController monster, int gridDistanceToPlayer)
        {
            if (monster == null || monster.Player == null || monster.Grid == null) return false;
            // 여러 칸 몸은 몸 가장자리부터 잰다(1칸이면 기존과 같음)
            int dist = orthogonalOnly
                ? monster.GetBodyManhattanDistance(monster.GetPlayerCell())
                : gridDistanceToPlayer;
            return dist >= minRange && dist <= maxRange;
        }

        /// <summary>
        /// 실제 패턴 실행 코루틴 (전조 -> 판정/발사). 후딜레이(recoveryDuration)는 컨트롤러에서 일괄 처리하거나 패턴 내에서 처리합니다.
        /// </summary>
        public abstract IEnumerator ExecuteRoutine(MonsterController monster);

        protected int CalculateDamage(MonsterController monster)
        {
            if (monster == null || monster.Data == null) return 1;
            return Mathf.Max(1, Mathf.RoundToInt(monster.Attack * damageMultiplier));
        }

        protected float GetEffectiveTelegraphDuration(MonsterController monster)
        {
            float mult = (monster != null && monster.Data != null) ? monster.Data.GetEffectiveTelegraphMultiplier() : 1f;
            return Mathf.Max(0.1f, telegraphDuration * mult);
        }
    }
}
