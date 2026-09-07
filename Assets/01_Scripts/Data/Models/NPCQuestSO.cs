using UnityEngine;

namespace DimensionLooter.Data.Models
{
    [CreateAssetMenu(fileName = "NewNPCQuest", menuName = "DimensionLooter/Data/NPCQuest")]
    public class NPCQuestSO : ScriptableObject
    {
        public int questID;
        public string npcID;
        public int reqItemID;
        public int reqAmount;
        public int rewardAffinity;
    }
}
