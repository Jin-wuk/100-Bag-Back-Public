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
    /// Role: 층 프리팹 규격 검증 (마커 개수·위치, 계단–탈출구 거리, 상자 후보 수, 지역 풀 구성).
    ///       에디터 메뉴와 런타임 생성기가 함께 쓰므로 UnityEditor API를 쓰지 않는다.
    /// Dependencies: FloorLayout, FloorMarker, RegionConfigSO, FloorDesignSO
    /// Events: 없음
    /// </summary>
    public static class FloorDesignValidator
    {
        /// <summary>계단과 탈출구 사이 최소 체비쇼프 거리. 반경 1칸(3x3) 드랍 제외 영역이 서로 겹치지 않는 거리.</summary>
        public const int MinStairsExitDistance = 3;

        private static readonly List<FloorMarker> MarkerBuffer = new List<FloorMarker>();

        public static bool ValidateLayout(
            FloorLayout layout, StairsRequirement stairs, int requiredChestCandidates, List<string> errors)
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

            return errors.Count == errorCountBefore;
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
            ValidateLayout(design.LayoutPrefab, stairs, region.GetMaxChestRolls(expectedBand), errors);
            return errors.Count == errorCountBefore;
        }

        /// <summary>
        /// 지역 전체 검증. 프리팹 에셋을 직접 읽으므로 에디터에서 호출해도 프리뷰 씬을 만들지 않는다.
        /// 풀 장수(한 판 중복 금지) 검증은 T3에서 추가한다.
        /// </summary>
        public static bool ValidateRegion(RegionConfigSO region, List<string> errors)
        {
            int errorCountBefore = errors.Count;
            if (region == null)
            {
                errors.Add("RegionConfigSO가 없습니다.");
                return false;
            }

            ValidatePool(region.EarlyPool, FloorBand.Early, region, errors);
            ValidatePool(region.LatePool, FloorBand.Late, region, errors);
            ValidateDesign(region.MidBossFloorDesign, FloorBand.Boss, StairsRequirement.Required, region, errors);
            ValidateDesign(region.BossFloorDesign, FloorBand.Boss, StairsRequirement.Optional, region, errors);
            return errors.Count == errorCountBefore;
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
