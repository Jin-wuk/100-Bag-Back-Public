using UnityEngine;
using System.Collections.Generic;
using BagBack100.Data.Core;
using BagBack100.Monster;

namespace BagBack100.Data.Models
{
    [CreateAssetMenu(fileName = "NewMonsterData", menuName = "BagBack100/Data/MonsterData")]
    public class MonsterDataSO : ScriptableObject
    {
        [Header("기본 정보 및 전투 스탯")]
        public int monsterID;
        public string monsterName;
        public MonsterType type;
        public int hp = 50;
        public int atk = 10;
        public int def = 5;
        public float speed = 5f;
        [Tooltip("원거리 몬스터의 경우 유지하려는 적정 카이팅 거리(칸), 근거리의 경우 기본 사거리")]
        public int range = 1;
        [Tooltip("플레이어를 감지하고 추적/교전을 시작하는 반경(칸)")]
        public int zocRadius = 6;

        [Header("1. 이동 템포 및 스타일 (Locomotion)")]
        [Tooltip("HeavyChase: 직진 추적 / AgileFlank: 측면·후방 우회 / RangedKiting: 거리 유지 카이팅")]
        public MonsterMovementStyle movementStyle = MonsterMovementStyle.HeavyChase;
        [Tooltip("타일 1칸을 이동하는 데 걸리는 시간(초) - 작을수록 이동 모션이 빠름")]
        [Min(0.05f)]
        public float moveDuration = 0.3f;
        [Tooltip("타일 1칸 이동 완료 후 다음 칸 이동까지의 대기 시간(초)")]
        [Min(0.02f)]
        public float moveInterval = 0.25f;

        [Header("2. 다음 행동 패턴까지의 시간 (Action Rhythm)")]
        [Tooltip("패턴 종료 후 다음 공격 패턴을 발동하기까지의 글로벌 쿨타임(초). 이 시간 동안에도 몬스터는 이동할 수 있음.")]
        [Min(0.1f)]
        public float globalPatternCooldown = 2.0f;
        [Tooltip("패턴 리스트가 비어 있을 때 사용하는 기본 전조 시간(초)")]
        public float telegraphTime = 0.8f;

        [Header("3. 반응형 회피/인터럽트 쿨타임 (억까 방지용)")]
        [Tooltip("날렵한 몬스터(AgileFlank): 플레이어 공격 감지 시 90도 측면 스텝 회피 쿨타임(초). 0 이하면 비활성화")]
        public float evasiveStepCooldown = 4.0f;
        [Tooltip("원거리 몬스터(RangedKiting): 피격 시 후방 이탈(텔레포트/백대시) 쿨타임(초). 0 이하면 비활성화")]
        public float escapeOnHitCooldown = 6.0f;

        [Header("4. 조립식 행동 패턴 리스트 (가중치 랜덤 추첨)")]
        [Tooltip("몬스터가 구사할 패턴 SO 목록. 사거리 조건 만족 시 각 패턴의 weight 비율로 가중치 랜덤 채택됨.")]
        public List<MonsterPatternSO> patterns = new List<MonsterPatternSO>();

        [Header("특성 및 드랍 보상 (Traits & Drops)")]
        public List<string> traits = new List<string>();
        public int dropPoolID;
        public int goldDropBase;
        //public AttackPatternType attackPattern = AttackPatternType.SingleTile;

        public bool HasTrait(string traitName)
        {
            if (traits == null || string.IsNullOrEmpty(traitName)) return false;
            for (int i = 0; i < traits.Count; i++)
            {
                if (string.Equals(traits[i], traitName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public float GetEffectiveMoveDuration()
        {
            float mult = HasTrait("FAST") ? 0.7f : 1f;
            return Mathf.Max(0.08f, moveDuration * mult);
        }

        public float GetEffectiveMoveInterval()
        {
            float mult = HasTrait("FAST") ? 0.7f : 1f;
            return Mathf.Max(0.04f, moveInterval * mult);
        }

        public float GetEffectiveTelegraphMultiplier()
        {
            return HasTrait("FAST") ? 0.7f : 1f;
        }
    }
}
