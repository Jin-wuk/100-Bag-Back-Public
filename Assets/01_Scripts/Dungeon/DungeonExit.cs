using UnityEngine;
using UnityEngine.SceneManagement;
using BagBack100.Player;

namespace BagBack100.Dungeon
{
    [RequireComponent(typeof(Collider2D))]
    public class DungeonExit : MonoBehaviour
    {
        [Header("Scene Transition")]
        [Tooltip("탈출 성공 시 이동할 허브 씬 이름")]
        [SerializeField] private string _hubSceneName = "02_HubScene";

        private bool _isTransitioning = false;

        private void Awake()
        {
            // 방어적 프로그래밍: 인스펙터에서 isTrigger 체크를 누락했더라도 강제로 트리거 모드 설정
            if (TryGetComponent<Collider2D>(out var col))
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Early Return: 이미 전환 중이거나 충돌체가 없으면 무시
            if (_isTransitioning || other == null) return;

            // PlayerController가 있는 객체와 충돌했는지 확인 (Magic string "Player" 태그 대신 타입 기반 체크)
            if (other.TryGetComponent<PlayerController>(out _))
            {
                _isTransitioning = true;
                ReturnToHub();
            }
        }

        private void ReturnToHub()
        {
            if (string.IsNullOrEmpty(_hubSceneName))
            {
                Debug.LogError("[DungeonExit] Hub Scene Name is null or empty!");
                return;
            }

            Debug.Log($"[DungeonExit] 탈출 성공! 인벤토리({BagBack100.Data.Core.PlayerSessionData.InventoryItems.Count}개) 및 장착 장비({BagBack100.Data.Core.PlayerSessionData.EquippedItems.Count}개) 보존하여 허브로 귀환합니다.");

            // LoadSceneMode.Single로 로드하여 기존 던전 씬 데이터 파괴 및 초기화 (아이템 데이터는 PlayerSessionData에 보존됨)
            SceneManager.LoadScene(_hubSceneName, LoadSceneMode.Single);
        }
    }
}
