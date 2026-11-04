using UnityEngine;

namespace BagBack100.Dungeon
{
    public enum FloorMarkerType
    {
        PlayerStart,    // 층 진입 시 플레이어 위치
        UpStairs,       // 위층 계단 (10층 보스층은 무시)
        Exit,           // 탈출 포탈 위치 (클리어 시 확률로 활성화)
        ChestCandidate, // 상자 후보 위치 (B안 굴림 당첨 시 후보 중 1곳)
        BossSpawn       // 보스층(5·10층) 보스 위치. 프리팹에 int로 저장되므로 뒤에만 추가
    }

    /// <summary>
    /// Role: 층 프리팹 안에서 특정 칸의 역할을 표시하는 마커. 마커는 칸 중앙(+0.5)에 둔다.
    /// Dependencies: FloorLayout (칸 좌표 기준 루트)
    /// Events: 없음
    /// </summary>
    public class FloorMarker : MonoBehaviour
    {
        [SerializeField] private FloorMarkerType _type = FloorMarkerType.PlayerStart;

        public FloorMarkerType Type => _type;

#if UNITY_EDITOR
        public void EditorSetType(FloorMarkerType type) => _type = type;

        private void OnDrawGizmos()
        {
            Gizmos.color = GetColor(_type);
            Gizmos.DrawCube(transform.position, new Vector3(0.8f, 0.8f, 0.1f));
            UnityEditor.Handles.Label(transform.position + new Vector3(-0.4f, 0.7f, 0f), _type.ToString());
        }

        private static Color GetColor(FloorMarkerType type)
        {
            switch (type)
            {
                case FloorMarkerType.PlayerStart:    return new Color(0.2f, 1f, 0.2f, 0.7f);
                case FloorMarkerType.UpStairs:       return new Color(0.7f, 0.4f, 1f, 0.7f);
                case FloorMarkerType.Exit:           return new Color(0.2f, 0.9f, 1f, 0.7f);
                case FloorMarkerType.ChestCandidate: return new Color(1f, 0.85f, 0.1f, 0.7f);
                default:                             return Color.white;
            }
        }
#endif
    }
}
