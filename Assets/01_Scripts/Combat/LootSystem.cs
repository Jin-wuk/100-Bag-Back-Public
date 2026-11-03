using UnityEngine;
using System.Collections.Generic;
using BagBack100.Core;
using BagBack100.Data.Models;
using BagBack100.Data.Core;
using BagBack100.Map;
using BagBack100.Dungeon; // [추가] FogOfWar 연동을 위한 네임스페이스

namespace BagBack100.Combat
{
    /// <summary>
    /// Role: 몬스터 사망·상자 열림 시 드랍 풀 추첨과 필드 아이템 스폰, 바닥 드랍 요청 처리. 드랍 풀 데이터 검증 경고.
    /// Dependencies: DropPoolSO, ItemDataSO, TileSystem, Pathfinder, FogVisibilityObject, LootableItem
    /// Events: 없음 (구독: GlobalEvents.OnMonsterDied, GlobalEvents.OnChestOpened, GlobalEvents.OnItemDroppedOnGround, GlobalEvents.OnItemDiscardRequested)
    /// </summary>
    public class LootSystem : MonoBehaviour
    {
        [SerializeField] private LayerMask _obstacleLayerMask; // 벽 너머 드랍 방지용 장애물 레이어

        [Header("Fog of War Settings")]
        [Tooltip("바닥에 드랍된 아이템의 안개 가시성 모드 (VisibleOnly: 시야 내에만 표시 / RememberWhenExplored: 시야 밖에서도 어둡게 표시)")]
        [SerializeField] private FogVisibilityMode _droppedItemFogMode = FogVisibilityMode.RememberWhenExplored;

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

            WarnCraftOnlyItemsInDropPools(dropPools);
        }

        /// <summary>
        /// 무기·방어구·가방은 기지 제작 전용이고 던전 드랍은 악세서리만 허용된다(기획 확정 D4/D7).
        /// 데이터 입력 실수를 에디터 단계에서 잡기 위해 드랍 풀에 제작 전용 장비가 있으면 경고만 남긴다.
        /// </summary>
        private void WarnCraftOnlyItemsInDropPools(DropPoolSO[] dropPools)
        {
            foreach (var pool in dropPools)
            {
                if (pool.guaranteedDrops != null)
                {
                    foreach (var entry in pool.guaranteedDrops) WarnIfCraftOnly(pool, entry.itemID);
                }
                if (pool.randomDropGroups == null) continue;
                foreach (var group in pool.randomDropGroups)
                {
                    if (group?.entries == null) continue;
                    foreach (var entry in group.entries) WarnIfCraftOnly(pool, entry.itemID);
                }
            }
        }

