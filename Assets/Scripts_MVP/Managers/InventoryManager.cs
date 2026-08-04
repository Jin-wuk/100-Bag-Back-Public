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
        
        [Header("Stash Settings")]
        [SerializeField] private int _defaultStashTabs = 3;
        [SerializeField] private int _stashWidth = 10;
        [SerializeField] private int _stashHeight = 10;
        
        public System.Collections.Generic.List<InventoryGrid> StashGrids { get; private set; } = new System.Collections.Generic.List<InventoryGrid>();

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
                
                // 창고(Stash) 초기화
                for (int i = 0; i < _defaultStashTabs; i++)
                {
                    StashGrids.Add(new InventoryGrid(_stashWidth, _stashHeight));
                }
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
                System.Collections.Generic.List<DimensionLooter.Data.ItemInstance> leftovers = new System.Collections.Generic.List<DimensionLooter.Data.ItemInstance>();
                foreach (var item in overflows)
                {
                    leftovers.Add(item.Instance);
                }

                Vector3 dropPos = Vector3.zero;
                var player = FindObjectOfType<DimensionLooter.Gameplay.PlayerController>();
                if (player != null) dropPos = player.transform.position;

                if (DimensionLooter.Managers.LootManager.Instance != null)
                {
                    DimensionLooter.Managers.LootManager.Instance.SpawnPouch(leftovers, dropPos);
                    Debug.LogWarning($"[InventoryManager] 가방 크기가 줄어들어 {leftovers.Count}개의 아이템이 바닥에 드랍되었습니다.");
                }
                else
                {
                    Debug.LogError("[InventoryManager] LootManager를 찾을 수 없어 오버플로우 아이템이 증발했습니다.");
                }
            }

            // UI 갱신 이벤트 호출
            OnGridResized?.Invoke();
        }

        /// <summary>
        /// 보안 파우치의 크기를 영구적으로 확장합니다. MVP 기준 최대 2x3 (6칸)으로 하드 캡 제한이 적용됩니다.
        /// </summary>
        public bool UpgradeSecurePouch()
        {
            if (_securePouchWidth == 2 && _securePouchHeight == 2)
            {
                _securePouchHeight = 3; // 2x3으로 확장
                SecureGrid.Resize(_securePouchWidth, _securePouchHeight);
                OnGridResized?.Invoke();
                Debug.Log("[InventoryManager] 보안 파우치가 2x3 사이즈로 확장되었습니다.");
                return true;
            }
            
            Debug.Log("[InventoryManager] 보안 파우치가 이미 최대 크기(2x3)입니다.");
            return false;
        }

        /// <summary>
        /// 빈 공간을 찾아 자동으로 아이템을 배치합니다. (주로 치트나 퀘스트 보상용)
        /// </summary>
        public DimensionLooter.Gameplay.Inventory.InventoryItem TryAddAuto(DimensionLooter.Data.ItemInstance itemInst)
        {
            var invItem = new InventoryItem(itemInst);
            for (int y = 0; y < MainGrid.Height; y++)
            {
                for (int x = 0; x < MainGrid.Width; x++)
                {
                    if (MainGrid.PlaceItem(invItem, x, y))
                    {
                        Debug.Log($"[InventoryManager] {itemInst.Data.Name} 아이템을 자동 배치했습니다. ({x}, {y})");
                        return invItem;
                    }
                }
            }

            // 1차 시도 실패 시 회전해서 다시 시도
            if (itemInst.Data.Width != itemInst.Data.Height)
            {
                invItem.IsRotated = !invItem.IsRotated;
                for (int y = 0; y < MainGrid.Height; y++)
                {
                    for (int x = 0; x < MainGrid.Width; x++)
                    {
                        if (MainGrid.PlaceItem(invItem, x, y))
                        {
                            Debug.Log($"[InventoryManager] {itemInst.Data.Name} 아이템을 회전하여 자동 배치했습니다. ({x}, {y})");
                            return invItem;
                        }
                    }
                }
                invItem.IsRotated = !invItem.IsRotated; // 실패 시 롤백
            }

            Debug.LogWarning($"[InventoryManager] 가방이 꽉 찼습니다! {itemInst.Data.Name} 배치 실패.");
            return null;
        }

        /// <summary>
        /// 사망 시 메인 인벤토리의 모든 아이템을 완전히 삭제(증발)시킵니다.
        /// (보안 파우치의 내용은 건드리지 않습니다.)
        /// </summary>
        public void ClearMainInventory()
        {
            MainGrid.Clear();
            OnGridResized?.Invoke();
            Debug.Log("[InventoryManager] 사망 페널티: 메인 인벤토리의 아이템이 모두 증발했습니다.");
        }
    }
}
