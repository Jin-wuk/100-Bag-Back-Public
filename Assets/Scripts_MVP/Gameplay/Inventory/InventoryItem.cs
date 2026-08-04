// File: Assets/Scripts/Gameplay/Inventory/InventoryItem.cs
using DimensionLooter.Data;

namespace DimensionLooter.Gameplay.Inventory
{
    public class InventoryItem
    {
        public ItemInstance Instance { get; private set; }
        public ItemData Data => Instance?.Data;
        public int GridX { get; set; }
        public int GridY { get; set; }
        public bool IsRotated { get; set; }

        public InventoryItem(ItemInstance instance)
        {
            Instance = instance;
        }

        public InventoryItem(ItemData data)
        {
            Instance = new ItemInstance(data);
        }

        public int GetWidth() => IsRotated ? Data.Height : Data.Width;
        public int GetHeight() => IsRotated ? Data.Width : Data.Height;
    }
}
