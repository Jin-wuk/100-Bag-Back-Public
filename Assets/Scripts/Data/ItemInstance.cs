// File: Assets/Scripts/Data/ItemInstance.cs
using System.Collections.Generic;
using UnityEngine;

namespace DimensionLooter.Data
{
    public class ItemInstance
    {
        public string InstanceId { get; private set; }
        public ItemData Data { get; private set; }
        public string AssignedSkillId { get; private set; }
        public bool IsUnidentified { get; set; }

        public ItemInstance(ItemData data)
        {
            InstanceId = System.Guid.NewGuid().ToString();
            Data = data;
            AssignedSkillId = RollSkill(data.PossibleSkills);
            
            // 장비류는 기본적으로 미확인 상태
            if (data.Type == DimensionLooter.Core.Define.ItemType.Weapon || 
                data.Type == DimensionLooter.Core.Define.ItemType.Armor ||
                data.Type == DimensionLooter.Core.Define.ItemType.Helmet ||
                data.Type == DimensionLooter.Core.Define.ItemType.Accessory)
            {
                IsUnidentified = true;
            }
            else
            {
                IsUnidentified = false;
            }
        }
        
        // 역직렬화 또는 인스턴스 복원용
        public ItemInstance(ItemData data, string assignedSkillId, bool isUnidentified = false)
        {
            InstanceId = System.Guid.NewGuid().ToString();
            Data = data;
            AssignedSkillId = assignedSkillId;
            IsUnidentified = isUnidentified;
        }

        private string RollSkill(string possibleSkills)
        {
            if (string.IsNullOrEmpty(possibleSkills))
                return null;

            // 예: "SKL_001:50|SKL_004:50"
            string[] parts = possibleSkills.Split('|');
            List<KeyValuePair<string, int>> skillWeights = new List<KeyValuePair<string, int>>();
            int totalWeight = 0;

            foreach (var part in parts)
            {
                string[] sw = part.Split(':');
                if (sw.Length == 2 && int.TryParse(sw[1], out int weight))
                {
                    skillWeights.Add(new KeyValuePair<string, int>(sw[0], weight));
                    totalWeight += weight;
                }
            }

            if (totalWeight <= 0) return null;

            int roll = Random.Range(0, totalWeight);
            int current = 0;
            foreach (var kvp in skillWeights)
            {
                current += kvp.Value;
                if (roll < current)
                {
                    return kvp.Key;
                }
            }

            return null;
        }
    }
}
