// File: Assets/Scripts/Managers/InventoryManager.cs
using UnityEngine;
using DimensionLooter.Gameplay.Inventory;

namespace DimensionLooter.Managers
{
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [Header("Main Inventory Settings")]
        [SerializeField] private int _width = 6;
        [SerializeField] private int _height = 6;

        public InventoryGrid MainGrid { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                MainGrid = new InventoryGrid(_width, _height);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
