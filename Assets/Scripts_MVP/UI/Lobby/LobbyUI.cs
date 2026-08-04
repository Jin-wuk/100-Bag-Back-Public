// File: Assets/Scripts/UI/Lobby/LobbyUI.cs
using UnityEngine;
using UnityEngine.UI;
using DimensionLooter.Managers;

namespace DimensionLooter.UI.Lobby
{
    public class LobbyUI : MonoBehaviour
    {
        [Header("Global Actions")]
        [SerializeField] private Button _btnInventory;
        [SerializeField] private Button _btnEnterDungeon;

        [Header("Facility Open Buttons")]
        [SerializeField] private Button _btnShop;
        [SerializeField] private Button _btnStash;
        [SerializeField] private Button _btnBlacksmith;

        [Header("Facility Panels")]
        [SerializeField] private GameObject _shopPanel;
        [SerializeField] private GameObject _stashPanel;
        [SerializeField] private GameObject _blacksmithPanel;

        private void Start()
        {
            // 글로벌
            if (_btnInventory != null) _btnInventory.onClick.AddListener(OnInventoryClicked);
            if (_btnEnterDungeon != null) _btnEnterDungeon.onClick.AddListener(OnEnterDungeonClicked);

            // 시설 오픈
            if (_btnShop != null) _btnShop.onClick.AddListener(() => OpenFacilityPanel(_shopPanel));
            if (_btnStash != null) _btnStash.onClick.AddListener(() => OpenFacilityPanel(_stashPanel));
            if (_btnBlacksmith != null) _btnBlacksmith.onClick.AddListener(() => OpenFacilityPanel(_blacksmithPanel));
            
            // 모든 시설 패널 초기화 시 비활성화
            CloseAllFacilities();
        }

        private void OnDestroy()
        {
            if (_btnInventory != null) _btnInventory.onClick.RemoveListener(OnInventoryClicked);
            if (_btnEnterDungeon != null) _btnEnterDungeon.onClick.RemoveListener(OnEnterDungeonClicked);
            // 람다로 연결한 버튼 리스너는 게임오브젝트 파괴 시 자동 해제되므로 생략
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            CloseAllFacilities();
        }

        private void OpenFacilityPanel(GameObject targetPanel)
        {
            // 배타적 팝업: 하나 열면 나머지는 닫힘
            CloseAllFacilities();
            
            if (targetPanel != null)
            {
                targetPanel.SetActive(true);
            }
            
            // 시설을 열 때 내 가방(DualUIPanel)을 함께 열어서 드래그 앤 드롭 유도 및 최상단 렌더링
            if (DimensionLooter.UI.Inventory.DualUIPanel.Instance != null)
            {
                DimensionLooter.UI.Inventory.DualUIPanel.Instance.ShowOnlyMainBag();
                DimensionLooter.UI.Inventory.DualUIPanel.Instance.transform.SetAsLastSibling();
            }
        }

        private void CloseAllFacilities()
        {
            if (_shopPanel != null) _shopPanel.SetActive(false);
            if (_stashPanel != null) _stashPanel.SetActive(false);
            if (_blacksmithPanel != null) _blacksmithPanel.SetActive(false);
        }

        private void OnInventoryClicked()
        {
            CloseAllFacilities();
            
            if (DimensionLooter.UI.Inventory.DualUIPanel.Instance != null)
            {
                DimensionLooter.UI.Inventory.DualUIPanel.Instance.ToggleInventory();
            }
        }

        private void OnEnterDungeonClicked()
        {
            Debug.Log("[LobbyUI] 던전 입장!");
            
            // 임시 스폰 좌표
            Vector3 dungeonSpawnPos = new Vector3(100f, 100f, 0f);

            var player = FindObjectOfType<Gameplay.PlayerController>();
            if (player != null)
            {
                player.transform.position = dungeonSpawnPos;
            }
            
            if (DimensionLooter.Managers.HeatManager.Instance != null)
            {
                DimensionLooter.Managers.HeatManager.Instance.ApplyHeatMode();
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(Core.Define.GameState.Exploration);
            }
            
            if (DimensionLooter.UI.Inventory.DualUIPanel.Instance != null)
            {
                DimensionLooter.UI.Inventory.DualUIPanel.Instance.Hide();
            }
        }
    }
}
