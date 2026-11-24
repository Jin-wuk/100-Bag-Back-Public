using System.Collections.Generic;
using UnityEngine;
using BagBack100.Data.Models;
using BagBack100.Player;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 퀵슬롯 1·2·3 HUD. 등록된 소모품의 아이콘·가방 내 총 수량·공용 쿨타임을 표시한다. 4번째 DASH 칸은 표시 여부만 정한다(쿨타임은 DashCooldownUI).
    /// Dependencies: QuickSlotManager, InventoryManager, ConsumableController(던전), QuickSlotSlotUI
    /// Events: 없음 (구독: QuickSlotManager.OnQuickSlotsChanged, InventoryManager.OnInventoryChanged)
    /// </summary>
    public class QuickSlotBarUI : MonoBehaviour
    {
        [SerializeField] private List<QuickSlotSlotUI> _slots = new List<QuickSlotSlotUI>();
        [Tooltip("4번째 DASH 칸(DashCooldownUI). 대시가 없는 허브에서는 끈다")]
        [SerializeField] private GameObject _dashSlot;
        [SerializeField] private bool _showDashSlot = true;

        private QuickSlotManager _manager;

        private void Awake()
        {
            if (_dashSlot != null) _dashSlot.SetActive(_showDashSlot);
            QuickSlotManager.OnManagerCreated += Bind;
            var existing = FindObjectOfType<QuickSlotManager>();
            if (existing != null) Bind(existing);
        }

        private void Start()
        {
            // 허브의 QuickSlotManager는 HubSystem.Awake에서 동적으로 붙으므로 실행 순서에 따라 Awake에서 못 찾을 수 있다
            if (_manager == null)
            {
                var existing = FindObjectOfType<QuickSlotManager>();
                if (existing != null) Bind(existing);
            }
        }

        private void OnDestroy()
        {
            QuickSlotManager.OnManagerCreated -= Bind;
            Unbind();
        }

        private void Bind(QuickSlotManager manager)
        {
            if (_manager == manager) return;
            Unbind();
            _manager = manager;
            if (_manager == null) return;

            _manager.OnQuickSlotsChanged += Refresh;
            if (_manager.Inventory != null) _manager.Inventory.OnInventoryChanged += Refresh;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] != null) _slots[i].Bind(_manager);
            }
            Refresh();
        }

        private void Unbind()
        {
            if (_manager == null) return;
            _manager.OnQuickSlotsChanged -= Refresh;
            if (_manager.Inventory != null) _manager.Inventory.OnInventoryChanged -= Refresh;
            _manager = null;
        }

        private void Refresh()
        {
            if (_manager == null) return;

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot == null) continue;

                int itemID = _manager.GetItemId(slot.Index);
                bool assigned = itemID != 0;
                int count = assigned && _manager.Inventory != null ? _manager.Inventory.CountItems(itemID) : 0;
                slot.Refresh(assigned ? _manager.GetIcon(slot.Index) : null, count, assigned);
            }
        }

        private void Update()
        {
            var consumables = _manager != null ? _manager.Consumables : null;
            float fill = 0f;
            if (consumables != null && consumables.GcdDuration > 0f)
            {
                fill = consumables.GcdRemaining / consumables.GcdDuration;
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] != null) _slots[i].SetCooldownFill(fill);
            }
        }
    }
}
