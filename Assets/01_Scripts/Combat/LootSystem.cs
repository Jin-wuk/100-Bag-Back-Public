using UnityEngine;
using System.Collections.Generic;
using DimensionLooter.Core;
using DimensionLooter.Data.Models;
using DimensionLooter.Data.Core;
using DimensionLooter.Map;
using System.Linq;

namespace DimensionLooter.Combat
{
    public class LootSystem : MonoBehaviour
    {
        private Dictionary<int, DropPoolSO> _dropPools = new Dictionary<int, DropPoolSO>();
        private Dictionary<int, ItemDataSO> _itemDataCache = new Dictionary<int, ItemDataSO>();

        private void Awake()
        {
            // 리소스 폴더 전체에서 모든 드랍 풀과 아이템 데이터를 찾아 로드하여 캐싱
            var dropPools = Resources.LoadAll<DropPoolSO>("");
            foreach (var pool in dropPools)
            {
                _dropPools[pool.dropPoolID] = pool;
            }

            var items = Resources.LoadAll<ItemDataSO>("");
            foreach (var item in items)
            {
                _itemDataCache[item.itemID] = item;
            }
        }

        private void OnEnable()
        {
            GlobalEvents.OnMonsterDied += HandleMonsterDeath;
            GlobalEvents.OnItemDroppedOnGround += HandleItemDroppedOnGround;
        }

        private void OnDisable()
        {
            GlobalEvents.OnMonsterDied -= HandleMonsterDeath;
            GlobalEvents.OnItemDroppedOnGround -= HandleItemDroppedOnGround;
        }

        private void HandleMonsterDeath(Vector3 position, int dropPoolID)
        {
            if (!_dropPools.TryGetValue(dropPoolID, out var pool))
            {
                Debug.LogWarning($"[LootSystem] DropPoolID {dropPoolID} 를 찾을 수 없습니다.");
                return;
            }

            int totalWeight = pool.dropEntries.Sum(e => e.dropWeight);
            if (totalWeight <= 0) return;

            int randomValue = Random.Range(0, totalWeight);
            int currentWeight = 0;

            foreach (var entry in pool.dropEntries)
            {
                currentWeight += entry.dropWeight;
                if (randomValue < currentWeight)
                {
                    if (entry.itemID == -1 || entry.itemID == 0) 
                    {
                        // 꽝 (아무것도 안떨어짐)
                        break;
                    }

                    SpawnLootableItem(entry.itemID, position);
                    break;
                }
            }
        }

        private void HandleItemDroppedOnGround(RuntimeItem item, Vector3 position)
        {
            // 인벤토리에서 바닥에 버려질 때 호출됨
            SpawnLootableItem(item, position);
        }

        private void SpawnLootableItem(int itemID, Vector3 position)
        {
            if (!_itemDataCache.TryGetValue(itemID, out var itemData))
            {
                Debug.LogError($"[LootSystem] ItemID {itemID} 데이터를 찾을 수 없습니다.");
                return;
            }

            RuntimeItem newItem = new RuntimeItem(itemData);
            SpawnLootableItem(newItem, position);
        }

        private void SpawnLootableItem(RuntimeItem item, Vector3 position)
        {
            // TODO: 풀링 시스템 연동 필요. 임시로 Instantiate 사용
            GameObject go = new GameObject($"LootableItem_{item.ItemData.itemName}");
            go.transform.position = position;
            go.layer = LayerMask.NameToLayer("Default"); // TODO: 상호작용 레이어 설정 시 변경 필요

            LootableItem lootable = go.AddComponent<LootableItem>();
            lootable.Setup(item);
        }
    }
}
