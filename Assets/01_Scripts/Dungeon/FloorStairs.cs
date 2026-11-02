using System;
using UnityEngine;
using BagBack100.Player;

namespace BagBack100.Dungeon
{
    /// <summary>
    /// Role: 위층 계단. 플레이어가 밟으면 계단 진입 이벤트만 던진다 (층 전환은 DungeonGenerator가 처리).
    ///       T2: 항상 열림. 잠금·클리어 순간 겹침 가드는 T4에서 추가.
    /// Dependencies: PlayerController
    /// Events: OnStairsEntered(static, 지역 이벤트)
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class FloorStairs : MonoBehaviour
    {
        public static event Action OnStairsEntered;

        private bool _isUsed;

        private void Awake()
        {
            if (TryGetComponent<Collider2D>(out var col))
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isUsed || other == null) return;
            if (!other.TryGetComponent<PlayerController>(out _)) return;

            // 같은 계단으로 두 번 전환하지 않도록 한 번만 발동한다 (층이 바뀌면 계단째 파괴된다)
            _isUsed = true;
            OnStairsEntered?.Invoke();
        }
    }
}
