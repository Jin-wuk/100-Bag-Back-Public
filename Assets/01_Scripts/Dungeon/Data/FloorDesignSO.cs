using UnityEngine;

namespace BagBack100.Dungeon.Data
{
    /// <summary>
    /// 층 디자인이 쓰이는 구간. 1~5 구간은 5층이 보스층 고정이라 실제 추첨은 1~4층이다.
    /// </summary>
    public enum FloorBand
    {
        Early,  // 1~5층 구간
        Late,   // 6~9층 구간
        Boss    // 5층 중간 보스 / 10층 보스
    }

    /// <summary>
    /// Role: 수작업 층 디자인 1장의 정적 데이터 (층 프리팹, 등장 구간, 추첨 가중치).
    /// Dependencies: FloorLayout (층 프리팹 루트)
    /// Events: 없음
    /// </summary>
    [CreateAssetMenu(fileName = "NewFloorDesign", menuName = "BagBack100/Floor/FloorDesign")]
    public class FloorDesignSO : ScriptableObject
    {
        [Tooltip("층 프리팹 (루트에 FloorLayout, Grid는 두지 않는다)")]
        [SerializeField] private FloorLayout _layoutPrefab;
        [Tooltip("이 디자인이 쓰이는 층 구간")]
        [SerializeField] private FloorBand _band = FloorBand.Early;
        [Tooltip("같은 구간 풀 안에서의 추첨 가중치")]
        [SerializeField, Min(1)] private int _weight = 1;

        public FloorLayout LayoutPrefab => _layoutPrefab;
        public FloorBand Band => _band;
        public int Weight => _weight;

#if UNITY_EDITOR
        /// <summary>회색 박스 생성 메뉴 전용 초기값 주입.</summary>
        public void EditorInitialize(FloorLayout layoutPrefab, FloorBand band, int weight)
        {
            _layoutPrefab = layoutPrefab;
            _band = band;
            _weight = Mathf.Max(1, weight);
        }
#endif
    }
}
