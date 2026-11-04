using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using BagBack100.Core;

namespace BagBack100.Dungeon
{
    /// <summary>
    /// Role: 탈출 포탈. 층 클리어 시 확률로 생성된다. 플레이어가 포탈 칸에 도착하면 탈출 요청 이벤트만 던지고(확인창은 UI 담당),
    ///       확인되면 탈출 결과(RunResult)를 기록하고 허브 씬으로 전환한다 (가방·장비·파우치는 세션에 보존).
    ///       진입은 물리 트리거가 아니라 타일 도착 이벤트(OnPlayerCellChanged)의 칸 비교로 판정한다 —
    ///       포탈·플레이어 콜라이더가 위아래 인접 칸에서 닿아 오발동하는 것을 막는다.
    /// Dependencies: GlobalEvents.OnPlayerCellChanged (구독), PlayerSessionData, RunResult
    /// Events: OnExitRequested(static, 지역 이벤트)
    /// </summary>
    public class DungeonExit : MonoBehaviour
    {
        public static event Action<DungeonExit> OnExitRequested;

        [Header("Scene Transition")]
        [Tooltip("탈출 성공 시 이동할 허브 씬 이름")]
        [SerializeField] private string _hubSceneName = "02_HubScene";

        private Vector3Int _cell;
        private bool _isActive;
        private bool _isTransitioning;
        // 생성 순간 또는 확인창 취소 직후 포탈 칸에 서 있으면, 다른 칸에 도착했다가 다시 밟아야 확인창을 띄운다 (기획 07 Q22)
        private bool _waitForLeave;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => OnExitRequested = null;

        private void Awake()
        {
            // 진입은 칸 판정으로 하지만, 프리팹 콜라이더가 이동·경로 판정에서 벽처럼 막지 않도록 트리거로 둔다
            foreach (var col in GetComponents<Collider2D>())
            {
                col.isTrigger = true;
            }
        }

        // 층 정리 시 이전 층을 즉시 비활성화하므로, 같은 프레임의 새 층 도착 이벤트를 받지 않도록 OnDisable에서 해제한다
        private void OnEnable() => GlobalEvents.OnPlayerCellChanged += HandlePlayerCellChanged;
        private void OnDisable() => GlobalEvents.OnPlayerCellChanged -= HandlePlayerCellChanged;

        /// <summary>생성 직후 DungeonGenerator가 호출한다. 플레이어의 마지막 도착 칸이 포탈 칸이면 겹침 가드를 건다.</summary>
        public void Activate(Vector3Int cell, Vector3Int lastPlayerCell)
        {
            _cell = cell;
            _isActive = true;
            _waitForLeave = lastPlayerCell == cell;
        }

        /// <summary>확인창에서 "탈출"을 고르면 UI가 호출한다.</summary>
        public void ConfirmExit()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;
            ReturnToHub();
        }

        /// <summary>확인창에서 취소하면 UI가 호출한다. 칸을 벗어났다 다시 밟아야 다시 묻는다.</summary>
        public void CancelExit()
        {
            _waitForLeave = true;
        }

        private void HandlePlayerCellChanged(Vector3Int playerCell)
        {
            if (!_isActive || _isTransitioning) return;

            if (playerCell != _cell)
            {
                _waitForLeave = false;
                return;
            }

            if (_waitForLeave) return;

            // 확인창 UI가 씬에 없으면 예전처럼 바로 탈출한다 (확인창 누락으로 탈출이 막히지 않게)
            if (OnExitRequested == null)
            {
                ConfirmExit();
                return;
            }

            // 확인창이 떠 있는 동안 다시 묻지 않도록, 응답(확인·취소)이 올 때까지 가드를 건다
            _waitForLeave = true;
            OnExitRequested.Invoke(this);
        }

        private void ReturnToHub()
        {
            if (string.IsNullOrEmpty(_hubSceneName))
            {
                Debug.LogError("[DungeonExit] Hub Scene Name is null or empty!");
                _isTransitioning = false; // 설정을 고친 뒤 다시 밟으면 탈출할 수 있게 잠금을 푼다
                return;
            }

            Debug.Log($"[DungeonExit] 탈출 성공! 인벤토리({BagBack100.Data.Core.PlayerSessionData.InventoryItems.Count}개) 및 장착 장비({BagBack100.Data.Core.PlayerSessionData.EquippedItems.Count}개) 보존하여 허브로 귀환합니다.");

            BagBack100.Data.Core.PlayerSessionData.LastRunResult = BagBack100.Data.Core.RunResult.CreateFromSession(true);

            // LoadSceneMode.Single로 로드하여 기존 던전 씬 데이터 파괴 및 초기화 (아이템 데이터는 PlayerSessionData에 보존됨)
            SceneManager.LoadScene(_hubSceneName, LoadSceneMode.Single);
        }
    }
}
