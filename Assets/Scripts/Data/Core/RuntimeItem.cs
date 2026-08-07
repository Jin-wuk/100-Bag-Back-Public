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
        public int GridX { get; set; }
        public int GridY { get; set; }
        public bool IsRotated { get; set; }

        public RuntimeItem(ItemDataSO itemData)
        {
            ItemData = itemData;
            InstanceID = Guid.NewGuid().ToString();
            GridX = -1;
            GridY = -1;
            IsRotated = false;
        }

        public int GetCurrentWidth()
        {
            return IsRotated ? ItemData.gridHeight : ItemData.gridWidth;
        }

        public int GetCurrentHeight()
        {
            return IsRotated ? ItemData.gridWidth : ItemData.gridHeight;
        }
    }
}
