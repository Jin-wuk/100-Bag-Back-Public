using UnityEngine;
using BagBack100.Core;
using BagBack100.Dungeon;
using BagBack100.Player;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 탈출 포탈 확인창 뷰 ("탈출할까요?", 기획 07 Q22). 포탈의 탈출 요청을 받아 확인창을 띄우고,
    ///       떠 있는 동안 게임을 일시정지하고 플레이어 입력을 잠근다. 결과는 포탈에 커맨드(ConfirmExit / CancelExit)로만 전달한다.
    ///       일시정지만으로는 PlayerInput이 Tab·퀵슬롯·공격 입력을 그대로 받으므로 입력 잠금을 함께 건다.
    /// Dependencies: DungeonExit(static OnExitRequested), ItemQuantityDialogUI, GamePauseService, PlayerInput
    /// Events: 없음
    /// </summary>
    public class ExitConfirmUI : MonoBehaviour
    {
        [Tooltip("확인창으로 쓸 대화상자 (가방 패널 밖, 던전 HUD 캔버스에 둔다 — 가방이 닫히면 비활성화되지 않게)")]
        [SerializeField] private ItemQuantityDialogUI _dialog;
        // 던전 HUD에 한글 TMP 폰트가 없어 영어 기본값을 둔다 (폰트가 들어오면 "탈출할까요?" / "탈출")
        [SerializeField] private string _title = "Leave the dungeon?";
        [SerializeField] private string _confirmLabel = "Leave";

        private PlayerInput _playerInput;
        private DungeonExit _pendingExit;
        // 이 뷰가 직접 건 잠금만 푼다 (층 전환 중 DungeonGenerator가 건 입력 잠금을 덮어쓰지 않도록)
        private bool _isLocked;

        private void Awake()
        {
            PlayerInput.OnInputCreated += HandleInputCreated;
            if (_playerInput == null)
            {
                var existing = FindObjectOfType<PlayerInput>();
                if (existing != null) HandleInputCreated(existing);
            }
        }

        private void OnEnable() => DungeonExit.OnExitRequested += HandleExitRequested;

        private void OnDisable()
        {
            DungeonExit.OnExitRequested -= HandleExitRequested;
            ReleaseLock();
        }

        private void OnDestroy()
        {
            PlayerInput.OnInputCreated -= HandleInputCreated;
            ReleaseLock();
        }

        private void HandleInputCreated(PlayerInput input) => _playerInput = input;

        private void HandleExitRequested(DungeonExit exit)
        {
            if (exit == null) return;
            if (_dialog == null)
            {
                // 확인창이 없으면 잠금을 걸지 않고 바로 탈출 (잠금만 걸리고 풀 경로가 없는 상황 방지)
                Debug.LogWarning("[ExitConfirmUI] 확인창(ItemQuantityDialogUI)이 연결되지 않아 바로 탈출합니다.");
                exit.ConfirmExit();
                return;
            }

            _pendingExit = exit;
            AcquireLock();
            _dialog.Open(_title, 1, _confirmLabel, HandleConfirm, HandleCancel);
        }

        private void HandleConfirm(int _)
        {
            DungeonExit exit = _pendingExit;
            _pendingExit = null;
            ReleaseLock();
            if (exit != null) exit.ConfirmExit();
        }

        private void HandleCancel()
        {
            DungeonExit exit = _pendingExit;
            _pendingExit = null;
            ReleaseLock();
            if (exit != null) exit.CancelExit();
        }

        private void AcquireLock()
        {
            if (_isLocked) return;
            _isLocked = true;
            GamePauseService.Request(this);
            if (_playerInput != null) _playerInput.IsInputLocked = true;
        }

        private void ReleaseLock()
        {
            if (!_isLocked) return;
            _isLocked = false;
            GamePauseService.Release(this);
            if (_playerInput != null) _playerInput.IsInputLocked = false;
        }
    }
}
