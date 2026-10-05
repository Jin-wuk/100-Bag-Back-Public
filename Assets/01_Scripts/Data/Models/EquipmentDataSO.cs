using UnityEngine;
using BagBack100.Data.Core;

namespace BagBack100.Data.Models
{
    public class EquipmentDataSO : ItemDataSO
    {
        public EquipType equipType;
        
        [Header("Equipment Stats")]
        public int hpBonus;
        public int mpBonus;
        public int attackBonus;
        public int defenseBonus;
        public float speedBonus;
        public int rangeBonus;
    }
}
