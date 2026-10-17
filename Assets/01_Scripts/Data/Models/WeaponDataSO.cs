using UnityEngine;

namespace BagBack100.Data.Models
{
    [CreateAssetMenu(fileName = "NewWeaponData", menuName = "BagBack100/Data/Items/Weapon")]
    public class WeaponDataSO : EquipmentDataSO
    {
        [Header("Weapon Specific")]
        public float baseAttackSpeed = 1f; 
        public GameObject weaponPrefab; // 기본 무기 공격 이펙트 (1개만 연결 시 1~3타/차징에 자동 변형 재사용)

        [Header("Optional Combo & Charge Effects")]
        [Tooltip("타수별(1타, 2타, 3타) 개별 이펙트 프리팹 (비워두면 weaponPrefab을 반전/확대하여 자동 사용)")]
        public GameObject[] comboEffectPrefabs;
        [Tooltip("우클릭 차징 강공격 전용 이펙트 프리팹 (비워두면 weaponPrefab을 확대하여 사용)")]
        public GameObject chargedEffectPrefab;

        public GameObject GetComboEffectPrefab(int comboStep)
        {
            if (comboEffectPrefabs != null && comboStep >= 0 && comboStep < comboEffectPrefabs.Length && comboEffectPrefabs[comboStep] != null)
            {
                return comboEffectPrefabs[comboStep];
            }
            return weaponPrefab;
        }

        public GameObject GetChargedEffectPrefab()
        {
            return chargedEffectPrefab != null ? chargedEffectPrefab : weaponPrefab;
        }
    }
}
