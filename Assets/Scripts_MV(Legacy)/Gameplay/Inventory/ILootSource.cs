using System.Collections.Generic;
using UnityEngine;
using DimensionLooter.Data;

namespace DimensionLooter.Gameplay.Inventory
{
    public interface ILootSource
    {
        string Id { get; }
        IReadOnlyList<ItemInstance> StoredItems { get; }
        void AppendItems(List<ItemInstance> items);
        bool RemoveItem(ItemInstance item);
        bool DestroyWhenEmpty { get; }
        GameObject gameObject { get; }
    }
}
