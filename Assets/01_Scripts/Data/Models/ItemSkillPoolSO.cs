using UnityEngine;
using System.Collections.Generic;

namespace BagBack100.Data.Models
{
    [System.Serializable]
    public struct SkillRollEntry
    {
        public string skillID;
        public int rollWeight;
    }

    [CreateAssetMenu(fileName = "NewItemSkillPool", menuName = "BagBack100/Data/ItemSkillPool")]
    public class ItemSkillPoolSO : ScriptableObject
    {
        public int itemID;
        public List<SkillRollEntry> possibleSkills = new List<SkillRollEntry>();
    }
}
