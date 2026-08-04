// File: Assets/Scripts/Managers/GameManager.cs
using UnityEngine;
using DimensionLooter.Core;

namespace DimensionLooter.Managers
{
    /// <summary>
    /// 게임의 전반적인 상태(로비, 탐험, 전투)를 통제하는 최상위 매니저입니다.
    /// [Why] 싱글톤으로 구성하여 씬이 변경되어도 게임의 생명주기를 잃지 않도록 합니다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("State Settings")]
        [Tooltip("현재 게임의 진행 상태를 나타냅니다.")]
        [SerializeField] private Define.GameState _currentState = Define.GameState.None;

        public Define.GameState CurrentState => _currentState;

        public event System.Action<Define.GameState> OnStateChanged;

        private void Awake()
        {
            Debug.Log("[GameManager] Awake 실행됨!");
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Debug.Log("[GameManager] Instance 할당 완료!");
            }
            else
            {
                Destroy(gameObject);
                Debug.Log("[GameManager] 중복 Instance 감지되어 파괴됨!");
            }
        }

        private void Start()
        {
            ChangeState(Define.GameState.Lobby);
        }

        public void ChangeState(Define.GameState newState)
        {
            _currentState = newState;
            Debug.Log($"[GameManager] 게임 상태 변경: {_currentState}", this);
            OnStateChanged?.Invoke(_currentState);
        }

        public void OnPlayerDeath()
        {
            Debug.Log("[GameManager] 플레이어 사망! 페널티 적용 및 로비 귀환.");

            // 1. 가방 및 장비 증발 페널티
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.ClearMainInventory();
            }

            if (GearManager.Instance != null)
            {
                GearManager.Instance.DestroyAllGears();
            }

            // 2. UI 갱신 (만약 열려 있다면 닫거나 갱신)
            if (UI.Inventory.DualUIPanel.Instance != null)
            {
                UI.Inventory.DualUIPanel.Instance.RefreshGearSlots();
                UI.Inventory.DualUIPanel.Instance.Hide();
            }

            // 3. 로비 상태로 전환 및 텔레포트
            ChangeState(Define.GameState.Lobby);

            var player = FindObjectOfType<Gameplay.PlayerController>();
            if (player != null)
            {
                // 로비 구역 좌표 (MVP 기준 임시 좌표)
                player.transform.position = new Vector3(0, 0, 0);
                
                // 체력 회복
                var stats = player.GetComponent<Gameplay.Combat.PlayerStats>();
                if (stats != null)
                {
                    stats.Heal(9999); // 풀피 회복
                }
            }
        }
    }
}
