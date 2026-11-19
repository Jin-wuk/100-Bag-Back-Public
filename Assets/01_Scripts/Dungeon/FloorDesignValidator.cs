using System.Collections.Generic;
using UnityEngine;
using BagBack100.Dungeon.Data;

namespace BagBack100.Dungeon
{
    public enum StairsRequirement
    {
        Required,   // 일반층·5층 중간 보스층: 정확히 1개
        Optional    // 10층 보스층: 0~1개 (있어도 런타임에서 무시)
    }

    /// <summary>
    /// Role: 층 프리팹 규격 검증 (마커 개수·위치, 계단–탈출구 거리, 상자 후보 수, 상자로 막혀도 계단·탈출구 도달 가능, 보스 마커 위치, 지역 풀 구성·보스 프리팹, 층별 출현 몬스터).
    ///       에디터 메뉴와 런타임 생성기가 함께 쓰므로 UnityEditor API를 쓰지 않는다.
    /// Dependencies: FloorLayout, FloorMarker, RegionConfigSO, FloorDesignSO
    /// Events: 없음
    /// </summary>
    public static class FloorDesignValidator
    {
        /// <summary>계단과 탈출구 사이 최소 체비쇼프 거리. 반경 1칸(3x3) 드랍 제외 영역이 서로 겹치지 않는 거리.</summary>
        public const int MinStairsExitDistance = 3;
        /// <summary>보스 마커는 시작점에서 이 걸음 수보다 멀어야 한다. 런타임 SpawnBoss도 같은 값으로 판정한다.</summary>
        public const int StartSafeSteps = 5;

        private static readonly List<FloorMarker> MarkerBuffer = new List<FloorMarker>();

        public static bool ValidateLayout(
            FloorLayout layout, StairsRequirement stairs, int requiredChestCandidates, List<string> errors, bool isBossFloor = false)
        {
            int errorCountBefore = errors.Count;
            if (layout == null)
            {
                errors.Add("층 프리팹이 없습니다.");
                return false;
            }

            string name = layout.name;
            // 마커 칸 계산(GetLocalCell)이 셀 크기 1을 가정하므로 런타임 부모 Grid도 같은지 확인한다
            Grid parentGrid = layout.transform.parent != null ? layout.transform.parent.GetComponentInParent<Grid>() : null;
            if (parentGrid != null && (!Mathf.Approximately(parentGrid.cellSize.x, 1f) || !Mathf.Approximately(parentGrid.cellSize.y, 1f)))
            {
                errors.Add($"[{name}] 부모 Grid 셀 크기 {parentGrid.cellSize}가 (1, 1)이 아닙니다.");
            }
            if (layout.FloorTilemap == null) errors.Add($"[{name}] Floor 타일맵 참조가 없습니다.");
            if (layout.WallTilemap == null) errors.Add($"[{name}] Wall 타일맵 참조가 없습니다.");
            CheckIdentityTransform(layout, layout.FloorTilemap != null ? layout.FloorTilemap.transform : null, name, errors);
            CheckIdentityTransform(layout, layout.WallTilemap != null ? layout.WallTilemap.transform : null, name, errors);

            CheckMarkerCount(layout, FloorMarkerType.PlayerStart, 1, 1, name, errors);
            CheckMarkerCount(layout, FloorMarkerType.Exit, 1, 1, name, errors);
            CheckMarkerCount(layout, FloorMarkerType.UpStairs, stairs == StairsRequirement.Required ? 1 : 0, 1, name, errors);
            int bossMarkers = isBossFloor ? 1 : 0;
            CheckMarkerCount(layout, FloorMarkerType.BossSpawn, bossMarkers, bossMarkers, name, errors);

            layout.CollectMarkers(FloorMarkerType.ChestCandidate, MarkerBuffer);
            if (MarkerBuffer.Count < requiredChestCandidates)
            {
                errors.Add($"[{name}] 상자 후보 {MarkerBuffer.Count}곳 < 필요한 굴림 횟수 {requiredChestCandidates}.");
            }

            var usedCells = new HashSet<Vector3Int>();
            FloorMarker[] markers = layout.Markers;
            for (int i = 0; i < markers.Length; i++)
            {
                Vector3Int cell = layout.GetLocalCell(markers[i]);
                if (!layout.IsLocalCellWalkable(cell))
                {
                    errors.Add($"[{name}] {markers[i].Type} 마커({cell.x},{cell.y})가 걸을 수 있는 칸이 아닙니다.");
                }
                if (!usedCells.Add(cell))
                {
                    errors.Add($"[{name}] 칸({cell.x},{cell.y})에 마커가 2개 이상 있습니다.");
                }
            }

            FloorMarker stairsMarker = layout.GetSingleMarker(FloorMarkerType.UpStairs);
            FloorMarker exitMarker = layout.GetSingleMarker(FloorMarkerType.Exit);
            if (stairsMarker != null && exitMarker != null)
            {
                int distance = ChebyshevDistance(layout.GetLocalCell(stairsMarker), layout.GetLocalCell(exitMarker));
                if (distance < MinStairsExitDistance)
                {
                    errors.Add($"[{name}] 계단–탈출구 거리 {distance}칸 < 최소 {MinStairsExitDistance}칸.");
                }
            }

            FloorMarker bossMarker = isBossFloor ? layout.GetSingleMarker(FloorMarkerType.BossSpawn) : null;
            if (bossMarker != null)
            {
                Vector3Int bossCell = layout.GetLocalCell(bossMarker);
                if (stairs == StairsRequirement.Required) CheckBossMarkerDistance(layout, bossCell, stairsMarker, name, errors);
                CheckBossMarkerDistance(layout, bossCell, exitMarker, name, errors);
            }

            CheckReachableWithChestsBlocked(layout, name, errors, bossMarker);
            return errors.Count == errorCountBefore;
        }

