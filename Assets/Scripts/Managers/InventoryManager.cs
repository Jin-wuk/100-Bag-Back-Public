// File: Assets/Scripts/Managers/InventoryManager.cs
using UnityEngine;
using DimensionLooter.Gameplay.Inventory;

namespace DimensionLooter.Managers
{
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        public event System.Action OnGridResized;

        [Header("Default Pockets Settings (No Bag)")]
        [SerializeField] private int _defaultPocketWidth = 2;
        [SerializeField] private int _defaultPocketHeight = 2;

        [Header("Secure Pouch Settings")]
        [SerializeField] private int _securePouchWidth = 2;
        [SerializeField] private int _securePouchHeight = 2;

        public InventoryGrid MainGrid { get; private set; }
        public InventoryGrid SecureGrid { get; private set; }
        
        // 현재 장착중인 가방 아이템 데이터
        public DimensionLooter.Data.ItemData EquippedBackpack { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // 맨몸 상태(주머니)로 초기화
                MainGrid = new InventoryGrid(_defaultPocketWidth, _defaultPocketHeight);
                SecureGrid = new InventoryGrid(_securePouchWidth, _securePouchHeight);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 가방 아이템을 장착(교체)하거나 벗습니다. null 전달 시 가방을 벗는(맨몸) 상태가 됩니다.
        /// </summary>
        public void EquipBackpack(DimensionLooter.Data.ItemData backpack)
        {
            int newW = _defaultPocketWidth;
            int newH = _defaultPocketHeight;

            if (backpack != null && backpack.Type == DimensionLooter.Core.Define.ItemType.Backpack)
            {
                newW = backpack.BagWidth;
                newH = backpack.BagHeight;
            }

            EquippedBackpack = backpack;

            // 크기 변경 및 오버플로우 반환
            var overflows = MainGrid.Resize(newW, newH);

            if (overflows.Count > 0)
            {
                // 오버플로우된 아이템들을 ItemData 리스트로 변환하여 바닥에 Pouch 형태로 드랍
                System.Collections.Generic.List<DimensionLooter.Data.ItemData> leftovers = new System.Collections.Generic.List<DimensionLooter.Data.ItemData>();
                foreach (var item in overflows)
                {
                    leftovers.Add(item.Data);
                }

                CombatManager cm = FindObjectOfType<CombatManager>();
                if (cm != null)
                {
                    cm.SpawnPouch(leftovers);
                    Debug.LogWarning($"[InventoryManager] 가방 크기가 줄어들어 {leftovers.Count}개의 아이템이 바닥에 드랍되었습니다.");
                }
                else
                {
                    Debug.LogError("[InventoryManager] CombatManager를 찾을 수 없어 오버플로우 아이템이 증발했습니다.");
                }
            }

            // UI 갱신 이벤트 호출
            OnGridResized?.Invoke();
        }

        /// <summary>
        /// 빈 공간을 찾아 자동으로 아이템을 배치합니다. (주로 치트나 퀘스트 보상용)
        /// </summary>
        public bool TryAddAuto(DimensionLooter.Data.ItemData itemData)
        {
            var invItem = new InventoryItem(itemData);
            for (int y = 0; y < MainGrid.Height; y++)
            {
                for (int x = 0; x < MainGrid.Width; x++)
                {
                    if (MainGrid.PlaceItem(invItem, x, y))
                    {
                        // UI 갱신을 위해 DualUIPanel이나 MainInventoryUI 측에 알려야 함
                        // 임시로 자동 배치 시에는 UI가 열려있다면 갱신하도록 처리 가능
                        // [Todo] Event 기반 UI 갱신 로직 (현재는 드래그 앤 드롭 전용이므로 생략)
                        Debug.Log($"[InventoryManager] {itemData.Name} 아이템을 자동 배치했습니다. ({x}, {y})");
                        return true;
                    }
                }
            }
            Debug.LogWarning($"[InventoryManager] 가방이 꽉 찼습니다! {itemData.Name} 배치 실패.");
            return false;
        }
    }
}