        private void WarnIfCraftOnly(DropPoolSO pool, int itemID)
        {
            if (!_itemDataCache.TryGetValue(itemID, out var data) || !(data is EquipmentDataSO eq)) return;

            bool isAccessory = eq.equipType == EquipType.Ring || eq.equipType == EquipType.Necklace || eq.equipType == EquipType.Earring;
            if (!isAccessory)
            {
                Debug.LogWarning($"[LootSystem] DropPool {pool.dropPoolID}({pool.name})에 제작 전용 장비 {itemID}({eq.equipType})가 들어 있습니다. 던전 드랍은 악세서리만 허용됩니다.");
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
            GlobalEvents.OnChestOpened += HandleChestOpened;
            GlobalEvents.OnItemDroppedOnGround += HandleItemDroppedOnGround;
            GlobalEvents.OnItemDiscardRequested += HandleItemDiscardRequested;
        }

        private void OnDisable()
        {
            GlobalEvents.OnMonsterDied -= HandleMonsterDeath;
            GlobalEvents.OnChestOpened -= HandleChestOpened;
            GlobalEvents.OnItemDroppedOnGround -= HandleItemDroppedOnGround;
            GlobalEvents.OnItemDiscardRequested -= HandleItemDiscardRequested;
        }

        private void HandleMonsterDeath(Vector3 position, int dropPoolID) => DropFromPool(position, dropPoolID, false);

        // 상자 칸은 플레이어가 막 밟은 칸이고 열린 상자 스프라이트가 남아 있으므로 아이템은 주변 칸부터 놓는다
        private void HandleChestOpened(Vector3 position, int dropPoolID) => DropFromPool(position, dropPoolID, true);

        // 몬스터와 상자가 같은 드랍 풀 구조(확정 + 가중치 그룹)를 공유한다
        private void DropFromPool(Vector3 position, int dropPoolID, bool avoidOriginCell)
        {
            if (!_dropPools.TryGetValue(dropPoolID, out var pool))
            {
                Debug.LogWarning($"[LootSystem] DropPoolID {dropPoolID} 를 찾을 수 없습니다.");
                return;
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

                    if (amount > 0) SpawnLootableItem(fixedDrop.itemID, amount, position, avoidOriginCell);
                }
            }

            // 3. 랜덤 그룹 다중 뽑기
            if (pool.randomDropGroups != null)
            {
                foreach (var group in pool.randomDropGroups)
                {
                    if (group.entries == null || group.entries.Count == 0) continue;

                    if (group.maxRolls <= 0)
                    {
                        Debug.LogWarning($"[LootSystem] '{group.groupName}' 그룹의 최대 뽑기 횟수(maxRolls)가 0입니다. 인스펙터에서 1 이상으로 설정해주세요!");
                        continue;
                    }

                    float randomVal = Random.value;
                    float curveMultiplier = Mathf.Clamp01(group.rollProbabilityCurve.Evaluate(randomVal));

                    float lerped = Mathf.Lerp(group.minRolls, group.maxRolls + 0.99f, curveMultiplier);
                    int rolls = Mathf.FloorToInt(lerped);

                    Debug.Log($"[LootSystem] 🎲 '{group.groupName}' 그룹 롤링 횟수 결정: 난수({randomVal:F2}) 👉 커브변환({curveMultiplier:F2}) 👉 최종 [{rolls}회] 굴림! (설정 범위: {group.minRolls}~{group.maxRolls})");

                    if (rolls <= 0) continue;

                    int totalWeight = 0;
                    for (int w = 0; w < group.entries.Count; w++) totalWeight += group.entries[w].dropWeight;
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
                                if (entry.itemID > 0)
                                {
                                    int minAmt = entry.minAmount > 0 ? entry.minAmount : 1;
                                    int maxAmt = entry.maxAmount >= minAmt ? entry.maxAmount : minAmt;
                                    int amount = Random.Range(minAmt, maxAmt + 1);

                                    Debug.Log($"[LootSystem] '{group.groupName}' 확률 롤: 총 가중치 {totalWeight} 중 주사위 {randomValue} 나옴 👉 ItemID {entry.itemID} ({amount}개) 당첨!");
                                    SpawnLootableItem(entry.itemID, amount, position, avoidOriginCell);
                                }
                                else
                                {
                                    Debug.Log($"[LootSystem] '{group.groupName}' 확률 롤: 총 가중치 {totalWeight} 중 주사위 {randomValue} 나옴 👉 꽝 당첨됨 (ItemID가 {entry.itemID})");
                                }
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void HandleItemDroppedOnGround(RuntimeItem item, Vector3 position)
        {
            SpawnLootableItem(item, position, false);
        }

        private void SpawnLootableItem(int itemID, int amount, Vector3 position, bool avoidOriginCell, bool spawnAsBundle = false)
        {
            if (!_itemDataCache.TryGetValue(itemID, out var itemData))
            {
                Debug.LogError($"[LootSystem] ItemID {itemID} 데이터를 찾을 수 없습니다.");
                return;
            }

            if (spawnAsBundle)
            {
                RuntimeItem newItem = new RuntimeItem(itemData);
                newItem.Count = amount;
                SpawnLootableItem(newItem, position, avoidOriginCell);
            }
            else
            {
                for (int i = 0; i < amount; i++)
                {
                    RuntimeItem newItem = new RuntimeItem(itemData);
                    newItem.Count = 1;
                    SpawnLootableItem(newItem, position, avoidOriginCell);
                }
            }
        }

        private static readonly Vector3Int[] DiscardDirs =
        {
            Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left,
            new Vector3Int(1, 1, 0), new Vector3Int(1, -1, 0), new Vector3Int(-1, -1, 0), new Vector3Int(-1, 1, 0)
        };
        private readonly Queue<Vector3Int> _discardQueue = new Queue<Vector3Int>();
        private readonly HashSet<Vector3Int> _discardVisited = new HashSet<Vector3Int>();

        private void HandleItemDiscardRequested(RuntimeItem item, Vector3 playerPosition, int radius, System.Action<bool> onResult)
        {
            if (item == null || _grid == null || TileSystem.Instance == null)
            {
                onResult?.Invoke(false);
                return;
            }

            Vector3Int playerCell = _grid.WorldToCell(playerPosition);
            playerCell.z = 0;

            if (!TryFindDiscardCell(playerCell, radius, out Vector3Int cell))
            {
                onResult?.Invoke(false);
                return;
            }

            SpawnLootableItemAtCell(item, cell);
            onResult?.Invoke(true);
        }

        /// <summary>
        /// 플레이어 칸 기준 체비셰프 반경 안에서 가장 가까운 빈 칸을 찾는다. 플레이어가 서 있는 칸은 제외(바로 다시 줍지 않도록).
        /// 벽은 통과하지 않고, 대각선은 양옆 직교 칸이 모두 열려 있을 때만 확장해 벽 모서리 틈으로 넘어가지 않는다.
        /// </summary>
        private bool TryFindDiscardCell(Vector3Int origin, int radius, out Vector3Int result)
        {
            _discardQueue.Clear();
            _discardVisited.Clear();
            _discardQueue.Enqueue(origin);
            _discardVisited.Add(origin);

            while (_discardQueue.Count > 0)
            {
                Vector3Int curr = _discardQueue.Dequeue();

                if (curr != origin && TileSystem.Instance.GetItemAtTile(curr) == null && TileSystem.Instance.GetEntityAtTile(curr) == null)
                {
                    result = curr;
                    return true;
                }

                for (int i = 0; i < DiscardDirs.Length; i++)
                {
                    Vector3Int dir = DiscardDirs[i];
                    Vector3Int next = curr + dir;
                    if (_discardVisited.Contains(next)) continue;
                    if (Mathf.Max(Mathf.Abs(next.x - origin.x), Mathf.Abs(next.y - origin.y)) > radius) continue;
                    if (IsBlocked(next)) continue;

                    bool isDiagonal = dir.x != 0 && dir.y != 0;
                    if (isDiagonal && (IsBlocked(new Vector3Int(curr.x + dir.x, curr.y, 0)) || IsBlocked(new Vector3Int(curr.x, curr.y + dir.y, 0)))) continue;

                    _discardVisited.Add(next);
                    _discardQueue.Enqueue(next);
                }
            }

            result = origin;
            return false;
        }

        private bool IsBlocked(Vector3Int cell) => Pathfinder.IsObstacle(_grid, new Vector2Int(cell.x, cell.y), _obstacleLayerMask);

        private void SpawnLootableItem(RuntimeItem item, Vector3 position, bool avoidOriginCell)
        {
            if (_grid == null || TileSystem.Instance == null)
            {
                SpawnLootableItemAt(item, Vector3Int.zero, position);
                return;
            }

            Vector3Int startCell = _grid.WorldToCell(position);
            startCell.z = 0;
            SpawnLootableItemAtCell(item, GetNearestEmptyItemTile(startCell, avoidOriginCell));
        }

        private void SpawnLootableItemAtCell(RuntimeItem item, Vector3Int cellPos)
        {
            SpawnLootableItemAt(item, cellPos, _grid.GetCellCenterWorld(cellPos));
        }

        private void SpawnLootableItemAt(RuntimeItem item, Vector3Int cellPos, Vector3 spawnPos)
        {
            // TODO: 풀링 시스템 연동 필요. 임시로 Instantiate 사용
            GameObject go = new GameObject($"LootableItem_{item.ItemData.itemName}");
            go.transform.position = spawnPos;
            go.layer = LayerMask.NameToLayer("Default");

            LootableItem lootable = go.AddComponent<LootableItem>();
            lootable.Setup(item, cellPos);

            // [추가] Setup 이후 SpriteRenderer가 생성된 상태에서 FogVisibilityObject 부착 및 초기화
            FogVisibilityObject fogVis = go.AddComponent<FogVisibilityObject>();
            fogVis.InitializeRuntime(_droppedItemFogMode);

            if (TileSystem.Instance != null)
            {
                Debug.Log($"[LootSystem] 아이템 드랍 및 등록 - 이름: {item.ItemData.itemName}, 좌표: {cellPos}");
                TileSystem.Instance.RegisterItemTile(cellPos, lootable);
            }
        }

        /// <summary>
        /// 시작 칸에서 가장 가까운 빈 아이템 칸(BFS). 벽은 통과하지 않고, 대각선은 양옆 직교 칸이 열려 있을 때만 확장해
        /// 벽 모서리 너머 방으로 넘어가지 않는다. avoidOriginCell이면 시작 칸에는 놓지 않는다(상자 칸).
        /// 시작 칸 자체가 막혀 있어도(벽에 붙은 몬스터 위치 등) 주변 탐색은 진행한다.
        /// </summary>
        private Vector3Int GetNearestEmptyItemTile(Vector3Int startCell, bool avoidOriginCell)
        {
            _discardQueue.Clear();
            _discardVisited.Clear();
            _discardQueue.Enqueue(startCell);
            _discardVisited.Add(startCell);

            while (_discardQueue.Count > 0)
            {
                Vector3Int curr = _discardQueue.Dequeue();
                bool isObstacle = IsBlocked(curr);

                bool canPlaceHere = !(avoidOriginCell && curr == startCell);
                if (canPlaceHere && !isObstacle && TileSystem.Instance.GetItemAtTile(curr) == null)
                {
                    return curr;
                }

                if (isObstacle && curr != startCell) continue;

                for (int i = 0; i < DiscardDirs.Length; i++)
                {
                    Vector3Int dir = DiscardDirs[i];
                    Vector3Int next = curr + dir;
                    if (_discardVisited.Contains(next)) continue;

                    bool isDiagonal = dir.x != 0 && dir.y != 0;
                    if (isDiagonal && (IsBlocked(new Vector3Int(curr.x + dir.x, curr.y, 0)) || IsBlocked(new Vector3Int(curr.x, curr.y + dir.y, 0)))) continue;

                    _discardVisited.Add(next);
                    _discardQueue.Enqueue(next);
                }
            }

            return startCell;
        }
    }
}