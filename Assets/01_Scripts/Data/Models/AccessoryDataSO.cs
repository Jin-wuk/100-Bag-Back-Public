using UnityEngine;

namespace BagBack100.Data.Models
{
    [CreateAssetMenu(fileName = "NewAccessoryData", menuName = "BagBack100/Data/Items/Accessory")]
    public class AccessoryDataSO : EquipmentDataSO
    {
        [Header("Accessory Specific")]
        public float attackSpeedBonus; // 장신구가 올려주는 공속 증가량
    }
}
