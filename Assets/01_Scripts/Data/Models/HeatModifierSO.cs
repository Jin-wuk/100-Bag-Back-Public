using UnityEngine;

namespace DimensionLooter.Data.Models
{
    [CreateAssetMenu(fileName = "NewHeatModifier", menuName = "DimensionLooter/Data/HeatModifier")]
    public class HeatModifierSO : ScriptableObject
    {
        public string modifierID;
        public string modifierName;
        public int heatScore;
        public string targetStat;
        public float value;
    }
}
