using UnityEngine;
using System.Collections.Generic;

namespace DimensionLooter.Data.Models
{
    [System.Serializable]
    public struct DropEntry
    {
        public int itemID;
        public int dropWeight;
    }

    [CreateAssetMenu(fileName = "NewDropPool", menuName = "DimensionLooter/Data/DropPool")]
    public class DropPoolSO : ScriptableObject
    {
        public int dropPoolID;
        public List<DropEntry> dropEntries = new List<DropEntry>();
    }
}
