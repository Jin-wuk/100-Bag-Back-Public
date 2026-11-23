using UnityEngine;
using System.Collections.Generic;
using BagBack100.Core;
using BagBack100.Data.Models;
using BagBack100.Data.Core;
using BagBack100.Map;
using BagBack100.Dungeon; // [추가] FogOfWar 연동을 위한 네임스페이스
using BagBack100.Dungeon.Data;

namespace BagBack100.Combat
{
    /// <summary>
    /// Role: 몬스터 사망·상자 열림 시 드랍 풀 추첨(층별 등급 비율 포함)과 필드 아이템 스폰, 바닥 드랍 요청 처리. 드랍 풀 데이터 검증 경고.
    /// Dependencies: DropPoolSO, ItemDataSO, RegionConfigSO, PlayerSessionData, TileSystem, Pathfinder, FogVisibilityObject, LootableItem
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
        private Dictionary<int, RegionConfigSO> _regions = new Dictionary<int, RegionConfigSO>();
        // 지역 데이터를 찾지 못할 때 쓰는 기획 기본 비율 (기획 07 5절)
        private static readonly RarityWeights DefaultRarityWeights = new RarityWeights(70f, 25f, 5f);
        private static readonly ItemRarity[] AllRarities = { ItemRarity.Common, ItemRarity.Rare, ItemRarity.Legendary };

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

            foreach (var region in Resources.LoadAll<RegionConfigSO>(""))
            {
                _regions[region.RegionId] = region;
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

                    int amount = RollAmount(fixedDrop.minAmount, fixedDrop.maxAmount);

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

                    if (group.useFloorRarity)
                    {
                        for (int i = 0; i < rolls; i++) RollFloorRarityEntry(group, position, avoidOriginCell);
                        continue;
                    }

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
                                    int amount = RollAmount(entry.minAmount, entry.maxAmount);

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

        /// <summary>
        /// 등급을 층 비율로 먼저 고르고 그 등급 안에서 dropWeight로 고른다. 그룹에 없거나 비율이 0인 등급은 빼고 재정규화한다
        /// (보스 풀처럼 일반 등급이 없는 그룹도 같은 경로를 쓴다).
        /// </summary>
        private void RollFloorRarityEntry(DropGroup group, Vector3 position, bool avoidOriginCell)
        {
            // Random.value는 1.0도 나오므로 확정 드랍(1)은 굴리지 않는다
            if (group.dropChance < 1f && Random.value >= group.dropChance) return;

            RarityWeights weights = GetCurrentRarityWeights();
            float totalRarity = 0f;
            foreach (ItemRarity rarity in AllRarities)
            {
                if (GroupWeightOfRarity(group, rarity, weights) > 0) totalRarity += weights.Get(rarity);
            }
            if (totalRarity <= 0f)
            {
                Debug.LogWarning($"[LootSystem] '{group.groupName}' 그룹에 현재 층 비율로 뽑을 수 있는 항목이 없습니다.");
                return;
            }

            float rarityRoll = Random.value * totalRarity;
            for (int r = 0; r < AllRarities.Length; r++)
            {
                ItemRarity rarity = AllRarities[r];
                int groupWeight = GroupWeightOfRarity(group, rarity, weights);
                if (groupWeight <= 0) continue;

                rarityRoll -= weights.Get(rarity);
                // 부동소수 오차로 끝까지 못 뽑히는 일이 없도록 마지막 유효 등급까지 내려가면 그 등급을 쓴다
                if (rarityRoll >= 0f && HasValidRarityAfter(group, r, weights)) continue;

                int entryRoll = Random.Range(0, groupWeight);
                foreach (var entry in group.entries)
                {
                    if (!IsEntryOfRarity(entry, rarity)) continue;
                    entryRoll -= entry.dropWeight;
                    if (entryRoll >= 0) continue;

                    SpawnLootableItem(entry.itemID, RollAmount(entry.minAmount, entry.maxAmount), position, avoidOriginCell);
                    return;
                }
                return;
            }
        }

        private RarityWeights GetCurrentRarityWeights()
        {
            return _regions.TryGetValue(PlayerSessionData.CurrentRegionId, out var region)
                ? region.GetRarityWeights(PlayerSessionData.CurrentFloor)
                : DefaultRarityWeights;
        }

        private int GroupWeightOfRarity(DropGroup group, ItemRarity rarity, RarityWeights weights)
        {
            if (weights.Get(rarity) <= 0f) return 0;
            int total = 0;
            foreach (var entry in group.entries)
            {
                if (IsEntryOfRarity(entry, rarity)) total += entry.dropWeight;
            }
            return total;
        }

        private bool HasValidRarityAfter(DropGroup group, int rarityIndex, RarityWeights weights)
        {
            for (int i = rarityIndex + 1; i < AllRarities.Length; i++)
            {
                if (GroupWeightOfRarity(group, AllRarities[i], weights) > 0) return true;
            }
            return false;
        }

        private bool IsEntryOfRarity(DropEntry entry, ItemRarity rarity)
        {
            return entry.itemID > 0 && entry.dropWeight > 0
                && _itemDataCache.TryGetValue(entry.itemID, out var data) && data.rarity == rarity;
        }

        private static int RollAmount(int min, int max)
        {
            int minAmt = min > 0 ? min : 1;
            int maxAmt = max >= minAmt ? max : minAmt;
            return Random.Range(minAmt, maxAmt + 1);
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

                // 드랍 금지 칸(탈출구·계단 주변)은 놓지 않되 BFS 확장은 계속해 그 너머 빈 칸을 찾는다
                if (curr != origin && !TileSystem.Instance.IsNoDropCell(curr) && TileSystem.Instance.GetItemAtTile(curr) == null && TileSystem.Instance.GetEntityAtTile(curr) == null)
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

        // 몬스터 몸 칸에 떨어진 아이템은 밟을 수 없어 줍지 못한다(고정형 보스면 영원히). 플레이어 칸은 기존처럼 허용
        private static bool IsMonsterCell(Vector3Int cell)
        {
            GameObject occupant = TileSystem.Instance.GetEntityAtTile(cell);
            return occupant != null && occupant.TryGetComponent(out BagBack100.Monster.MonsterController _);
        }

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

                bool canPlaceHere = !(avoidOriginCell && curr == startCell) && !TileSystem.Instance.IsNoDropCell(curr);
                if (canPlaceHere && !isObstacle && TileSystem.Instance.GetItemAtTile(curr) == null && !IsMonsterCell(curr))
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