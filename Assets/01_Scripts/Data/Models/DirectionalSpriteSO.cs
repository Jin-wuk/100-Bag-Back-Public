using UnityEngine;

namespace BagBack100.Data.Models
{
    // 순서는 남쪽부터 시계 방향. DirectionalSpriteSO의 배열 칸 순서와 같다
    public enum FacingDir8
    {
        S,
        SW,
        W,
        NW,
        N,
        NE,
        E,
        SE
    }

    /// <summary>
    /// Role: 몬스터 1종의 8방향 정지 스프라이트 정적 데이터. 방향 벡터를 8방향으로 바꾸는 헬퍼를 함께 둔다.
    /// Dependencies: 없음
    /// Events: 없음
    /// </summary>
    [CreateAssetMenu(fileName = "NewDirectionalSprite", menuName = "BagBack100/Data/DirectionalSprite")]
    public class DirectionalSpriteSO : ScriptableObject
    {
        public const int DirectionCount = 8;

        [Tooltip("S, SW, W, NW, N, NE, E, SE 순서")]
        [SerializeField] private Sprite[] _sprites = new Sprite[DirectionCount];

        // 빈 방향은 남쪽 이미지로 대신한다
        public Sprite Get(FacingDir8 dir)
        {
            if (_sprites == null || _sprites.Length == 0) return null;
            int index = (int)dir;
            Sprite sprite = index < _sprites.Length ? _sprites[index] : null;
            return sprite != null ? sprite : _sprites[0];
        }

        // 화면 기준 방향 벡터를 45° 단위로 맞춘다. 거의 0이면 fallback
        public static FacingDir8 FromVector(Vector3 v, FacingDir8 fallback)
        {
            if (v.x * v.x + v.y * v.y < 0.000001f) return fallback;
            // atan2 기준 0° = 동쪽, 반시계. 남쪽(-90°)을 0번으로 두고 시계 방향으로 센다
            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            int step = Mathf.RoundToInt((-90f - angle) / 45f);
            step = ((step % DirectionCount) + DirectionCount) % DirectionCount;
            return (FacingDir8)step;
        }

        private void OnValidate()
        {
            if (_sprites == null || _sprites.Length != DirectionCount)
            {
                Debug.LogWarning($"[DirectionalSpriteSO] {name}: 스프라이트 칸이 {DirectionCount}개가 아닙니다.", this);
                return;
            }
            for (int i = 0; i < DirectionCount; i++)
            {
                if (_sprites[i] == null) Debug.LogWarning($"[DirectionalSpriteSO] {name}: {(FacingDir8)i} 스프라이트가 비어 있습니다.", this);
            }
        }
    }
}
