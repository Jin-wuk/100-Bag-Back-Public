using System;
using UnityEngine;
using BagBack100.Core;

namespace BagBack100.Player
{
    /// <summary>
    /// Role: 플레이어 전투 상태 판정. 마지막 타격·피격 후 일정 시간 동안 전투 중으로 본다.
    ///       포션 규칙(전투 중 지속 회복 + 공용 쿨타임)과 전투 중 장비 교체 잠금의 기준.
    /// Dependencies: 없음 (구독: GlobalEvents.OnEntityDamaged)
    /// Events: OnCombatStateChanged(bool isInCombat)
    /// </summary>
    public class PlayerCombatState : MonoBehaviour
    {
        [Tooltip("마지막 타격/피격 후 전투 상태를 유지하는 시간(초)")]
        [SerializeField] private float _combatHoldDuration = 5f;

        public bool IsInCombat { get; private set; }
        public event Action<bool> OnCombatStateChanged;

        private float _combatEndTime;

        private void OnEnable()
        {
            GlobalEvents.OnEntityDamaged += HandleEntityDamaged;
        }

        private void OnDisable()
        {
            GlobalEvents.OnEntityDamaged -= HandleEntityDamaged;
        }

        // 몬스터 피격은 현재 플레이어 공격으로만 발생하므로 플레이어 피격과 동일하게 전투 갱신으로 본다
        private void HandleEntityDamaged(Vector3 position, int damage, bool isHeavyHit, bool isPlayerDamaged)
        {
            _combatEndTime = Time.time + _combatHoldDuration;
            SetInCombat(true);
        }

        private void Update()
        {
            // Time.time 기준이라 가방 일시정지 중에는 전투 상태가 그대로 유지된다 (의도된 동작)
            if (IsInCombat && Time.time >= _combatEndTime)
            {
                SetInCombat(false);
            }
        }

        private void SetInCombat(bool inCombat)
        {
            if (IsInCombat == inCombat) return;
            IsInCombat = inCombat;
            OnCombatStateChanged?.Invoke(inCombat);
        }
    }
}
