// File: Assets/Scripts/Gameplay/Inventory/PouchInteractable.cs
using UnityEngine;
using DimensionLooter.Core;
using DimensionLooter.Data;
using DimensionLooter.UI.Inventory;
using System.Collections.Generic;

namespace DimensionLooter.Gameplay.Inventory
{
    public class PouchInteractable : MonoBehaviour, IInteractable, ILootSource
    {
        public string Id { get; private set; }
        private List<ItemInstance> _storedItems = new List<ItemInstance>();
        public IReadOnlyList<ItemInstance> StoredItems => _storedItems;
        public bool DestroyWhenEmpty => true;

        public void Init(string id, List<ItemInstance> items)
        {
            Id = id;
            _storedItems = new List<ItemInstance>(items);
        }

        public void AppendItems(List<ItemInstance> items)
        {
            if (items != null)
            {
                _storedItems.AddRange(items);
            }
        }

        public bool RemoveItem(ItemInstance item)
        {
            return _storedItems.Remove(item);
        }

        public void Interact(GameObject interactor)
        {
            if (_storedItems.Count > 0)
            {
                Debug.Log($"[Pouch] 바닥에 떨어진 파우치({Id})를 엽니다!");
                if (DualUIPanel.Instance != null)
                {
                    DualUIPanel.Instance.Show(this);
                }
            }
        }
    }
}
