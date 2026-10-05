using UnityEngine;
using BagBack100.Core;
using BagBack100.Dungeon;

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
        [SerializeField] private GameObject _storageUIPanel;
        [SerializeField] private GameObject _inventoryUIPanel;

        private void OnEnable()
        {
            GlobalEvents.OnHubObjectClicked += HandleHubObjectClicked;
        }

        private void OnDisable()
        {
            GlobalEvents.OnHubObjectClicked -= HandleHubObjectClicked;
        }

        private void HandleHubObjectClicked(HubObjectType objectType)
        {
            switch (objectType)
            {
                case HubObjectType.DungeonPortal:
                    Debug.Log("[HubSystem] 던전 포탈 클릭됨: 던전 진입 UI 오픈");
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
                    // TODO: UIManager에 대장간 패널 오픈 이벤트 전달
                    // 예: GlobalEvents.OnOpenBlacksmithPanel?.Invoke();
                    break;
                case HubObjectType.Shop:
                    Debug.Log("[HubSystem] 상점 클릭됨: 상점 패널 오픈 요청");
                    // TODO: UIManager에 상점 패널 오픈 이벤트 전달
                    break;
                case HubObjectType.Storage:
                    Debug.Log("[HubSystem] 창고 클릭됨: 창고 및 인벤토리 UI 오픈");
                    if (_storageUIPanel != null) _storageUIPanel.SetActive(true);
                    if (_inventoryUIPanel != null) _inventoryUIPanel.SetActive(true);
                    break;
                default:
                    Debug.LogWarning($"[HubSystem] 처리되지 않은 오브젝트 타입: {objectType}");
                    break;
            }
        }
    }
}
