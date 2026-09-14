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

        private Grid _gridCache;
        private Grid _grid
        {
            get
            {
                if (_gridCache == null) _gridCache = FindObjectOfType<Grid>();
                return _gridCache;
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

            // 1. 확정 재화 (골드)
            if (pool.maxGold > 0)
            {
                int goldAmount = Random.Range(pool.minGold, pool.maxGold + 1);
                if (goldAmount > 0)
                {
                    // 재화는 렉 방지를 위해 수량 전체를 1개의 뭉치(Bundle)로 스폰
                    SpawnLootableItem(pool.goldItemID, goldAmount, position, true);
                }
            }

            // 2. 확정 아이템
            if (pool.guaranteedDrops != null)
            {
                foreach (var fixedDrop in pool.guaranteedDrops)
                {
                    if (fixedDrop.itemID <= 0) continue;
                    
                    int minAmt = fixedDrop.minAmount > 0 ? fixedDrop.minAmount : 1;
                    int maxAmt = fixedDrop.maxAmount >= minAmt ? fixedDrop.maxAmount : minAmt;
                    int amount = Random.Range(minAmt, maxAmt + 1);
                    
                    if (amount > 0) SpawnLootableItem(fixedDrop.itemID, amount, position);
                }
            }

            // 3. 랜덤 그룹 다중 뽑기
            if (pool.randomDropGroups != null)
            {
                foreach (var group in pool.randomDropGroups)
                {
                    if (group.entries == null || group.entries.Count == 0) continue;
                    
                    // 디버그: 최대 횟수가 0인지 체크 (유니티 인스펙터에서 안 바꿨을 경우)
                    if (group.maxRolls <= 0)
                    {
                        Debug.LogWarning($"[LootSystem] '{group.groupName}' 그룹의 최대 뽑기 횟수(maxRolls)가 0입니다. 인스펙터에서 1 이상으로 설정해주세요!");
                        continue;
                    }

                    int rolls = Random.Range(group.minRolls, group.maxRolls + 1);
                    if (rolls <= 0) continue;

                    int totalWeight = group.entries.Sum(e => e.dropWeight);
                    if (totalWeight <= 0) 
                    {
                        Debug.LogWarning($"[LootSystem] '{group.groupName}' 그룹의 총 가중치 합이 0입니다. 가중치를 확인해주세요.");
                        continue;
                    }

                    for (int i = 0; i < rolls; i++)
                    {
                        int randomValue = Random.Range(0, totalWeight);
                        int currentWeight = 0;

                        foreach (var entry in group.entries)
                        {
                            currentWeight += entry.dropWeight;
                            if (randomValue < currentWeight)
                            {
                                if (entry.itemID > 0) // 0 이거나 -1 이면 꽝
                                {
                                    int minAmt = entry.minAmount > 0 ? entry.minAmount : 1;
                                    int maxAmt = entry.maxAmount >= minAmt ? entry.maxAmount : minAmt;
                                    int amount = Random.Range(minAmt, maxAmt + 1);
                                    
                                    SpawnLootableItem(entry.itemID, amount, position);
                                }
                                else
                                {
                                    Debug.Log($"[LootSystem] 꽝 당첨됨 (itemID가 {entry.itemID})");
                                }
                                break; // 해당 롤(Roll) 종료
                            }
                        }
                    }
                }
            }
        }

        private void HandleItemDroppedOnGround(RuntimeItem item, Vector3 position)
        {
            // 인벤토리에서 바닥에 버려질 때 호출됨
            SpawnLootableItem(item, position);
        }

        private void SpawnLootableItem(int itemID, int amount, Vector3 position, bool spawnAsBundle = false)
        {
            if (!_itemDataCache.TryGetValue(itemID, out var itemData))
            {
                Debug.LogError($"[LootSystem] ItemID {itemID} 데이터를 찾을 수 없습니다.");
                return;
            }

            if (spawnAsBundle)
            {
                // 응집형 드랍 (예: 500골드가 1개의 타일에 스폰)
                RuntimeItem newItem = new RuntimeItem(itemData);
                newItem.Count = amount;
                SpawnLootableItem(newItem, position);
            }
            else
            {
                // 분산형 드랍 (예: 수량이 50개면 1개짜리 아이템을 50개의 개별 타일에 흩뿌림)
                for (int i = 0; i < amount; i++)
                {
                    RuntimeItem newItem = new RuntimeItem(itemData);
                    newItem.Count = 1;
                    SpawnLootableItem(newItem, position);
                }
            }
        }

        private void SpawnLootableItem(RuntimeItem item, Vector3 position)
        {
            Vector3Int cellPos = Vector3Int.zero;
            Vector3 spawnPos = position;

            if (_grid != null && TileSystem.Instance != null)
            {
                Vector3Int startCell = _grid.WorldToCell(position);
                startCell.z = 0; // 2D 그리드 오차 방지
                
                cellPos = GetNearestEmptyItemTile(startCell);
                spawnPos = _grid.GetCellCenterWorld(cellPos);
            }

            // TODO: 풀링 시스템 연동 필요. 임시로 Instantiate 사용
            GameObject go = new GameObject($"LootableItem_{item.ItemData.itemName}");
            go.transform.position = spawnPos;
            go.layer = LayerMask.NameToLayer("Default"); // TODO: 상호작용 레이어 설정 시 변경 필요

            LootableItem lootable = go.AddComponent<LootableItem>();
            lootable.Setup(item, cellPos);

            if (TileSystem.Instance != null)
            {
                Debug.Log($"[LootSystem] 아이템 드랍 및 등록 - 이름: {item.ItemData.itemName}, 좌표: {cellPos}");
                TileSystem.Instance.RegisterItemTile(cellPos, lootable);
            }
        }

        private Vector3Int GetNearestEmptyItemTile(Vector3Int startCell)
        {
            if (TileSystem.Instance.GetItemAtTile(startCell) == null)
            {
                return startCell;
            }

            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
            queue.Enqueue(startCell);
            visited.Add(startCell);

            Vector3Int[] dirs = { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left,
                                  new Vector3Int(1, 1, 0), new Vector3Int(1, -1, 0), new Vector3Int(-1, -1, 0), new Vector3Int(-1, 1, 0) };

            while (queue.Count > 0)
            {
                Vector3Int curr = queue.Dequeue();
                
                foreach (var dir in dirs)
                {
                    Vector3Int next = curr + dir;
                    if (!visited.Contains(next))
                    {
                        if (TileSystem.Instance.GetItemAtTile(next) == null)
                        {
                            return next;
                        }
                        
                        visited.Add(next);
                        queue.Enqueue(next);
                    }
                }
            }

            return startCell; // Fallback
        }
    }
}
