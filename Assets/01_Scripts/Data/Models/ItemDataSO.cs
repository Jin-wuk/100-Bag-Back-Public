using UnityEngine;
using BagBack100.Data.Core;

namespace BagBack100.Data.Models
{
    [CreateAssetMenu(fileName = "NewItemData", menuName = "BagBack100/Data/ItemData")]
    public class ItemDataSO : ScriptableObject
    {
        public int itemID;
        public string itemName;
        public Sprite itemIcon;
        public ItemCategory category;
        public EquipType equipType;
        public float weight = 1f;
        public int maxStack = 1;

        [Header("For Backpack Only")]
        public int bagAddedSlots = 10;
        public float bagMaxWeight = 50f;

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
