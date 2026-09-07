using UnityEngine;
using DimensionLooter.Data.Core;

namespace DimensionLooter.Data.Models
{
    [CreateAssetMenu(fileName = "NewItemData", menuName = "DimensionLooter/Data/ItemData")]
    public class ItemDataSO : ScriptableObject
    {
        public int itemID;
        public string itemName;
        public Sprite itemIcon;
        public ItemCategory category;
        public EquipType equipType;
        public int gridWidth;
        public int gridHeight;
        [Header("For Backpack Only")]
        public int bagHeight;
        public int baseValue;
        public ItemRarity rarity;

        [Header("Equipment Stats")]
        public int hpBonus;
        public int mpBonus;
        public int attackBonus;
        public int defenseBonus;
        public float speedBonus;
        public int rangeBonus;
    }
}
