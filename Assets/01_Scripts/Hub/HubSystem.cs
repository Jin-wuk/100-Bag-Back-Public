using UnityEngine;
using BagBack100.Core;
using BagBack100.Dungeon;
using BagBack100.Player;

namespace BagBack100.Hub
{
    /// <summary>
    /// 허브 내 시스템 및 상태를 관리하는 2계층 System.
    /// 싱글톤을 사용하지 않으며, 전역 이벤트를 수신하여 로직을 분기합니다.
    /// </summary>
    public class HubSystem : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private DungeonEntranceUI _dungeonEntranceUI;
        [Tooltip("허브 창고 UI 패널")]
        [SerializeField] private GameObject _storageUIPanel;
        [Tooltip("인벤토리 및 장비창 통합 UI 패널")]
        [SerializeField] private GameObject _inventoryUIPanel;

        private void Awake()
        {
            EnsureHubManagers();
            CloseStorageUI();
        }

        /// <summary>
        /// 허브 씬에 Player 프리팹이 없더라도 창고↔인벤토리/장비 상호작용이 동작하도록
        /// StorageManager, InventoryManager, EquipmentManager 존재를 보장합니다.
        /// </summary>
        private void EnsureHubManagers()
        {
            var storageManager = FindObjectOfType<StorageManager>();
            GameObject managerHost = storageManager != null ? storageManager.gameObject : gameObject;

            if (storageManager == null)
            {
                managerHost.AddComponent<StorageManager>();
            }

            if (FindObjectOfType<InventoryManager>() == null)
            {
                managerHost.AddComponent<InventoryManager>();
            }

            if (FindObjectOfType<EquipmentManager>() == null)
            {
                managerHost.AddComponent<EquipmentManager>();
            }
        }

        private void OnEnable()
        {
            GlobalEvents.OnHubObjectClicked += HandleHubObjectClicked;
        }

        private void OnDisable()
        {
            GlobalEvents.OnHubObjectClicked -= HandleHubObjectClicked;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseAllPanels();
            }
            else if (Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleInventoryUI();
            }
        }

        private void HandleHubObjectClicked(HubObjectType objectType)
        {
            switch (objectType)
            {
                case HubObjectType.DungeonPortal:
                    Debug.Log("[HubSystem] 던전 포탈 클릭됨: 던전 진입 UI 오픈");
                    CloseStorageUI();
                    if (_dungeonEntranceUI != null)
                    {
                        _dungeonEntranceUI.ShowPanel();
                    }
                    else
                    {
                        Debug.LogError("[HubSystem] DungeonEntranceUI가 할당되지 않았습니다!");
                    }
                    break;
                case HubObjectType.Blacksmith:
                    Debug.Log("[HubSystem] 대장간 클릭됨: 강화 패널 오픈 요청");
                    break;
                case HubObjectType.Shop:
                    Debug.Log("[HubSystem] 상점 클릭됨: 상점 패널 오픈 요청");
                    break;
                case HubObjectType.Storage:
                    Debug.Log("[HubSystem] 창고(상자) 클릭됨: 창고 및 인벤토리/장비 UI 동시 오픈");
                    if (_dungeonEntranceUI != null) _dungeonEntranceUI.HidePanel();
                    OpenStorageUI();
                    break;
                default:
                    Debug.LogWarning($"[HubSystem] 처리되지 않은 오브젝트 타입: {objectType}");
                    break;
            }
        }

        public void ToggleInventoryUI()
        {
            if (_inventoryUIPanel == null) return;

            if (_inventoryUIPanel.activeInHierarchy)
            {
                CloseStorageUI();
            }
            else
            {
                if (_dungeonEntranceUI != null) _dungeonEntranceUI.HidePanel();
                if (_storageUIPanel != null) _storageUIPanel.SetActive(false);
                SetPanelAndParentsActive(_inventoryUIPanel, true);
            }
        }

        public void OpenStorageUI()
        {
            SetPanelAndParentsActive(_storageUIPanel, true);
            SetPanelAndParentsActive(_inventoryUIPanel, true);
        }

        public void CloseStorageUI()
        {
            SetPanelAndParentsActive(_storageUIPanel, false);
            SetPanelAndParentsActive(_inventoryUIPanel, false);
        }

        private void SetPanelAndParentsActive(GameObject panel, bool isActive)
        {
            if (panel == null) return;

            panel.SetActive(isActive);

            // 부모 컨테이너(예: StorageUIPanel)가 Canvas 하위에 묶여 있는 경우 함께 활성화/비활성화
            Transform parent = panel.transform.parent;
            while (parent != null && parent.GetComponent<Canvas>() == null)
            {
                parent.gameObject.SetActive(isActive);
                parent = parent.parent;
            }
        }

        public void CloseAllPanels()
        {
            CloseStorageUI();
            if (_dungeonEntranceUI != null) _dungeonEntranceUI.HidePanel();
        }
    }
}
