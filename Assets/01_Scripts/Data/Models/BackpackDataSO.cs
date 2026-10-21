using UnityEngine;

namespace BagBack100.Data.Models
{
    [CreateAssetMenu(fileName = "NewBackpackData", menuName = "BagBack100/Data/Items/Backpack")]
    public class BackpackDataSO : EquipmentDataSO
    {
        [Header("Backpack Specific")]
        public int bagAddedSlots = 10;
        public float bagMaxWeight = 50f;
    }
}
