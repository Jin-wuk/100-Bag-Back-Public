using UnityEngine;
using System.Collections.Generic;
using BagBack100.Core;
using BagBack100.Data.Models;
using BagBack100.Data.Core;
using BagBack100.Map;
using System.Linq;

namespace BagBack100.Combat
{
    public class LootSystem : MonoBehaviour
    {
        [SerializeField] private LayerMask _obstacleLayerMask; // 벽 너머 드랍 방지용 장애물 레이어

        private Dictionary<int, DropPoolSO> _dropPools = new Dictionary<int, DropPoolSO>();
        private Dictionary<int, ItemDataSO> _itemDataCache = new Dictionary<int, ItemDataSO>();

        private void Awake()
        {
            if (_obstacleLayerMask.value == 0)
            {
                Debug.LogError("[LootSystem] 치명적 오류: 인스펙터에서 'Obstacle Layer Mask'가 아무것도 선택되지 않았습니다(Nothing). 이로 인해 벽을 인식하지 못하고 벽 안에 아이템이 드랍됩니다!");
            }

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

                    // 애니메이션 커브(우하향 곡선 등)를 적용한 체감형 랜덤 뽑기 횟수 계산
                    float randomVal = Random.value; // 0.0 ~ 1.0 랜덤값
                    float curveMultiplier = Mathf.Clamp01(group.rollProbabilityCurve.Evaluate(randomVal));
                    
                    // 각 숫자가 공평한 구간을 가지도록(Uniform bucketing) FloorToInt와 0.99f 사용
                    float lerped = Mathf.Lerp(group.minRolls, group.maxRolls + 0.99f, curveMultiplier);
                    int rolls = Mathf.FloorToInt(lerped);

                    Debug.Log($"[LootSystem] 🎲 '{group.groupName}' 그룹 롤링 횟수 결정: 난수({randomVal:F2}) 👉 커브변환({curveMultiplier:F2}) 👉 최종 [{rolls}회] 굴림! (설정 범위: {group.minRolls}~{group.maxRolls})");

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
                                    
                                    Debug.Log($"[LootSystem] '{group.groupName}' 확률 롤: 총 가중치 {totalWeight} 중 주사위 {randomValue} 나옴 👉 ItemID {entry.itemID} ({amount}개) 당첨!");
                                    SpawnLootableItem(entry.itemID, amount, position);
                                }
                                else
                                {
                                    Debug.Log($"[LootSystem] '{group.groupName}' 확률 롤: 총 가중치 {totalWeight} 중 주사위 {randomValue} 나옴 👉 꽝 당첨됨 (ItemID가 {entry.itemID})");
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
            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
            queue.Enqueue(startCell);
            visited.Add(startCell);

            Vector3Int[] dirs = { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left,
                                  new Vector3Int(1, 1, 0), new Vector3Int(1, -1, 0), new Vector3Int(-1, -1, 0), new Vector3Int(-1, 1, 0) };

            while (queue.Count > 0)
            {
                Vector3Int curr = queue.Dequeue();
                
                // 1. 현재 좌표가 장애물(벽)인지 확인
                Vector2Int currPos2D = new Vector2Int(curr.x, curr.y);
                bool isObstacle = Pathfinder.IsObstacle(_grid, currPos2D, _obstacleLayerMask);

                // 2. 벽이 아니면서, 다른 아이템이 없는 빈 타일인 경우에만 스폰 위치로 확정
                if (!isObstacle && TileSystem.Instance.GetItemAtTile(curr) == null)
                {
                    return curr;
                }

                // 3. 만약 현재 타일이 장애물(벽)이라면, 이 타일을 기점으로 더 이상 탐색을 확장하지 않음 (벽 너머 스폰 원천 차단)
                // 단, 최초 시작점(몬스터 사망 위치)이 벽에 겹쳐있는 예외 상황에서는 바깥으로 빠져나오기 위해 탐색을 허용함
                if (isObstacle && curr != startCell)
                {
                    continue; 
                }

                // 4. 다음 인접 타일 탐색
                foreach (var dir in dirs)
                {
                    Vector3Int next = curr + dir;
                    if (!visited.Contains(next))
                    {
                        visited.Add(next);
                        queue.Enqueue(next);
                    }
                }
            }

            return startCell; // Fallback
        }
    }
}
