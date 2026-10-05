using System;
using System.Collections.Generic;

namespace BagBack100.Data.Core
{
    [Serializable]
    public class InventorySaveData
    {
        // 런타임 저장용 POCO
        public List<RuntimeItem> StoredItems = new List<RuntimeItem>();
    }

    [Serializable]
    public class EquipmentSaveData
    {
        // 부위별 장착된 아이템의 InstanceID 또는 RuntimeItem 참조를 저장
        // Dictionary는 직렬화가 까다로우므로 List 구조체 방식을 사용하거나 별도의 직렬화 구현 필요
        public List<EquipSlotData> EquippedSlots = new List<EquipSlotData>();
    }

    [Serializable]
    public class EquipSlotData
    {
        public EquipType Type;
        public RuntimeItem EquippedItem;
    }
}
