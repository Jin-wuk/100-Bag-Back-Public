using UnityEngine;
using DimensionLooter.Data.Core;

namespace DimensionLooter.Data.Models
{
    [CreateAssetMenu(fileName = "NewSkillData", menuName = "DimensionLooter/Data/SkillData")]
    public class SkillDataSO : ScriptableObject
    {
        public string skillID;
        public string skillName;
        public SkillActivationType activationType;
        public SkillRoleType roleType;
        public int mpCost;
        public float cooldown;
        public float effectValue;
        public float duration;
    }
}
