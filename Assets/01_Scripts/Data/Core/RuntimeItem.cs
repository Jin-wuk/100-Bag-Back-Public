using System;
using DimensionLooter.Data.Models;
using UnityEngine;

namespace DimensionLooter.Data.Core
{
    [Serializable]
    public class RuntimeItem
    {
        public ItemDataSO ItemData { get; private set; }
        public string InstanceID { get; private set; }
        public int SlotIndex { get; set; }
        public int Count { get; set; }

        public RuntimeItem(ItemDataSO itemData)
        {
            ItemData = itemData;
            InstanceID = Guid.NewGuid().ToString();
            SlotIndex = -1;
            Count = 1;
        }
    }
}
