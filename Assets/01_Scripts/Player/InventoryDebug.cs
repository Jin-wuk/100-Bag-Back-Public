//using UnityEngine;
//using DimensionLooter.Data.Models;
//using DimensionLooter.Data.Core;
//using DimensionLooter.Player;
//using System.Collections.Generic;

//namespace DimensionLooter.DebugTools
//{
//    public class InventoryDebug : MonoBehaviour
//    {
//        [Tooltip("테스트용으로 생성할 아이템 데이터 목록")]
//        public List<ItemDataSO> testItems;

//        [Tooltip("디버그 기능을 활성화할지 여부. 끄면 키 입력이 무시됩니다.")]
//        [SerializeField] private bool _enableDebug = true;

//        private InventoryManager _inventoryManager;

//        private void Start()
//        {
//            // 의도적으로 비워둠. 플레이어가 나중에 스폰되므로 매번 탐색해야 함.
//        }

//        private void Update()
//        {
//            if (!_enableDebug) return;

//            // O 키를 누르면 무작위 아이템 추가 시도
//            if (Input.GetKeyDown(KeyCode.O))
//            {
//                if (_inventoryManager == null)
//                    _inventoryManager = FindObjectOfType<InventoryManager>();

//                AddRandomTestItem();
//            }
//        }

//        private void AddRandomTestItem()
//        {
//            if (_inventoryManager == null || testItems == null || testItems.Count == 0) return;

//            ItemDataSO randomData = testItems[Random.Range(0, testItems.Count)];
//            RuntimeItem newItem = new RuntimeItem(randomData);

//            // 그리드 0,0부터 빈 공간을 찾아 스캔 (무식한 방법이지만 디버그용이므로 OK)
//            bool placed = false;
//            for (int y = 0; y < _inventoryManager.GridHeight; y++)
//            {
//                for (int x = 0; x < _inventoryManager.GridWidth; x++)
//                {
//                    if (_inventoryManager.AddItemAt(newItem, x, y))
//                    {
//                        placed = true;
//                        Debug.Log($"[Debug] {randomData.itemName} 아이템을 ({x}, {y})에 추가했습니다.");
//                        break;
//                    }
//                }
//                if (placed) break;
//            }

//            if (!placed)
//            {
//                Debug.LogWarning("[Debug] 가방이 꽉 찼거나 아이템을 놓을 빈 공간이 없습니다.");
//            }
//        }
//    }
//}
