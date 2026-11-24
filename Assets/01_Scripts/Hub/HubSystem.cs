using UnityEngine;
using BagBack100.Core;
using BagBack100.Dungeon;
using BagBack100.Player;

namespace BagBack100.Hub
{
    /// <summary>
    /// Role: 허브 내 시스템 및 상태를 관리하는 2계층 System. 싱글톤을 쓰지 않고 전역 이벤트를 받아 분기한다.
    ///       플레이어가 없는 허브에서도 창고·가방·장비·퀵슬롯 매니저가 존재하도록 보장한다. 스탯(PlayerStatManager)은 기본 스탯 SO 연결이 필요해 씬 Manager에 직접 배치하고, 없으면 경고만 남긴다.
    /// Dependencies: DungeonEntranceUI, 대장간(제작)·잡화상(구매·판매) 패널, StorageManager, InventoryManager, EquipmentManager, QuickSlotManager, SecurePouchManager, PlayerStatManager(존재 확인만)
    /// Events: 없음 (구독: GlobalEvents.OnHubObjectClicked)
    /// </summary>
    public class HubSystem : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private DungeonEntranceUI _dungeonEntranceUI;
        [Tooltip("허브 창고 UI 패널")]
        [SerializeField] private GameObject _storageUIPanel;
        [Tooltip("인벤토리 및 장비창 통합 UI 패널")]
        [SerializeField] private GameObject _inventoryUIPanel;
        [Tooltip("대장간(제작, 테크트리) 패널. 창고 패널과 같은 자리에 표시")]
        [SerializeField] private GameObject _blacksmithPanel;
        [Tooltip("잡화상(구매·판매) 패널. 창고 패널과 같은 자리에 표시")]
        [SerializeField] private GameObject _shopPanel;

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

            // 퀵슬롯 등록은 허브에서도 할 수 있어야 한다 (사용은 던전에서만)
            // RequireComponent(InventoryManager) 때문에 인벤토리와 같은 오브젝트에 붙여야 인벤토리가 중복 생성되지 않는다
            var inventory = FindObjectOfType<InventoryManager>();
            if (FindObjectOfType<QuickSlotManager>() == null)
            {
                inventory.gameObject.AddComponent<QuickSlotManager>();
            }

            // 보안 파우치는 허브에서 넣고 빼야 하므로 가방과 같은 오브젝트에 보장한다
            if (FindObjectOfType<SecurePouchManager>() == null)
            {
                inventory.gameObject.AddComponent<SecurePouchManager>();
            }

            // 기본 스탯 SO가 있어야 동작하므로 AddComponent로 만들지 않는다. 장비 패널 스탯 표시가 비는 원인을 바로 알 수 있게 경고만 남긴다
            if (FindObjectOfType<PlayerStatManager>() == null)
            {
                Debug.LogWarning("[HubSystem] 허브 씬에 PlayerStatManager가 없어 장비 패널 스탯이 표시되지 않습니다. Manager 오브젝트에 PlayerStatManager를 붙이고 PlayerBaseStats SO를 연결하세요.", this);
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
                // 월드맵이 열려 있으면 입장 패널 → 지도 순으로 한 겹씩 닫는다
                if (_dungeonEntranceUI != null && _dungeonEntranceUI.TryCloseTopLayer()) return;
                CloseAllPanels();
            }
            else if (Input.GetKeyDown(KeyCode.Tab))
            {
                // 전체 화면 월드맵 위로 가방이 겹치지 않게 막는다
                if (_dungeonEntranceUI != null && _dungeonEntranceUI.IsOpen) return;
                ToggleInventoryUI();
            }
        }

        private void HandleHubObjectClicked(HubObjectType objectType)
        {
            switch (objectType)
            {
                case HubObjectType.DungeonPortal:
                    Debug.Log("[HubSystem] 던전 포탈 클릭됨: 월드맵 오픈");
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
                    if (_dungeonEntranceUI != null) _dungeonEntranceUI.HidePanel();
                    OpenSidePanelWithInventory(_blacksmithPanel);
                    break;
                case HubObjectType.Shop:
                    if (_dungeonEntranceUI != null) _dungeonEntranceUI.HidePanel();
                    OpenSidePanelWithInventory(_shopPanel);
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
                HideSidePanels();
                SetPanelAndParentsActive(_inventoryUIPanel, true);
            }
        }

        public void OpenStorageUI() => OpenSidePanelWithInventory(_storageUIPanel);

        // 창고·대장간·잡화상은 가방 옆 같은 자리를 쓰므로 하나만 켠다
        private void OpenSidePanelWithInventory(GameObject sidePanel)
        {
            HideSidePanels();
            SetPanelAndParentsActive(sidePanel, true);
            SetPanelAndParentsActive(_inventoryUIPanel, true);
        }

        private void HideSidePanels()
        {
            if (_storageUIPanel != null) _storageUIPanel.SetActive(false);
            if (_blacksmithPanel != null) _blacksmithPanel.SetActive(false);
            if (_shopPanel != null) _shopPanel.SetActive(false);
        }

        public void CloseStorageUI()
        {
            HideSidePanels();
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
