using System;
using UnityEngine;

namespace BagBack100.Data.Models
{
    public enum FacingDir4
    {
        South,
        West,
        East,
        North
    }

    [Serializable]
    public class DirectionalFrames
    {
        [SerializeField] private Sprite[] _south;
        [SerializeField] private Sprite[] _west;
        [SerializeField] private Sprite[] _east;
        [SerializeField] private Sprite[] _north;

        public Sprite[] Get(FacingDir4 dir)
        {
            switch (dir)
            {
                case FacingDir4.West: return _west;
                case FacingDir4.East: return _east;
                case FacingDir4.North: return _north;
                default: return _south;
            }
        }

        public void CollectWarnings(string label, UnityEngine.Object context)
        {
            for (int i = 0; i < 4; i++)
            {
                FacingDir4 dir = (FacingDir4)i;
                Sprite[] frames = Get(dir);
                if (frames == null || frames.Length == 0)
                {
                    Debug.LogWarning($"[SpriteAnimSetSO] {context.name}: {label} {dir} 프레임이 비어 있습니다.", context);
                    continue;
                }
                for (int f = 0; f < frames.Length; f++)
                {
                    if (frames[f] == null) Debug.LogWarning($"[SpriteAnimSetSO] {context.name}: {label} {dir} {f}번 프레임이 비어 있습니다.", context);
                }
            }
        }
    }

    /// <summary>
    /// Role: 캐릭터 1종의 4방향 스프라이트 애니메이션 정적 데이터 (이동·공격 프레임과 재생 속도).
    /// Dependencies: 없음
    /// Events: 없음
    /// </summary>
    [CreateAssetMenu(fileName = "NewSpriteAnimSet", menuName = "BagBack100/Data/SpriteAnimSet")]
    public class SpriteAnimSetSO : ScriptableObject
    {
        [Tooltip("이동 프레임. 서 있을 때는 0번 프레임을 쓴다")]
        [SerializeField] private DirectionalFrames _move = new DirectionalFrames();
        [SerializeField] private DirectionalFrames _attack = new DirectionalFrames();
        [Tooltip("기본 이동 속도일 때 초당 프레임 수. 이동 속도가 빨라지면 같은 비율로 빨라진다")]
        [SerializeField, Min(0.1f)] private float _moveFps = 8f;
        [Tooltip("공격 프레임 초당 프레임 수. 1회 재생 후 마지막 프레임을 유지한다")]
        [SerializeField, Min(0.1f)] private float _attackFps = 12f;
        [Tooltip("대시 중 고정할 이동 프레임 번호")]
        [SerializeField, Min(0)] private int _dashFrame = 1;

        public DirectionalFrames Move => _move;
        public DirectionalFrames Attack => _attack;
        public float MoveFps => _moveFps;
        public float AttackFps => _attackFps;
        public int DashFrame => _dashFrame;

        private void OnValidate()
        {
            _move?.CollectWarnings("Move", this);
            _attack?.CollectWarnings("Attack", this);
        }
    }
}
