// File: Assets/Scripts/UI/Lobby/LobbyUI.cs
using UnityEngine;
using UnityEngine.UI;
using DimensionLooter.Managers;

namespace DimensionLooter.UI.Lobby
{
    public class LobbyUI : MonoBehaviour
    {
        [SerializeField] private Button _btnInventory;
        [SerializeField] private Button _btnEnterDungeon;

        private void Start()
        {
            if (_btnInventory != null)
                _btnInventory.onClick.AddListener(OnInventoryClicked);
            
            if (_btnEnterDungeon != null)
                _btnEnterDungeon.onClick.AddListener(OnEnterDungeonClicked);
        }

        private void OnDestroy()
        {
            if (_btnInventory != null)
                _btnInventory.onClick.RemoveListener(OnInventoryClicked);
            
            if (_btnEnterDungeon != null)
                _btnEnterDungeon.onClick.RemoveListener(OnEnterDungeonClicked);
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnInventoryClicked()
        {
            if (DimensionLooter.UI.Inventory.DualUIPanel.Instance != null)
            {
                DimensionLooter.UI.Inventory.DualUIPanel.Instance.ToggleInventory();
            }
        }

        private void OnEnterDungeonClicked()
        {
            Debug.Log("[LobbyUI] 던전 입장!");
            
            // 던전 구역 스폰 좌표 (임시 MVP)
            Vector3 dungeonSpawnPos = new Vector3(100f, 100f, 0f);

            var player = FindObjectOfType<Gameplay.PlayerController>();
            if (player != null)
            {
                player.transform.position = dungeonSpawnPos;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(Core.Define.GameState.Exploration);
            }
            
            // 던전 입장 시 인벤토리 닫기
            if (DimensionLooter.UI.Inventory.DualUIPanel.Instance != null)
            {
                DimensionLooter.UI.Inventory.DualUIPanel.Instance.Hide();
            }
        }
    }
}
