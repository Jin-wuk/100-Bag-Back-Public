using UnityEngine;
using System.Collections.Generic;

namespace DimensionLooter.Data.Models
{
    [System.Serializable]
    public struct SkillRollEntry
    {
        public string skillID;
        public int rollWeight;
    }

    [CreateAssetMenu(fileName = "NewItemSkillPool", menuName = "DimensionLooter/Data/ItemSkillPool")]
    public class ItemSkillPoolSO : ScriptableObject
    {
        public int itemID;
        public List<SkillRollEntry> possibleSkills = new List<SkillRollEntry>();
    }
}
