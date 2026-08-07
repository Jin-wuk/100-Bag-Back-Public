using UnityEngine;
using DimensionLooter.Player;
using DimensionLooter.Data.Core;

namespace DimensionLooter.UI
{
    public class EquipmentUIController : MonoBehaviour
    {
        [SerializeField] private EquipmentManager _equipmentManager;



        private void Start()
        {
            if (_equipmentManager != null)
            {
                _equipmentManager.OnEquipmentChanged += RefreshUI;
                _equipmentManager.OnEquipmentFailed += ShowErrorMessage;
            }
        }

        private void OnDestroy()
        {
            if (_equipmentManager != null)
            {
                _equipmentManager.OnEquipmentChanged -= RefreshUI;
                _equipmentManager.OnEquipmentFailed -= ShowErrorMessage;
            }
        }

        public void Bind(EquipmentManager eqManager)
        {
            if (_equipmentManager != null)
            {
                _equipmentManager.OnEquipmentChanged -= RefreshUI;
                _equipmentManager.OnEquipmentFailed -= ShowErrorMessage;
            }

            _equipmentManager = eqManager;

            if (_equipmentManager != null)
            {
                _equipmentManager.OnEquipmentChanged += RefreshUI;
                _equipmentManager.OnEquipmentFailed += ShowErrorMessage;
            }
        }

        private void RefreshUI()
        {
            // 장착된 장비 아이콘 갱신 로직
            // 각 부위별 UI 슬롯에 아이콘 할당
        }

        private void ShowErrorMessage(string msg)
        {
            Debug.LogWarning($"[System] {msg}");
            // 화면 중앙 플로팅 텍스트 띄우기 로직 연동 가능
        }
    }
}
