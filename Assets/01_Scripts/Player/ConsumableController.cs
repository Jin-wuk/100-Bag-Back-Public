using System;
using System.Collections;
using UnityEngine;
using BagBack100.Data.Core;
using BagBack100.Data.Models;

namespace BagBack100.Player
{
    /// <summary>
    /// Role: 포션 사용 규칙 실행. 전투 중이면 지속 회복(HoT) + 공용 쿨타임(GCD), 전투가 아니면 즉시 회복.
    ///       회복할 것이 없으면 소모하지 않는다.
    /// Dependencies: PlayerStatManager, InventoryManager, PlayerCombatState, ConsumableDataSO
    /// Events: OnGcdStarted(duration), OnHotStateChanged(isActive), OnUseFailed(reason)
    /// </summary>
    [RequireComponent(typeof(PlayerStatManager))]
    [RequireComponent(typeof(InventoryManager))]
    public class ConsumableController : MonoBehaviour
    {
        public event Action<float> OnGcdStarted;
        public event Action<bool> OnHotStateChanged;
        public event Action<string> OnUseFailed;

        private PlayerStatManager _stats;
        private InventoryManager _inventory;
        private PlayerCombatState _combatState;

        private float _gcdEndTime;
        private float _gcdDuration;
        private Coroutine _hotCoroutine;

        public float GcdRemaining => Mathf.Max(0f, _gcdEndTime - Time.time);
        public float GcdDuration => _gcdDuration;
        public bool IsHotActive => _hotCoroutine != null;

        private void Awake()
        {
            _stats = GetComponent<PlayerStatManager>();
            _inventory = GetComponent<InventoryManager>();
            TryGetComponent(out _combatState);
        }

        private void OnDisable()
        {
            StopHot();
        }

        public bool TryUse(RuntimeItem item)
        {
            if (item == null || !(item.ItemData is ConsumableDataSO data)) return false;
            if (_stats.IsDead) return false;

            if (!CanRestoreAnything(data))
            {
                OnUseFailed?.Invoke("이미 가득 차 있습니다.");
                return false;
            }

            bool inCombat = _combatState != null && _combatState.IsInCombat;
            if (inCombat && GcdRemaining > 0f)
            {
                OnUseFailed?.Invoke("포션 재사용 대기 중입니다.");
                return false;
            }

            if (!_inventory.ConsumeOne(item)) return false;

            if (inCombat)
            {
                StartHot(data);
                _gcdDuration = data.combatGcd;
                _gcdEndTime = Time.time + data.combatGcd;
                OnGcdStarted?.Invoke(data.combatGcd);
            }
            else
            {
                if (data.hpRestore > 0) _stats.Heal(data.hpRestore);
                if (data.mpRestore > 0) _stats.RestoreMp(data.mpRestore);
            }
            return true;
        }

        // HP 전용 포션을 HP 가득·MP 부족 상태에서 써도 소모되지 않도록 회복 대상별로 판정
        private bool CanRestoreAnything(ConsumableDataSO data)
        {
            return (data.hpRestore > 0 && _stats.CurrentHp < _stats.MaxHp) ||
                   (data.mpRestore > 0 && _stats.CurrentMp < _stats.MaxMp);
        }

        /// <summary>회복 중 재사용 시 남은 회복량은 버리고 새 포션 기준으로 다시 시작한다 (중첩 불가).</summary>
        private void StartHot(ConsumableDataSO data)
        {
            StopHot();
            _hotCoroutine = StartCoroutine(HotRoutine(data.hpRestore, data.mpRestore, data.hotDuration));
            OnHotStateChanged?.Invoke(true);
        }

        private void StopHot()
        {
            if (_hotCoroutine == null) return;
            StopCoroutine(_hotCoroutine);
            _hotCoroutine = null;
            OnHotStateChanged?.Invoke(false);
        }

        private IEnumerator HotRoutine(int hpTotal, int mpTotal, float duration)
        {
            // 소수 회복량을 누적해 정수로 지급 (총량 오차 없이 duration 동안 균등 분배)
            float elapsed = 0f;
            int hpGiven = 0, mpGiven = 0;

            while (elapsed < duration)
            {
                elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
                float t = elapsed / duration;

                int hpTarget = Mathf.FloorToInt(hpTotal * t);
                int mpTarget = Mathf.FloorToInt(mpTotal * t);
                if (hpTarget > hpGiven) { _stats.Heal(hpTarget - hpGiven); hpGiven = hpTarget; }
                if (mpTarget > mpGiven) { _stats.RestoreMp(mpTarget - mpGiven); mpGiven = mpTarget; }

                yield return null;
            }

            _hotCoroutine = null;
            OnHotStateChanged?.Invoke(false);
        }
    }
}
