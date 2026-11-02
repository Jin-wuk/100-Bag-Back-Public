using UnityEngine;

namespace BagBack100.Data.Models
{
    /// <summary>
    /// Role: 포션 등 소모품 정적 데이터. 전투 중에는 지속 회복 + 공용 쿨타임, 전투가 아니면 즉시 회복.
    /// Dependencies: ItemDataSO
    /// Events: 없음
    /// </summary>
    [CreateAssetMenu(fileName = "NewConsumableData", menuName = "BagBack100/Data/Items/Consumable")]
    public class ConsumableDataSO : ItemDataSO
    {
        [Header("Consumable Specific")]
        [Min(0)] public int hpRestore;
        [Min(0)] public int mpRestore;

        [Tooltip("전투 중 사용 시 회복량을 나눠 받는 시간(초)")]
        [Min(0.1f)] public float hotDuration = 5f;

        [Tooltip("전투 중 사용 시 모든 포션에 걸리는 공용 쿨타임(초)")]
        [Min(0f)] public float combatGcd = 7f;
    }
}
