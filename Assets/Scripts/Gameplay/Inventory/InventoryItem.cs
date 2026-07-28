// File: Assets/Scripts/Gameplay/Inventory/InventoryItem.cs
using DimensionLooter.Data;

namespace DimensionLooter.Gameplay.Inventory
{
    public class InventoryItem
    {
        public ItemData Data { get; private set; }
        public int GridX { get; set; }
        public int GridY { get; set; }
        public bool IsRotated { get; set; }

        public InventoryItem(ItemData data)
        {
            Data = data;
        }

        public int GetWidth() => IsRotated ? Data.Height : Data.Width;
        public int GetHeight() => IsRotated ? Data.Width : Data.Height;
    }
}
