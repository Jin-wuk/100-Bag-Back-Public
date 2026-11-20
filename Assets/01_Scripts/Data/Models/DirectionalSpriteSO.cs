using UnityEngine;

namespace BagBack100.Data.Models
{
    /// <summary>
    /// Role: 몬스터 1종의 4방향(S/W/E/N) 정지 스프라이트 정적 데이터. 방향 벡터를 4방향으로 바꾸는 헬퍼를 함께 둔다(플레이어와 같은 규칙: 대각선은 좌우 우선).
    /// Dependencies: FacingDir4 (SpriteAnimSetSO.cs)
    /// Events: 없음
    /// </summary>
    [CreateAssetMenu(fileName = "NewDirectionalSprite", menuName = "BagBack100/Data/DirectionalSprite")]
    public class DirectionalSpriteSO : ScriptableObject
    {
        [SerializeField] private Sprite _south;
        [SerializeField] private Sprite _west;
        [SerializeField] private Sprite _east;
        [SerializeField] private Sprite _north;

        // 빈 방향은 남쪽 이미지로 대신한다
        public Sprite Get(FacingDir4 dir)
        {
            Sprite sprite;
            switch (dir)
            {
                case FacingDir4.West: sprite = _west; break;
                case FacingDir4.East: sprite = _east; break;
                case FacingDir4.North: sprite = _north; break;
                default: sprite = _south; break;
            }
            return sprite != null ? sprite : _south;
        }

        // 거의 0인 벡터는 fallback. 대각선은 좌우 방향을 우선한다
        public static FacingDir4 FromVector(Vector3 v, FacingDir4 fallback)
        {
            float ax = Mathf.Abs(v.x);
            float ay = Mathf.Abs(v.y);
            if (ax < 0.001f && ay < 0.001f) return fallback;
            if (ax >= ay) return v.x > 0f ? FacingDir4.East : FacingDir4.West;
            return v.y > 0f ? FacingDir4.North : FacingDir4.South;
        }

        private void OnValidate()
        {
            if (_south == null || _west == null || _east == null || _north == null)
            {
                Debug.LogWarning($"[DirectionalSpriteSO] {name}: 비어 있는 방향 스프라이트가 있습니다(남쪽 이미지로 대신 표시).", this);
            }
        }
    }
}