        // 계단·탈출구 주변 스폰 금지 반경과 겹치면 런타임에 보스가 놓이지 않는다
        private static void CheckBossMarkerDistance(FloorLayout layout, Vector3Int bossCell, FloorMarker other, string name, List<string> errors)
        {
            if (other == null) return;
            int distance = ChebyshevDistance(bossCell, layout.GetLocalCell(other));
            if (distance < MinStairsExitDistance)
            {
                errors.Add($"[{name}] 보스 마커–{other.Type} 거리 {distance}칸 < 최소 {MinStairsExitDistance}칸.");
            }
        }

        private static readonly Vector3Int[] CardinalDirs = { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left };
        // 층 생성마다 런타임에서도 호출되므로 탐색 버퍼를 재사용한다
        private static readonly HashSet<Vector3Int> BlockedBuffer = new HashSet<Vector3Int>();
        private static readonly HashSet<Vector3Int> VisitedBuffer = new HashSet<Vector3Int>();
        private static readonly Queue<Vector3Int> QueueBuffer = new Queue<Vector3Int>();
        private static readonly Dictionary<Vector3Int, int> StepsBuffer = new Dictionary<Vector3Int, int>();

        /// <summary>
        /// 상자는 열린 뒤에도 칸을 막으므로, 상자 후보 칸이 전부 막힌 최악의 경우에도
        /// 시작점에서 위층 계단·탈출구와 나머지 모든 바닥 칸에 걸어갈 수 있어야 한다
        /// (1칸 통로에 상자 후보를 두면 길이 끊기거나, 고립 칸에 몬스터·드랍이 생겨 클리어·습득이 막힌다).
        /// </summary>
        private static void CheckReachableWithChestsBlocked(FloorLayout layout, string name, List<string> errors, FloorMarker bossMarker)
        {
            FloorMarker start = layout.GetSingleMarker(FloorMarkerType.PlayerStart);
            if (start == null) return;

            HashSet<Vector3Int> blocked = BlockedBuffer;
            HashSet<Vector3Int> visited = VisitedBuffer;
            Queue<Vector3Int> queue = QueueBuffer;
            blocked.Clear();
            visited.Clear();
            queue.Clear();
            StepsBuffer.Clear();

            layout.CollectMarkers(FloorMarkerType.ChestCandidate, MarkerBuffer);
            for (int i = 0; i < MarkerBuffer.Count; i++) blocked.Add(layout.GetLocalCell(MarkerBuffer[i]));

            Vector3Int startCell = layout.GetLocalCell(start);
            visited.Add(startCell);
            StepsBuffer[startCell] = 0;
            queue.Enqueue(startCell);
            while (queue.Count > 0)
            {
                Vector3Int curr = queue.Dequeue();
                for (int i = 0; i < CardinalDirs.Length; i++)
                {
                    Vector3Int next = curr + CardinalDirs[i];
                    if (visited.Contains(next) || blocked.Contains(next) || !layout.IsLocalCellWalkable(next)) continue;
                    visited.Add(next);
                    StepsBuffer[next] = StepsBuffer[curr] + 1;
                    queue.Enqueue(next);
                }
            }

            CheckMarkerReached(layout, FloorMarkerType.UpStairs, visited, name, errors);
            CheckMarkerReached(layout, FloorMarkerType.Exit, visited, name, errors);
            if (bossMarker != null)
            {
                Vector3Int bossCell = layout.GetLocalCell(bossMarker);
                if (!StepsBuffer.TryGetValue(bossCell, out int bossSteps))
                {
                    errors.Add($"[{name}] 시작점에서 보스 마커({bossCell.x},{bossCell.y})에 갈 수 없습니다.");
                }
                else if (bossSteps <= StartSafeSteps)
                {
                    errors.Add($"[{name}] 보스 마커가 시작점에서 {bossSteps}걸음 (필요: {StartSafeSteps}걸음 초과).");
                }
            }

            if (layout.FloorTilemap == null) return;
            int isolated = 0;
            Vector3Int firstIsolated = default;
            foreach (Vector3Int cell in layout.FloorTilemap.cellBounds.allPositionsWithin)
            {
                Vector3Int c = new Vector3Int(cell.x, cell.y, 0);
                if (blocked.Contains(c) || visited.Contains(c) || !layout.IsLocalCellWalkable(c)) continue;
                if (isolated == 0) firstIsolated = c;
                isolated++;
            }
            if (isolated > 0)
            {
                errors.Add($"[{name}] 상자 후보 칸이 모두 막히면 시작점에서 갈 수 없는 바닥 칸 {isolated}개 (예: {firstIsolated.x},{firstIsolated.y}).");
            }
        }

