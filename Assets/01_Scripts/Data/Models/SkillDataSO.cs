using UnityEngine;
using BagBack100.Data.Core;

namespace BagBack100.Data.Models
{
    [CreateAssetMenu(fileName = "NewSkillData", menuName = "BagBack100/Data/SkillData")]
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
