using UnityEngine;

namespace BagBack100.Data.Models
{
    [CreateAssetMenu(fileName = "NewWeaponData", menuName = "BagBack100/Data/Items/Weapon")]
    public class WeaponDataSO : EquipmentDataSO
    {
        [Header("Weapon Specific")]
        public float baseAttackSpeed = 1f; 
        // 추후 무기 고유 이펙트 프리팹 등을 여기에 추가 가능
        // public GameObject weaponPrefab;
    }
}