        private static void CheckMarkerReached(FloorLayout layout, FloorMarkerType type, HashSet<Vector3Int> reached, string name, List<string> errors)
        {
            FloorMarker marker = layout.GetSingleMarker(type);
            if (marker == null) return;
            Vector3Int cell = layout.GetLocalCell(marker);
            if (!reached.Contains(cell))
            {
                errors.Add($"[{name}] 상자 후보 칸이 모두 막히면 시작점에서 {type}({cell.x},{cell.y})에 갈 수 없습니다 (상자는 열린 뒤에도 길을 막음).");
            }
        }

        public static bool ValidateDesign(FloorDesignSO design, FloorBand expectedBand, StairsRequirement stairs,
            RegionConfigSO region, List<string> errors)
        {
            if (design == null)
            {
                errors.Add($"[{region.name}] {expectedBand} 디자인 슬롯이 비어 있습니다.");
                return false;
            }

            int errorCountBefore = errors.Count;
            if (design.Band != expectedBand)
            {
                errors.Add($"[{design.name}] 구간 {design.Band}가 들어간 풀({expectedBand})과 다릅니다.");
            }
            ValidateLayout(design.LayoutPrefab, stairs, region.GetMaxChestRolls(expectedBand), errors, expectedBand == FloorBand.Boss);
            return errors.Count == errorCountBefore;
        }

        /// <summary>
        /// 지역 전체 검증. 프리팹 에셋을 직접 읽으므로 에디터에서 호출해도 프리뷰 씬을 만들지 않는다.
        /// 풀 장수는 한 판 중복 금지(Q13)를 채울 수 있는지 본다: 고유 프리팹 수가 구간 층 수보다 적으면 오류,
        /// 같으면 경고(추천 6장 이상). 디버그 중복 허용이 켜져 있으면 부족해도 경고로 낮춘다.
        /// </summary>
        public static bool ValidateRegion(RegionConfigSO region, List<string> errors, List<string> warnings)
        {
            int errorCountBefore = errors.Count;
            if (region == null)
            {
                errors.Add("RegionConfigSO가 없습니다.");
                return false;
            }

            ValidatePool(region.EarlyPool, FloorBand.Early, region, errors);
            ValidatePool(region.LatePool, FloorBand.Late, region, errors);
            ValidatePoolSize(region.EarlyPool, FloorBand.Early, RegionConfigSO.MidBossFloor - 1, region, errors, warnings);
            ValidatePoolSize(region.LatePool, FloorBand.Late, RegionConfigSO.RuleFloorCount - RegionConfigSO.MidBossFloor, region, errors, warnings);
            CheckSharedLayouts(region, errors, warnings);
            ValidateDesign(region.MidBossFloorDesign, FloorBand.Boss, StairsRequirement.Required, region, errors);
            ValidateDesign(region.BossFloorDesign, FloorBand.Boss, StairsRequirement.Optional, region, errors);
            CheckBossPrefab(region.MidBossPrefab, RegionConfigSO.MidBossFloor, region, warnings);
            CheckBossPrefab(region.BossPrefab, RegionConfigSO.BossFloor, region, warnings);
            CheckMonsterPool(region, errors, warnings);
            return errors.Count == errorCountBefore;
        }

