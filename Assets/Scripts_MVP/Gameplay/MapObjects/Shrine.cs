using UnityEngine;
using DimensionLooter.Core;
using DimensionLooter.Gameplay.Combat;
using DimensionLooter.Managers;

namespace DimensionLooter.Gameplay.MapObjects
{
    public enum ShrineType
    {
        Healing,
        Buff,
        Sacrifice
    }

    /// <summary>
    /// 플레이어가 상호작용하여 체력 회복이나 전투 버프 등을 얻을 수 있는 제단입니다.
    /// </summary>
    public class Shrine : MonoBehaviour, IInteractable
    {
        public ShrineType type = ShrineType.Healing;

        [Header("Healing Settings")]
        [Tooltip("회복량")]
        public int healAmount = 50;

        [Header("Buff Settings")]
        [Tooltip("공격력 증가량")]
        public int buffAttack = 10;
        [Tooltip("방어력 증가량")]
        public int buffDefense = 5;
        [Tooltip("버프가 유지되는 전투 횟수")]
        public int buffEncounters = 3;

        private bool _isUsed = false;

        public void Interact(GameObject interactor)
        {
            if (_isUsed) return;

            var playerStats = interactor.GetComponent<PlayerStats>();
            if (playerStats == null) playerStats = FindObjectOfType<PlayerStats>();
            if (playerStats == null) return;

            bool success = false;

            switch (type)
            {
                case ShrineType.Healing:
                    if (playerStats.CurrentHealth < playerStats.MaxHealth)
                    {
                        playerStats.Heal(healAmount);
                        Debug.Log($"[Shrine] 회복의 제단 사용: {healAmount} 회복");
                        success = true;
                    }
                    else
                    {
                        Debug.Log("[Shrine] 체력이 가득 차 있어 제단을 사용할 수 없습니다.");
                    }
                    break;

                case ShrineType.Buff:
                    playerStats.ApplyShrineBuff(buffAttack, buffDefense, buffEncounters);
                    success = true;
                    break;

                case ShrineType.Sacrifice:
                    success = TrySacrifice(playerStats);
                    break;
            }

            if (success)
            {
                _isUsed = true;
                // 사용 후 비활성화 또는 스프라이트 변경 처리
                Destroy(gameObject); // 임시로 파괴
            }
        }

        private bool TrySacrifice(PlayerStats playerStats)
        {
            if (InventoryManager.Instance == null) return false;

            var mainGrid = InventoryManager.Instance.MainGrid;
            var allItems = mainGrid.GetAllItems();
            if (allItems.Count > 0)
            {
                // 랜덤하게 하나 희생 (MVP용 임시 구현)
                int randomIndex = Random.Range(0, allItems.Count);
                var itemToSacrifice = allItems[randomIndex];
                
                string itemName = itemToSacrifice.Instance.Data.Name;
                mainGrid.RemoveItem(itemToSacrifice);
                
                Debug.Log($"[Shrine] 희생의 제단: '{itemName}'을(를) 바쳐 강력한 버프를 얻었습니다!");
                playerStats.ApplyShrineBuff(buffAttack * 2, buffDefense * 2, buffEncounters);
                return true;
            }
            else
            {
                Debug.Log("[Shrine] 희생의 제단: 바칠 아이템이 인벤토리에 없습니다.");
                return false;
            }
        }
    }
}
