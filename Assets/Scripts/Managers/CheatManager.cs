// File: Assets/Scripts/Managers/CheatManager.cs
using UnityEngine;
using DimensionLooter.Data;

namespace DimensionLooter.Managers
{
    /// <summary>
    /// 게임 플레이 로직을 우회하여 테스트를 용이하게 하는 치트 매니저입니다.
    /// [Why] 개발 중 인게임 상호작용 없이도 핵심 로직(아이템 획득, 전투 소환)을 즉시 발동시켜 검증하기 위함입니다.
    /// </summary>
    public class CheatManager : MonoBehaviour
    {
        private void Update()
        {
            // [치트 1] F1 키: 더미 아이템 1번(초보자의 검) 가방에 획득
            if (Input.GetKeyDown(KeyCode.F1))
            {
                if (DataManager.Instance.ItemDict.TryGetValue(1, out ItemData itemData))
                {
                    Debug.Log("[CheatManager] F1 누름: 초보자의 검 강제 획득 시도", this);
                    InventoryManager.Instance.TryAddAuto(itemData);
                }
            }

            // [치트 2] F2 키: 슬라임 몬스터 즉시 소환
            if (Input.GetKeyDown(KeyCode.F2))
            {
                Debug.Log("[CheatManager] F2 누름: 슬라임 강제 소환", this);
                CombatManager.Instance.SpawnMonster(1);
            }

            // [치트 3] F3 키: 게임 상태 강제 전환 (Combat)
            if (Input.GetKeyDown(KeyCode.F3))
            {
                Debug.Log("[CheatManager] F3 누름: 게임 상태 전환 -> Combat", this);
                GameManager.Instance.ChangeState(Core.Define.GameState.Combat);
            }
        }
    }
}
