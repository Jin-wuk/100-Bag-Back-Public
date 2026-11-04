using System;
using UnityEngine;
using BagBack100.Core;

namespace BagBack100.Dungeon
{
    /// <summary>
    /// Role: 위층 계단. 잠긴 채로 생성되고 층 클리어 시 열린다. 열린 계단 칸에 플레이어가 도착하면 계단 진입 이벤트만 던진다
    ///       (층 전환은 DungeonGenerator가 처리). 확인창 없음(기획 07 Q28).
    ///       진입은 물리 트리거가 아니라 타일 도착 이벤트(OnPlayerCellChanged)의 칸 비교로 판정한다 —
    ///       콜라이더가 인접 칸과 닿아 오발동하거나, 이동 보간 중 잠금 해제 시 물리 상태와 칸이 어긋나는 것을 막는다.
    /// Dependencies: GlobalEvents.OnPlayerCellChanged (구독)
    /// Events: OnStairsEntered(static, 지역 이벤트)
    /// </summary>
    public class FloorStairs : MonoBehaviour
    {
        public static event Action OnStairsEntered;

        [Tooltip("잠겨 있을 때 스프라이트 색 (층 클리어 전)")]
        [SerializeField] private Color _lockedColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        private SpriteRenderer[] _renderers;
        private Color[] _unlockedColors;
        private Vector3Int _cell;
        private bool _isInitialized;
        private bool _isLocked = true;
        private bool _isUsed;
        // 잠금이 풀리는 순간 계단 칸에 서 있었다면, 다른 칸에 도착했다가 다시 밟아야 발동한다
        private bool _waitForLeave;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => OnStairsEntered = null;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _unlockedColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _unlockedColors[i] = _renderers[i].color;
            }
            ApplyLockVisual();
        }

        // 층 정리 시 이전 층을 즉시 비활성화하므로, 같은 프레임의 새 층 도착 이벤트를 받지 않도록 OnDisable에서 해제한다
        private void OnEnable() => GlobalEvents.OnPlayerCellChanged += HandlePlayerCellChanged;
        private void OnDisable() => GlobalEvents.OnPlayerCellChanged -= HandlePlayerCellChanged;

        /// <summary>생성 직후 DungeonGenerator가 자기 칸을 넣는다 (월드 좌표 반올림 오차 방지).</summary>
        public void Initialize(Vector3Int cell)
        {
            _cell = cell;
            _isInitialized = true;
        }

        /// <summary>층 클리어 시 호출. 플레이어의 마지막 도착 칸이 계단 칸이면 한 번 벗어나야 발동한다.</summary>
        public void Unlock(Vector3Int lastPlayerCell)
        {
            if (!_isLocked) return;
            _isLocked = false;
            _waitForLeave = _isInitialized && lastPlayerCell == _cell;
            ApplyLockVisual();
        }

        private void HandlePlayerCellChanged(Vector3Int playerCell)
        {
            if (!_isInitialized || _isUsed) return;

            if (playerCell != _cell)
            {
                _waitForLeave = false;
                return;
            }

            if (_isLocked || _waitForLeave) return;

            // 같은 계단으로 두 번 전환하지 않도록 한 번만 발동한다 (층이 바뀌면 계단째 파괴된다)
            _isUsed = true;
            OnStairsEntered?.Invoke();
        }

        private void ApplyLockVisual()
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null) _renderers[i].color = _isLocked ? _lockedColor : _unlockedColors[i];
            }
        }
    }
}
