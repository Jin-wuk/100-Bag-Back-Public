using UnityEngine;

namespace BagBack100.Data.Models
{
    [CreateAssetMenu(fileName = "NewWeaponData", menuName = "BagBack100/Data/Items/Weapon")]
    public class WeaponDataSO : EquipmentDataSO
    {
        [Header("Weapon Specific")]
        public float baseAttackSpeed = 1f; 
        public GameObject weaponPrefab; // 무기 전용 공격 이펙트 (프리팹)
    }
}
