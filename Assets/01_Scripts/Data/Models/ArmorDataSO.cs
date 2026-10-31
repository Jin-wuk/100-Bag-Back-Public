using UnityEngine;
using BagBack100.Data.Core;

namespace BagBack100.Data.Models
{
    /// <summary>
    /// Role: 방어구(투구·갑옷·신발) 정적 데이터. 제작 전용 장비(던전 드랍 없음).
    /// Dependencies: EquipmentDataSO, ArmorBranch
    /// Events: 없음
    /// </summary>
    [CreateAssetMenu(fileName = "NewArmorData", menuName = "BagBack100/Data/Items/Armor")]
    public class ArmorDataSO : EquipmentDataSO
    {
        [Header("Armor Specific")]
        [Tooltip("방어구 테크트리 분기. 분기별 효과는 테크트리 작업 시 적용")]
        public ArmorBranch branch = ArmorBranch.Heavy;
    }
}
