// File: Assets/Scripts/UI/InventoryUI.cs
using UnityEngine;
using DimensionLooter.Managers;

namespace DimensionLooter.UI
{
    /// <summary>
    /// 인벤토리 데이터를 시각적으로 표현하는 View 클래스입니다. (MVP UI 아키텍처)
    /// [Why] UI 클래스는 로직을 전혀 갖지 않으며, 오직 Manager의 데이터만 바라보고 렌더링합니다.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("타일 배경 프리팹 (빈 슬롯)")]
        [SerializeField] private GameObject _slotPrefab;
        
        [Tooltip("아이템 아이콘 프리팹 (생성되는 아이템 2D 이미지)")]
        [SerializeField] private GameObject _itemIconPrefab;
        
        [Tooltip("인벤토리 그리드가 생성될 부모 Transform")]
        [SerializeField] private Transform _gridParent;

        private void Start()
        {
            // Null 체크 (Fail-Fast)
            Debug.Assert(_slotPrefab != null, "[InventoryUI] _slotPrefab이 연결되지 않았습니다!", this);
            Debug.Assert(_itemIconPrefab != null, "[InventoryUI] _itemIconPrefab이 연결되지 않았습니다!", this);
            Debug.Assert(_gridParent != null, "[InventoryUI] _gridParent가 연결되지 않았습니다!", this);
        }

        // [Rule] 메모리 누수 방지를 위해 이벤트 구독은 무조건 OnEnable / OnDisable에서 짝을 맞춥니다.
        private void OnEnable()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryChanged += RefreshUI;
                RefreshUI(); // 활성화 시 즉시 1회 동기화
            }
        }

        private void OnDisable()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryChanged -= RefreshUI;
            }
        }

        /// <summary>
        /// 인벤토리 데이터가 변할 때마다 호출되어 화면을 다시 그립니다.
        /// </summary>
        private void RefreshUI()
        {
            Debug.Log("[InventoryUI] 인벤토리 화면을 갱신합니다.", this);
            
            var allItems = InventoryManager.Instance.Grid.GetAllItems();
            
            // TODO: 기존 생성된 _itemIconPrefab들을 풀(PoolManager)로 돌려보내고, 
            // allItems 목록을 순회하며 GridX, GridY 위치에 맞게 RectTransform 좌표를 계산하여 새로 렌더링.
            // MVP 1차 단계이므로 세부 UGUI 좌표 로직은 생략합니다.
        }
    }
}
