// File: Assets/Scripts/UI/Inventory/GearSlotUI.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DimensionLooter.Core;
using DimensionLooter.Managers;
using DimensionLooter.Gameplay.Inventory;

namespace DimensionLooter.UI.Inventory
{
    public class GearSlotUI : MonoBehaviour, IPointerClickHandler
    {
        [Header("Slot Settings")]
        public Define.ItemType slotType;
        [SerializeField] private Image _iconImage; // 장착된 아이템 아이콘
        [SerializeField] private Text _placeholderText;

        private InventoryItem _equippedItem;
        public InventoryItem EquippedItem => _equippedItem;

        public void Init(Define.ItemType type)
        {
            slotType = type;
            if (_placeholderText != null)
            {
                _placeholderText.text = type.ToString();
            }
            UpdateVisual();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // 더블클릭 감지
            if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount == 2)
            {
                UnequipToBag();
            }
        }

        public void UnequipToBag()
        {
            if (_equippedItem == null) return;

            InventoryItem oldItem = _equippedItem;
            Unequip(); // 상태 갱신 및 UI 비우기

            // 가방에 자동 추가 시도
            if (InventoryManager.Instance != null)
            {
                var addedItem = InventoryManager.Instance.TryAddAuto(oldItem.Data);
                if (addedItem != null)
                {
                    // UI에 생성
                    if (DualUIPanel.Instance != null)
                    {
                        DualUIPanel.Instance.SpawnMainInventoryItemUI(addedItem);
                    }
                }
                else
                {
                    // 가방이 꽉 찼다면 보상패널에 추가 시도, 보상패널도 닫혀있거나 꽉 찼다면 바닥에 드롭
                    bool addedToReward = false;
                    if (DualUIPanel.Instance != null && DualUIPanel.Instance.IsPanelOpen)
                    {
                        var addedRewardItem = DualUIPanel.Instance.TryAddRewardAuto(oldItem.Data);
                        if (addedRewardItem != null)
                        {
                            addedToReward = true;
                        }
                    }

                    if (!addedToReward)
                    {
                        Vector3 dropPos = Vector3.zero;
                        var player = FindObjectOfType<DimensionLooter.Gameplay.PlayerController>();
                        if (player != null) dropPos = player.transform.position;

                        if (DimensionLooter.Managers.LootManager.Instance != null)
                        {
                            DimensionLooter.Managers.LootManager.Instance.SpawnPouch(new System.Collections.Generic.List<DimensionLooter.Data.ItemData> { oldItem.Data }, dropPos);
                        }
                    }
                }
            }
        }

        public bool TryEquip(InventoryItem item, DraggableItemUI draggedUI)
        {
            if (item.Data.Type != slotType) return false;

            if (_equippedItem != null)
            {
                UnequipToBag();
            }

            _equippedItem = item;
            GearManager.Instance.Equip(item, slotType);
            
            // UI 객체(DraggableItemUI)는 더 이상 인벤토리 그리드에 속하지 않으므로 파괴
            if (draggedUI != null)
            {
                Destroy(draggedUI.gameObject);
            }

            UpdateVisual();
            return true;
        }

        public void Unequip()
        {
            if (_equippedItem == null) return;

            GearManager.Instance.Unequip(slotType);
            _equippedItem = null;
            UpdateVisual();
        }

        public void ForceClear()
        {
            _equippedItem = null;
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (_equippedItem != null)
            {
                if (_iconImage != null)
                {
                    _iconImage.gameObject.SetActive(true);
                    _iconImage.color = new Color(Random.Range(0.3f, 0.9f), Random.Range(0.3f, 0.9f), Random.Range(0.3f, 0.9f));
                }
                if (_placeholderText != null) _placeholderText.text = _equippedItem.Data.Name;
            }
            else
            {
                if (_iconImage != null) _iconImage.gameObject.SetActive(false);
                if (_placeholderText != null) _placeholderText.text = slotType.ToString();
            }
        }
    }
}
