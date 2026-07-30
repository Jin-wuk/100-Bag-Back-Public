// File: Assets/Scripts/Gameplay/ReturnPortal.cs
using UnityEngine;
using DimensionLooter.Managers;

namespace DimensionLooter.Gameplay
{
    public class ReturnPortal : MonoBehaviour, Core.IInteractable
    {
        [Tooltip("로비 귀환 시 이동할 스폰 좌표")]
        [SerializeField] private Vector3 _lobbySpawnPosition = new Vector3(0, 0, 0);

        public void Interact(GameObject interactor)
        {
            if (interactor.CompareTag("Player"))
            {
                ReturnToLobby(interactor);
            }
        }

        private void ReturnToLobby(GameObject player)
        {
            Debug.Log("[ReturnPortal] 던전 탈출 성공! 무사 귀환했습니다.");

            // 플레이어 체력 완전 회복
            var stats = player.GetComponent<Combat.PlayerStats>();
            if (stats != null)
            {
                stats.Heal(9999);
            }

            // 플레이어 좌표 이동
            player.transform.position = _lobbySpawnPosition;

            // 로비 상태로 전환 (아이템 손실 없음)
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(Core.Define.GameState.Lobby);
            }
        }
    }
}