        /// <summary>
        /// 몬스터 출현 층 범위 검증. 지역 몬스터 풀은 1~10층마다 출현 가능한 몬스터가 1종 이상이어야 한다(없으면 오류).
        /// 구역 전용 풀은 그 디자인이 쓰이는 층에서 0종이면 경고 (런타임은 범위를 무시하고 스폰하는 폴백이 있다).
        /// 특정 몬스터를 가정하지 않고 풀에 연결된 프리팹의 MonsterDataSO만 읽는다.
        /// </summary>
        private static void CheckMonsterPool(RegionConfigSO region, List<string> errors, List<string> warnings)
        {
            IReadOnlyList<GameObject> pool = region.MonsterPool;
            if (pool == null || pool.Count == 0)
            {
                errors.Add($"[{region.name}] 몬스터 풀이 비어 있습니다.");
            }
            else
            {
                CheckInvalidPrefabs(pool, $"[{region.name}] 몬스터 풀", warnings);
                for (int floor = 1; floor <= RegionConfigSO.BossFloor; floor++)
                {
                    if (CountMonstersForFloor(pool, floor) == 0)
                    {
                        errors.Add($"[{region.name}] 몬스터 풀에 {floor}층 출현 범위에 맞는 몬스터가 없습니다.");
                    }
                }
            }

            CheckZonePools(region.EarlyPool, 1, RegionConfigSO.MidBossFloor - 1, region, warnings);
            CheckZonePools(region.LatePool, RegionConfigSO.MidBossFloor + 1, RegionConfigSO.RuleFloorCount, region, warnings);
            CheckZonePools(region.MidBossFloorDesign, RegionConfigSO.MidBossFloor, RegionConfigSO.MidBossFloor, region, warnings);
            CheckZonePools(region.BossFloorDesign, RegionConfigSO.BossFloor, RegionConfigSO.BossFloor, region, warnings);
        }

        private static void CheckZonePools(IReadOnlyList<FloorDesignSO> designs, int fromFloor, int toFloor, RegionConfigSO region, List<string> warnings)
        {
            if (designs == null) return;
            for (int i = 0; i < designs.Count; i++)
            {
                CheckZonePools(designs[i], fromFloor, toFloor, region, warnings);
            }
        }

        private static void CheckZonePools(FloorDesignSO design, int fromFloor, int toFloor, RegionConfigSO region, List<string> warnings)
        {
            if (design == null || design.LayoutPrefab == null) return;
            DungeonZone[] zones = design.LayoutPrefab.Zones;
            if (zones == null) return;
            for (int z = 0; z < zones.Length; z++)
            {
                DungeonZone zone = zones[z];
                if (zone == null || zone.OverrideMonsterPool == null || zone.OverrideMonsterPool.Count == 0) continue;
                CheckInvalidPrefabs(zone.OverrideMonsterPool, $"[{region.name}] {design.name}의 구역 {zone.name} 전용 풀", warnings);
                for (int floor = fromFloor; floor <= toFloor; floor++)
                {
                    if (CountMonstersForFloor(zone.OverrideMonsterPool, floor) > 0) continue;
                    warnings.Add($"[{region.name}] {design.name}의 구역 {zone.name} 전용 풀에 {floor}층 출현 범위에 맞는 몬스터가 없습니다. 범위를 무시하고 스폰합니다.");
                }
            }
        }

