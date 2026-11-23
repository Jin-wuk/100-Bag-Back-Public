using System;
using System.Collections.Generic;
using UnityEngine;
using BagBack100.Monster;

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

        // optional = true: 방향이 통째로 비어 있는 것은 허용하고, 칸을 만든 뒤 비워 둔 프레임만 경고한다
        public void CollectWarnings(string label, UnityEngine.Object context, bool optional = false)
        {
            for (int i = 0; i < 4; i++)
            {
                FacingDir4 dir = (FacingDir4)i;
                Sprite[] frames = Get(dir);
                if (frames == null || frames.Length == 0)
                {
                    if (optional) continue;
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

    // 이동 프레임을 칸 하나에 어떻게 나눠 재생할지. 한 사이클(왼발+오른발) 시트를 두 방식으로 비교하려고 둔다
    public enum MoveCycleMode
    {
        HalfCyclePerTile, // 한 칸에 한 발: 첫 칸은 앞 절반, 이어 걷는 다음 칸은 뒤 절반
        FullCyclePerTile  // 한 칸에 사이클 전체
    }

    // (몬스터) 특정 패턴에서 기본 공격 애니 대신 쓸 프레임 세트. 예고·타격 규칙은 기본 공격과 같다
    [Serializable]
    public class PatternAttackAnim
    {
        [SerializeField] private MonsterPatternSO _pattern;
        [Tooltip("예고 전용 프레임. 비어 있으면 _attack[0.._hitFrame-1]을 예고로 쓴다")]
        [SerializeField] private DirectionalFrames _windup = new DirectionalFrames();
        [SerializeField] private DirectionalFrames _attack = new DirectionalFrames();
        [SerializeField, Min(0)] private int _hitFrame = 0;
        [SerializeField] private bool _loopStrikeWhileExecuting = false;

        public MonsterPatternSO Pattern => _pattern;
        public DirectionalFrames Windup => _windup;
        public DirectionalFrames Attack => _attack;
        public int HitFrame => _hitFrame;
        public bool LoopStrikeWhileExecuting => _loopStrikeWhileExecuting;
    }

    /// <summary>
    /// Role: 캐릭터 1종의 4방향 스프라이트 애니메이션 정적 데이터 (대기·이동·공격 프레임과 재생 속도).
    ///       몬스터는 패턴별 공격 애니(_patternAttacks)와 이동 중 반복 재생할 이동 프레임(_moveFps)을 추가로 쓸 수 있다.
    /// Dependencies: MonsterPatternSO(패턴별 공격 애니 키)
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
        [Tooltip("이동 프레임 재생 방식. HalfCyclePerTile: 첫 칸은 앞 절반, 이어 걷는 다음 칸은 뒤 절반(_moveAlt는 쓰지 않음). FullCyclePerTile: 칸마다 전체(_moveAlt가 있으면 이어 걷기에 번갈아 씀)")]
        [SerializeField] private MoveCycleMode _moveCycleMode = MoveCycleMode.FullCyclePerTile;
        [SerializeField] private DirectionalFrames _attack = new DirectionalFrames();
        [Tooltip("(몬스터) 예고 전용 프레임. 있으면 예고 시간 동안 이 프레임을 늘려 재생하고, 판정 순간부터 _attack 전체를 재생한다")]
        [SerializeField] private DirectionalFrames _attackWindup = new DirectionalFrames();
        [Tooltip("(몬스터) _attackWindup이 비어 있을 때 쓰는 타격 프레임 번호. _attack[0..이 번호-1]은 예고 시간에 맞춰 늘리고, 이 번호부터는 판정 순간부터 _attackFps로 재생한다")]
        [SerializeField, Min(0)] private int _attackHitFrame = 0;
        [Tooltip("(몬스터) 패턴이 끝나지 않았으면(돌진 이동 등) 타격 프레임을 반복한다")]
        [SerializeField] private bool _loopStrikeWhileExecuting = false;
        [Tooltip("(몬스터) 패턴별 공격 애니. 목록에 없는 패턴은 위 기본 공격 애니를 쓴다")]
        [SerializeField] private List<PatternAttackAnim> _patternAttacks = new List<PatternAttackAnim>();
        [Tooltip("(몬스터) 이동 프레임이 있을 때 이동 중 반복 재생 속도. 플레이어는 칸 진행도로 재생하므로 쓰지 않는다")]
        [SerializeField, Min(0.1f)] private float _moveFps = 10f;
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
        public DirectionalFrames AttackWindup => _attackWindup;
        public MoveCycleMode MoveCycle => _moveCycleMode;
        public int AttackHitFrame => _attackHitFrame;
        public bool LoopStrikeWhileExecuting => _loopStrikeWhileExecuting;
        public float IdleFps => _idleFps;
        public float AttackFps => _attackFps;
        public float StepChainGap => _stepChainGap;
        public int DashFrame => _dashFrame;
        public float MoveFps => _moveFps;

        // 패턴 수가 적어 선형 탐색. 할당 없음
        public PatternAttackAnim FindPatternAttack(MonsterPatternSO pattern)
        {
            if (pattern == null || _patternAttacks == null) return null;
            for (int i = 0; i < _patternAttacks.Count; i++)
            {
                if (_patternAttacks[i] != null && _patternAttacks[i].Pattern == pattern) return _patternAttacks[i];
            }
            return null;
        }

        private void OnValidate()
        {
            _idle?.CollectWarnings("Idle", this, true);
            // 몬스터는 이동 프레임 없이 대기 프레임으로 이동하므로 이동이 통째로 비어 있는 것은 허용한다
            _move?.CollectWarnings("Move", this, true);
            _moveAlt?.CollectWarnings("MoveAlt", this, true);
            _attack?.CollectWarnings("Attack", this);
            _attackWindup?.CollectWarnings("AttackWindup", this, true);

            CheckHitFrame(_attackWindup, _attack, _attackHitFrame, "기본 공격");

            if (_patternAttacks == null) return;
            for (int i = 0; i < _patternAttacks.Count; i++)
            {
                PatternAttackAnim entry = _patternAttacks[i];
                if (entry == null) continue;
                string label = $"패턴 공격 {i}";
                if (entry.Pattern == null) Debug.LogWarning($"[SpriteAnimSetSO] {name}: {label}의 패턴이 비어 있습니다.", this);
                entry.Windup?.CollectWarnings(label + " Windup", this, true);
                entry.Attack?.CollectWarnings(label + " Attack", this);
                CheckHitFrame(entry.Windup, entry.Attack, entry.HitFrame, label);
            }
        }

        // 타격 프레임 번호는 예고 전용 프레임이 없는 방향에서만 쓴다
        private void CheckHitFrame(DirectionalFrames windupFrames, DirectionalFrames attackFrames, int hitFrame, string label)
        {
            if (attackFrames == null || windupFrames == null) return;
            for (int i = 0; i < 4; i++)
            {
                FacingDir4 dir = (FacingDir4)i;
                Sprite[] windup = windupFrames.Get(dir);
                Sprite[] attack = attackFrames.Get(dir);
                if ((windup == null || windup.Length == 0) && attack != null && attack.Length > 0 && hitFrame >= attack.Length)
                {
                    Debug.LogWarning($"[SpriteAnimSetSO] {name}: {label} {dir} 타격 프레임 번호({hitFrame})가 공격 프레임 수({attack.Length}) 이상이라 타격 구간이 비어 있습니다.", this);
                }
            }
        }
    }
}
