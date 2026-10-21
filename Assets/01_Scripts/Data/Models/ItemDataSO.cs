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
        public float weight = 1f;
        public int maxStack = 1;

        public int baseValue;
        public ItemRarity rarity;
    }
}