        // 빈 칸이나 MonsterController·데이터가 없는 프리팹은 런타임에서 건너뛰므로, 다른 몬스터가 층을 메워도 연결 실수를 알린다
        private static void CheckInvalidPrefabs(IReadOnlyList<GameObject> pool, string poolLabel, List<string> warnings)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                GameObject prefab = pool[i];
                if (prefab == null)
                {
                    warnings.Add($"{poolLabel} {i}번 칸이 비어 있습니다.");
                }
                else if (!prefab.TryGetComponent(out BagBack100.Monster.MonsterController monster) || monster.Data == null)
                {
                    warnings.Add($"{poolLabel}의 {prefab.name}에 MonsterController 또는 몬스터 데이터가 없어 스폰에서 제외됩니다.");
                }
            }
        }

        private static int CountMonstersForFloor(IReadOnlyList<GameObject> pool, int floor)
        {
            int count = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                GameObject prefab = pool[i];
                if (prefab == null) continue;
                if (!prefab.TryGetComponent(out BagBack100.Monster.MonsterController monster) || monster.Data == null) continue;
                if (monster.Data.CanAppearOnFloor(floor)) count++;
            }
            return count;
        }

        // 보스가 없어도 층은 전원 처치 규칙으로 진행되므로 오류가 아니라 경고로 둔다
        private static void CheckBossPrefab(GameObject prefab, int floor, RegionConfigSO region, List<string> warnings)
        {
            if (prefab == null)
            {
                warnings.Add($"[{region.name}] {floor}층 보스 프리팹이 없습니다. 전원 처치 규칙으로 동작합니다.");
            }
            else if (!prefab.TryGetComponent(out BagBack100.Monster.MonsterController monster) || !monster.IsBoss)
            {
                warnings.Add($"[{region.name}] {floor}층 보스 프리팹 {prefab.name}의 데이터가 Boss 등급이 아닙니다. 전원 처치 규칙으로 동작합니다.");
            }
        }

        private static void ValidatePool(IReadOnlyList<FloorDesignSO> pool, FloorBand band, RegionConfigSO region, List<string> errors)
        {
            if (pool == null || pool.Count == 0)
            {
                errors.Add($"[{region.name}] {band} 풀이 비어 있습니다.");
                return;
            }
            for (int i = 0; i < pool.Count; i++)
            {
                ValidateDesign(pool[i], band, StairsRequirement.Required, region, errors);
            }
        }

        public const int RecommendedPoolSize = 6;

        private static void ValidatePoolSize(IReadOnlyList<FloorDesignSO> pool, FloorBand band, int floorCount,
            RegionConfigSO region, List<string> errors, List<string> warnings)
        {
            int unique = CountUniqueLayouts(pool);
            if (unique < floorCount)
            {
                string message = $"[{region.name}] {band} 풀 고유 디자인 {unique}장 < 구간 층 수 {floorCount}장 (한 판 중복 금지를 채울 수 없음).";
                if (region.DebugAllowDuplicateDesigns) warnings.Add(message + " 디버그 중복 허용이 켜져 있어 경고로 처리.");
                else errors.Add(message);
            }
            else if (unique == floorCount)
            {
                warnings.Add($"[{region.name}] {band} 풀 고유 디자인 {unique}장 = 구간 층 수. 매 판 같은 구성이 됩니다 (추천 {RecommendedPoolSize}장 이상).");
            }
        }

        // Early와 Late가 같은 프리팹을 쓰면 한 판 중복 금지 때문에 1~4층이 그 프리팹을 먼저 써 버려
        // 6~9층 후보가 모자랄 수 있다(런타임 후보 0장). 두 구간은 서로 다른 프리팹으로 채워야 한다.
        private static void CheckSharedLayouts(RegionConfigSO region, List<string> errors, List<string> warnings)
        {
            var early = new HashSet<FloorLayout>();
            AddLayouts(region.EarlyPool, early);
            var late = new HashSet<FloorLayout>();
            AddLayouts(region.LatePool, late);
            foreach (FloorLayout layout in late)
            {
                if (!early.Contains(layout)) continue;
                string message = $"[{region.name}] {layout.name}이 1~5 풀과 6~9 풀에 모두 있습니다 (6~9층 후보가 모자랄 수 있음).";
                if (region.DebugAllowDuplicateDesigns) warnings.Add(message + " 디버그 중복 허용이 켜져 있어 경고로 처리.");
                else errors.Add(message);
            }
        }

        private static int CountUniqueLayouts(IReadOnlyList<FloorDesignSO> pool)
        {
            var set = new HashSet<FloorLayout>();
            AddLayouts(pool, set);
            return set.Count;
        }

        private static void AddLayouts(IReadOnlyList<FloorDesignSO> pool, HashSet<FloorLayout> set)
        {
            if (pool == null) return;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && pool[i].LayoutPrefab != null) set.Add(pool[i].LayoutPrefab);
            }
        }

        private static void CheckMarkerCount(FloorLayout layout, FloorMarkerType type, int min, int max, string name, List<string> errors)
        {
            layout.CollectMarkers(type, MarkerBuffer);
            int count = MarkerBuffer.Count;
            if (count < min || count > max)
            {
                string expected = min == max ? $"{min}개" : $"{min}~{max}개";
                errors.Add($"[{name}] {type} 마커 {count}개 (필요: {expected}).");
            }
        }

        private static void CheckIdentityTransform(FloorLayout layout, Transform child, string name, List<string> errors)
        {
            if (child == null) return;
            Vector3 localPos = layout.transform.InverseTransformPoint(child.position);
            bool rotated = Quaternion.Angle(child.rotation, layout.transform.rotation) > 0.01f;
            if (localPos.sqrMagnitude > 0.0001f || child.lossyScale != layout.transform.lossyScale || rotated)
            {
                errors.Add($"[{name}] {child.name} 타일맵은 루트 기준 위치 0, 회전 0, 스케일 1이어야 합니다.");
            }
        }

        public static int ChebyshevDistance(Vector3Int a, Vector3Int b)
        {
            return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }
    }
}
