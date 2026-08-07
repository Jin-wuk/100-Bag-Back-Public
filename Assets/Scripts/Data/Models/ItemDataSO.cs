using UnityEngine;
using DimensionLooter.Data.Core;

namespace DimensionLooter.Data.Models
{
    [CreateAssetMenu(fileName = "NewItemData", menuName = "DimensionLooter/Data/ItemData")]
    public class ItemDataSO : ScriptableObject
    {
        public int itemID;
        public string itemName;
        public ItemCategory category;
        public EquipType equipType;
        public int gridWidth;
        public int gridHeight;
        [Header("For Backpack Only")]
        public int bagHeight;
        public int baseValue;
        public ItemRarity rarity;
    }
}
