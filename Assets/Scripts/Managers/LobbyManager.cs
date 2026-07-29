// File: Assets/Scripts/Managers/LobbyManager.cs
using UnityEngine;
using DimensionLooter.Core;

namespace DimensionLooter.Managers
{
    public class LobbyManager : MonoBehaviour
    {
        public static LobbyManager Instance { get; private set; }

        [SerializeField] private UI.Lobby.LobbyUI _lobbyUI;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged += HandleStateChanged;
                // 초기 상태 강제 동기화
                HandleStateChanged(GameManager.Instance.CurrentState);
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged -= HandleStateChanged;
            }
        }

        private void HandleStateChanged(Define.GameState state)
        {
            bool isLobby = (state == Define.GameState.Lobby);

            if (_lobbyUI != null)
            {
                if (isLobby) _lobbyUI.Show();
                else _lobbyUI.Hide();
            }

            // 로비 구역에서는 플레이어 컨트롤러 비활성화
            var player = FindObjectOfType<Gameplay.PlayerController>();
            if (player != null)
            {
                player.enabled = !isLobby; // 로비면 조작 불가, 던전이면 조작 가능
                if (isLobby)
                {
                    // 물리력 멈춤
                    var rb = player.GetComponent<Rigidbody2D>();
                    if (rb != null) rb.velocity = Vector2.zero;
                }
            }
        }

        public void SetLobbyUI(UI.Lobby.LobbyUI ui)
        {
            _lobbyUI = ui;
            if (GameManager.Instance != null)
            {
                HandleStateChanged(GameManager.Instance.CurrentState);
            }
        }
    }
}
