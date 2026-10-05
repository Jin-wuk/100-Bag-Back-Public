using UnityEngine;

namespace BagBack100.Data.Models
{
    [CreateAssetMenu(fileName = "NewNPCQuest", menuName = "BagBack100/Data/NPCQuest")]
    public class NPCQuestSO : ScriptableObject
    {
        public int questID;
        public string npcID;
        public int reqItemID;
        public int reqAmount;
        public int rewardAffinity;
    }
}
