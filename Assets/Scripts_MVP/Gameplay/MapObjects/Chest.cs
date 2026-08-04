using System.Collections.Generic;
using UnityEngine;
using DimensionLooter.Core;
using DimensionLooter.Data;
using DimensionLooter.Managers;

namespace DimensionLooter.Gameplay.MapObjects
{
    public enum ChestRarity
    {
        Normal,
        Rare,
        Legendary
    }

    /// <summary>
    /// 플레이어가 상호작용하여 아이템을 얻을 수 있는 보물상자입니다.
    /// 열쇠를 소모할 수 있습니다.
    /// </summary>
    public class Chest : MonoBehaviour, IInteractable, DimensionLooter.Gameplay.Inventory.ILootSource
    {
        [Header("Chest Settings")]
        public ChestRarity rarity = ChestRarity.Normal;
        
        [Tooltip("열쇠가 필요한 상자인가?")]
        public bool requiresKey = false;
        
        [Tooltip("필요한 열쇠의 아이템 ID (예: ITEM_KEY_001)")]
        public string requiredKeyId = "ITEM_KEY_001";

        private bool _isOpened = false;

        public string Id { get; private set; }
        private List<ItemInstance> _storedItems = new List<ItemInstance>();
        public IReadOnlyList<ItemInstance> StoredItems => _storedItems;
        public bool DestroyWhenEmpty => false;

        public void AppendItems(List<ItemInstance> items) { if (items != null) _storedItems.AddRange(items); }
        public bool RemoveItem(ItemInstance item) { return _storedItems.Remove(item); }

        private void Awake()
        {
            Id = "Chest_" + System.Guid.NewGuid().ToString().Substring(0, 8);
        }

        public void Interact(GameObject interactor)
        {
            if (_isOpened)
            {
                if (DimensionLooter.UI.Inventory.DualUIPanel.Instance != null)
                {
                    DimensionLooter.UI.Inventory.DualUIPanel.Instance.Show(this);
                }
                return;
            }

            if (requiresKey)
            {
                if (!ConsumeKey(requiredKeyId))
                {
                    Debug.Log($"[Chest] 열쇠({requiredKeyId})가 부족하여 상자를 열 수 없습니다.");
                    // TODO: 필요하다면 인게임 플로팅 텍스트나 UI 알림 띄우기
                    return;
                }
                Debug.Log($"[Chest] 열쇠({requiredKeyId})를 소모했습니다.");
            }

            OpenChest();
        }

        private bool ConsumeKey(string keyId)
        {
            if (InventoryManager.Instance == null) return false;

            // MainGrid에서 키 찾기
            var mainGrid = InventoryManager.Instance.MainGrid;
            foreach (var placedItem in mainGrid.GetAllItems())
            {
                if (placedItem.Instance.Data.Id == keyId)
                {
                    mainGrid.RemoveItem(placedItem);
                    return true;
                }
            }

            // SecureGrid에서 키 찾기
            var secureGrid = InventoryManager.Instance.SecureGrid;
            foreach (var placedItem in secureGrid.GetAllItems())
            {
                if (placedItem.Instance.Data.Id == keyId)
                {
                    secureGrid.RemoveItem(placedItem);
                    return true;
                }
            }

            return false;
        }

        private void OpenChest()
        {
            _isOpened = true;
            Debug.Log($"[Chest] {rarity} 등급 보물상자를 열었습니다!");

            // 등급별 드롭 확률 및 갯수 적용 (MVP: 임시 확률 적용)
            int dropCount = rarity == ChestRarity.Legendary ? 3 : (rarity == ChestRarity.Rare ? 2 : 1);
            
            List<ItemInstance> dropList = new List<ItemInstance>();
            if (DataManager.Instance != null && DataManager.Instance.ItemDict.Count > 0)
            {
                List<ItemData> allItems = new List<ItemData>(DataManager.Instance.ItemDict.Values);
                for (int i = 0; i < dropCount; i++)
                {
                    dropList.Add(new ItemInstance(allItems[Random.Range(0, allItems.Count)]));
                }
            }

            if (dropList.Count > 0)
            {
                _storedItems.AddRange(dropList);
            }

            if (DimensionLooter.UI.Inventory.DualUIPanel.Instance != null)
            {
                DimensionLooter.UI.Inventory.DualUIPanel.Instance.Show(this);
            }

            // TODO: SpriteRenderer의 sprite를 '열린 상자' 이미지로 교체하는 로직 추가
        }
    }
}
