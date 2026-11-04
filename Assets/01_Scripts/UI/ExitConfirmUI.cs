using UnityEngine;
using BagBack100.Core;
using BagBack100.Data.Core;
using BagBack100.Dungeon;
using BagBack100.Player;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 던전을 떠나는 확인창 뷰. 탈출 포탈("탈출할까요?", 기획 07 Q22)과 귀환석(Q31)의 요청을 받아 확인창을 띄우고,
    ///       떠 있는 동안 게임을 일시정지하고 플레이어 입력을 잠근다. 결과는 요청한 쪽에 커맨드로만 전달한다
    ///       (포탈: ConfirmExit / CancelExit, 귀환석: ConsumableController.ConfirmReturnStone, 취소 시 아무것도 하지 않음).
    ///       일시정지만으로는 PlayerInput이 Tab·퀵슬롯·공격 입력을 그대로 받으므로 입력 잠금을 함께 건다.
    /// Dependencies: DungeonExit(static OnExitRequested), ConsumableController(OnReturnStoneRequested), ItemQuantityDialogUI,
    ///               GamePauseService, PlayerInput
    /// Events: 없음
    /// </summary>
    public class ExitConfirmUI : MonoBehaviour
    {
        [Tooltip("확인창으로 쓸 대화상자 (가방 패널 밖, 던전 HUD 캔버스에 둔다 — 가방이 닫히면 비활성화되지 않게)")]
        [SerializeField] private ItemQuantityDialogUI _dialog;
        // 던전 HUD에 한글 TMP 폰트가 없어 영어 기본값을 둔다 (폰트가 들어오면 "탈출할까요?" / "탈출")
        [SerializeField] private string _title = "Leave the dungeon?";
        [SerializeField] private string _confirmLabel = "Leave";
        [SerializeField] private string _returnStoneTitle = "Use the Return Stone and leave?";

        private PlayerInput _playerInput;
        private ConsumableController _consumables;
        private DungeonExit _pendingExit;
        private RuntimeItem _pendingStone;
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
            ClearPending();
            ReleaseLock();
        }

        private void OnDestroy()
        {
            PlayerInput.OnInputCreated -= HandleInputCreated;
            BindConsumables(null);
            ClearPending();
            ReleaseLock();
        }

        private void HandleInputCreated(PlayerInput input)
        {
            _playerInput = input;
            // 소모품 규칙은 입력과 같은 플레이어 오브젝트에 붙어 있다
            ConsumableController consumables = null;
            if (input != null) input.TryGetComponent(out consumables);
            BindConsumables(consumables);
        }

        private void BindConsumables(ConsumableController consumables)
        {
            if (_consumables == consumables) return;
            if (_consumables != null) _consumables.OnReturnStoneRequested -= HandleReturnStoneRequested;
            _consumables = consumables;
            if (_consumables != null) _consumables.OnReturnStoneRequested += HandleReturnStoneRequested;
        }

        private bool HasPending => _pendingExit != null || _pendingStone != null;

        // 확인창이 밖에서 꺼져도 대기 요청이 남아 이후 요청이 영구히 무시되지 않게 비운다
        private void ClearPending()
        {
            _pendingExit = null;
            _pendingStone = null;
        }

        private void HandleReturnStoneRequested(RuntimeItem stone)
        {
            if (stone == null || HasPending) return;
            if (_dialog == null)
            {
                Debug.LogWarning("[ExitConfirmUI] 확인창(ItemQuantityDialogUI)이 연결되지 않아 귀환석으로 바로 탈출합니다.");
                if (_consumables != null) _consumables.ConfirmReturnStone(stone);
                return;
            }

            _pendingStone = stone;
            AcquireLock();
            _dialog.Open(_returnStoneTitle, 1, _confirmLabel, HandleConfirm, HandleCancel);
        }

        private void HandleExitRequested(DungeonExit exit)
        {
            if (exit == null) return;
            // 다른 확인창이 떠 있는 동안 들어온 요청은 무시한다 (포탈은 가드 때문에 칸을 벗어났다 다시 밟으면 다시 묻는다)
            if (HasPending)
            {
                exit.CancelExit();
                return;
            }
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
            RuntimeItem stone = _pendingStone;
            ClearPending();
            ReleaseLock();
            if (exit != null) exit.ConfirmExit();
            else if (stone != null && _consumables != null) _consumables.ConfirmReturnStone(stone);
        }

        private void HandleCancel()
        {
            DungeonExit exit = _pendingExit;
            ClearPending();
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
