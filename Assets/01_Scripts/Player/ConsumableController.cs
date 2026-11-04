using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using BagBack100.Data.Core;
using BagBack100.Data.Models;
using BagBack100.Dungeon.Data;

namespace BagBack100.Player
{
    /// <summary>
    /// Role: 소모품 사용 규칙 실행. 포션은 전투 중이면 지속 회복(HoT) + 공용 쿨타임(GCD), 전투가 아니면 즉시 회복하고,
    ///       회복할 것이 없으면 소모하지 않는다. 귀환석은 비전투·보스층 아님을 확인한 뒤 확인창 요청만 던지고,
    ///       확인되면 1개 소모 후 탈출 결과를 기록하고 허브로 돌아간다 (기획 07 Q31).
    /// Dependencies: PlayerStatManager, InventoryManager, PlayerCombatState, ConsumableDataSO, ReturnStoneDataSO,
    ///               RegionConfigSO(보스층 판정), PlayerSessionData, RunResult
    /// Events: OnGcdStarted(duration), OnHotStateChanged(isActive), OnUseFailed(reason), OnReturnStoneRequested(item)
    /// </summary>
    [RequireComponent(typeof(PlayerStatManager))]
    [RequireComponent(typeof(InventoryManager))]
    public class ConsumableController : MonoBehaviour
    {
        public event Action<float> OnGcdStarted;
        public event Action<bool> OnHotStateChanged;
        public event Action<string> OnUseFailed;
        // 귀환석 확인창 요청. 확인창 UI가 구독하고 확인 시 ConfirmReturnStone을 호출한다
        public event Action<RuntimeItem> OnReturnStoneRequested;

        [Header("Return Stone")]
        [Tooltip("귀환석으로 탈출할 때 이동할 허브 씬 이름")]
        [SerializeField] private string _hubSceneName = "02_HubScene";
        // 던전 HUD에 한글 TMP 폰트가 없어 영어 문구를 쓴다
        [SerializeField] private string _combatBlockedMessage = "Can't use it during combat.";
        [SerializeField] private string _bossFloorBlockedMessage = "Can't use it on a boss floor.";

        private PlayerStatManager _stats;
        private InventoryManager _inventory;
        private PlayerCombatState _combatState;

        private float _gcdEndTime;
        private float _gcdDuration;
        private Coroutine _hotCoroutine;
        private bool _isReturning;

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

            if (data is ReturnStoneDataSO) return TryRequestReturn(item);

            if (!CanRestoreAnything(data))
            {
                OnUseFailed?.Invoke("Already full.");
                return false;
            }

            bool inCombat = _combatState != null && _combatState.IsInCombat;
            if (inCombat && GcdRemaining > 0f)
            {
                OnUseFailed?.Invoke("Potion is on cooldown.");
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

        // 확인창을 거친 뒤에 소모하므로 여기서는 항상 false(아직 소모 안 함)를 돌려준다
        private bool TryRequestReturn(RuntimeItem item)
        {
            if (_isReturning) return false;

            if (_combatState != null && _combatState.IsInCombat)
            {
                OnUseFailed?.Invoke(_combatBlockedMessage);
                return false;
            }

            if (RegionConfigSO.IsBossFloor(PlayerSessionData.CurrentFloor))
            {
                OnUseFailed?.Invoke(_bossFloorBlockedMessage);
                return false;
            }

            // 확인창 UI가 씬에 없으면 탈출 포탈처럼 바로 탈출한다 (확인창 누락으로 사용이 막히지 않게)
            if (OnReturnStoneRequested == null) return ConfirmReturnStone(item);

            OnReturnStoneRequested.Invoke(item);
            return false;
        }

        /// <summary>
        /// 확인창에서 "Leave"를 고르면 UI가 호출한다. 확인창이 떠 있는 동안은 일시정지·입력 잠금이라
        /// 전투 상태와 층이 바뀌지 않으므로 사용 조건은 다시 보지 않고, 사망·중복·가방에서 빠졌는지만 확인한다.
        /// </summary>
        public bool ConfirmReturnStone(RuntimeItem item)
        {
            if (_isReturning || _stats.IsDead) return false;
            if (item == null || !(item.ItemData is ReturnStoneDataSO)) return false;
            if (!_inventory.ConsumeOne(item)) return false;

            _isReturning = true;
            // 귀환석을 소모한 뒤에 기록해야 "가져온 것" 목록에 귀환석이 들어가지 않는다
            PlayerSessionData.LastRunResult = RunResult.CreateFromSession(true);
            SceneManager.LoadScene(_hubSceneName, LoadSceneMode.Single);
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
