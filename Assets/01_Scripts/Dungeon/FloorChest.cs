using UnityEngine;
using BagBack100.Core;
using BagBack100.Player;

namespace BagBack100.Dungeon
{
    /// <summary>
    /// Role: 층 상자. 자기 칸을 TileSystem에 점유로 등록해 플레이어·몬스터 이동을 막고,
    ///       플레이어의 근접·차지 공격을 받으면 한 번 열려 상자 열림 이벤트만 던진다 (내용물 드랍은 LootSystem).
    ///       열린 뒤에도 그 칸을 계속 막는다(유저 결정).
    /// Dependencies: TileSystem, PlayerController, IAttackInteractable
    /// Events: GlobalEvents.OnChestOpened
    /// </summary>
    public class FloorChest : MonoBehaviour, IAttackInteractable
    {
        [Tooltip("열린 뒤 표시 색 (전용 열린 스프라이트가 생기면 교체)")]
        [SerializeField] private Color _openedColor = new Color(0.55f, 0.5f, 0.45f, 1f);

        private SpriteRenderer _renderer;
        private int _dropPoolId = -1;
        private bool _isOpened;

        private void Awake()
        {
            _renderer = GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>층 생성 시 지역 설정의 상자 드랍 풀과 놓인 칸을 넣는다. 칸을 점유로 등록해 이동을 막는다.</summary>
        public void Initialize(int dropPoolId, Vector3Int cell)
        {
            _dropPoolId = dropPoolId;
            if (TileSystem.Instance != null)
            {
                TileSystem.Instance.RegisterInitialTile(cell, gameObject);
            }
        }

        public void OnAttacked(GameObject attacker)
        {
            // 몬스터 공격으로는 열리지 않는다
            if (_isOpened || attacker == null || !attacker.TryGetComponent<PlayerController>(out _)) return;

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
