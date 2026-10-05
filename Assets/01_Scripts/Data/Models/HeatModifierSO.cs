using UnityEngine;

namespace BagBack100.Data.Models
{
    [CreateAssetMenu(fileName = "NewHeatModifier", menuName = "BagBack100/Data/HeatModifier")]
    public class HeatModifierSO : ScriptableObject
    {
        public string modifierID;
        public string modifierName;
        public int heatScore;
        public string targetStat;
        public float value;
    }
}
