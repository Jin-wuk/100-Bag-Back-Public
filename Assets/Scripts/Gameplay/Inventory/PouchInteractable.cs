// File: Assets/Scripts/Gameplay/Inventory/PouchInteractable.cs
using UnityEngine;
using DimensionLooter.Core;
using DimensionLooter.Data;
using DimensionLooter.UI.Inventory;
using System.Collections.Generic;

namespace DimensionLooter.Gameplay.Inventory
{
    public class PouchInteractable : MonoBehaviour, IInteractable
    {
        private List<ItemData> _storedItems = new List<ItemData>();
        public IReadOnlyList<ItemData> StoredItems => _storedItems;

        public void Init(List<ItemData> items)
        {
            _storedItems = new List<ItemData>(items);
        }

        public void AppendItems(List<ItemData> items)
        {
            if (items != null)
            {
                _storedItems.AddRange(items);
            }
        }

        public void Interact(GameObject interactor)
        {
            if (_storedItems.Count > 0)
            {
                Debug.Log("[Pouch] 바닥에 떨어진 파우치를 엽니다!");
                if (DualUIPanel.Instance != null)
                {
                    DualUIPanel.Instance.Show(_storedItems);
                }
                
                // 상호작용 후 1회성으로 자신은 파괴. 
                // 다시 닫을 때 남은 템이 있으면 CombatManager/MapManager가 새로 스폰할 것임.
                Destroy(gameObject);
            }
        }
    }
}
