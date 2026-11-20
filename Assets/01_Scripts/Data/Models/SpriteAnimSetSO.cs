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
    /// Role: 캐릭터 1종의 4방향 스프라이트 애니메이션 정적 데이터 (대기·이동·공격 프레임과 재생 속도).
    /// Dependencies: 없음
    /// Events: 없음
    /// </summary>
    [CreateAssetMenu(fileName = "NewSpriteAnimSet", menuName = "BagBack100/Data/SpriteAnimSet")]
    public class SpriteAnimSetSO : ScriptableObject
    {
        [Tooltip("대기 프레임. 방향이 비어 있으면 그 방향은 이동 0번 프레임으로 서 있는다")]
        [SerializeField] private DirectionalFrames _idle = new DirectionalFrames();
        [Tooltip("이동 프레임(첫 걸음 = 왼발). 한 칸을 건너는 동안 한 번 재생한다(도약→착지). 서 있는 프레임이 없을 때는 0번 프레임으로 선다")]
        [SerializeField] private DirectionalFrames _move = new DirectionalFrames();
        [Tooltip("같은 방향으로 이어 걸을 때 번갈아 쓰는 오른발 프레임. 방향이 비어 있으면 그 방향은 _move만 쓴다")]
        [SerializeField] private DirectionalFrames _moveAlt = new DirectionalFrames();
        [SerializeField] private DirectionalFrames _attack = new DirectionalFrames();
        [SerializeField, Min(0.1f)] private float _idleFps = 4f;
        [Tooltip("공격 프레임 초당 프레임 수. 1회 재생 후 마지막 프레임을 유지한다")]
        [SerializeField, Min(0.1f)] private float _attackFps = 12f;
        [Tooltip("이 시간(초) 안에 같은 방향으로 다음 칸을 걸으면 이어 걷기로 보고 발을 바꾼다")]
        [SerializeField, Min(0f)] private float _stepChainGap = 0.1f;
        [Tooltip("대시 중 고정할 이동 프레임 번호")]
        [SerializeField, Min(0)] private int _dashFrame = 1;

        public DirectionalFrames Idle => _idle;
        public DirectionalFrames Move => _move;
        public DirectionalFrames MoveAlt => _moveAlt;
        public DirectionalFrames Attack => _attack;
        public float IdleFps => _idleFps;
        public float AttackFps => _attackFps;
        public float StepChainGap => _stepChainGap;
        public int DashFrame => _dashFrame;

        private void OnValidate()
        {
            _move?.CollectWarnings("Move", this);
            _attack?.CollectWarnings("Attack", this);
        }
    }
}
