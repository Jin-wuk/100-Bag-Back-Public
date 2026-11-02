using UnityEngine;
using BagBack100.Core;
using BagBack100.Player;

namespace BagBack100.Dungeon
{
    /// <summary>
    /// Role: 층 상자. 플레이어가 밟으면 한 번 열리고 상자 열림 이벤트만 던진다 (내용물 드랍은 LootSystem이 처리).
    ///       실수로 열어도 손해가 없으므로 확인창 없이 바로 연다.
    /// Dependencies: PlayerController
    /// Events: GlobalEvents.OnChestOpened
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class FloorChest : MonoBehaviour
    {
        [Tooltip("열린 뒤 표시 색 (회색 박스용)")]
        [SerializeField] private Color _openedColor = new Color(0.45f, 0.4f, 0.3f, 0.9f);

        private SpriteRenderer _renderer;
        private int _dropPoolId = -1;
        private bool _isOpened;

        private void Awake()
        {
            _renderer = GetComponentInChildren<SpriteRenderer>();
            if (TryGetComponent<Collider2D>(out var col))
            {
                col.isTrigger = true;
            }
        }

        /// <summary>층 생성 시 지역 설정의 상자 드랍 풀을 넣는다.</summary>
        public void Initialize(int dropPoolId)
        {
            _dropPoolId = dropPoolId;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isOpened || other == null) return;
            if (!other.TryGetComponent<PlayerController>(out _)) return;

            _isOpened = true;
            if (_renderer != null) _renderer.color = _openedColor;

            if (_dropPoolId < 0)
            {
                Debug.LogWarning($"[FloorChest] {name}에 드랍 풀이 지정되지 않아 빈 상자로 열립니다.");
                return;
            }
            GlobalEvents.OnChestOpened?.Invoke(transform.position, _dropPoolId);
        }
    }
}
