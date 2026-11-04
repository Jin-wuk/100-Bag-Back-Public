using UnityEngine;
using BagBack100.Player;
using BagBack100.Core;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 던전 가방(인벤토리·장비·보상) 패널 열기/닫기. 열려 있는 동안 이동 차단과 게임 일시정지를 요청한다.
    ///       전투 중에는 가방을 열지 않는다(일시정지로 숨 돌리기 방지, 보안 파우치 이동도 비전투로 제한 — 기획 07 Q16).
    ///       소모품 사용 실패 사유(전투 중 귀환석, 포션 쿨타임 등)도 같은 HUD 토스트로 띄운다.
    /// Dependencies: PlayerInput, PlayerCombatState, ConsumableController(OnUseFailed), GamePauseService, UIToast
    /// Events: 없음
    /// </summary>
    public class DungeonUIManager : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject _inventoryScreenRoot;
        [Tooltip("상자 오픈 등 특정 상황에서만 켜질 보상 패널 (루트의 자식이어야 함)")]
        [SerializeField] private GameObject _rewardPanel;

        [Header("Combat Lock")]
        [Tooltip("전투 중 가방을 열려고 할 때 안내할 토스트 (가방 패널 밖 HUD 캔버스의 UIToast)")]
        [SerializeField] private UIToast _toast;
        [SerializeField] private string _combatLockedMessage = "Can't open the bag during combat.";

        private PlayerInput _playerInput;
        private PlayerCombatState _combatState;
        private ConsumableController _consumables;

        private void Awake()
        {
            PlayerInput.OnInputCreated += OnInputCreated;
            
            if (_playerInput == null)
            {
                var existing = FindObjectOfType<PlayerInput>();
                if (existing != null) OnInputCreated(existing);
            }

            if (_inventoryScreenRoot != null)
                _inventoryScreenRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            PlayerInput.OnInputCreated -= OnInputCreated;

            if (_playerInput != null)
            {
                _playerInput.OnInventoryToggle -= ToggleInventoryScreen;
            }
            BindConsumables(null);
        }

        private void OnInputCreated(PlayerInput input)
        {
            if (_playerInput != null)
            {
                _playerInput.OnInventoryToggle -= ToggleInventoryScreen;
            }
            _playerInput = input;
            _combatState = null;
            if (_playerInput != null)
            {
                _playerInput.OnInventoryToggle += ToggleInventoryScreen;
                // 전투 상태는 입력과 같은 플레이어 오브젝트에 붙어 있다
                _playerInput.TryGetComponent(out _combatState);
            }

            ConsumableController consumables = null;
            if (_playerInput != null) _playerInput.TryGetComponent(out consumables);
            BindConsumables(consumables);
        }

        private void BindConsumables(ConsumableController consumables)
        {
            if (_consumables == consumables) return;
            if (_consumables != null) _consumables.OnUseFailed -= ShowToast;
            _consumables = consumables;
            if (_consumables != null) _consumables.OnUseFailed += ShowToast;
        }

        private void ShowToast(string message)
        {
            if (_toast != null) _toast.Show(message);
        }

        private bool IsOpenBlockedByCombat()
        {
            if (_combatState == null || !_combatState.IsInCombat) return false;
            ShowToast(_combatLockedMessage);
            return true;
        }

        private void Update()
        {
            // 떠나기 확인창·층 전환 중(입력 잠금)에는 Esc로 확인창 뒤의 가방만 닫히지 않게 한다
            if (_playerInput != null && _playerInput.IsInputLocked) return;

            if (_inventoryScreenRoot != null && _inventoryScreenRoot.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseInventoryScreen();
            }
        }

        private void ToggleInventoryScreen()
        {
            if (_inventoryScreenRoot == null) return;

            bool open = !_inventoryScreenRoot.activeSelf;
            if (open && IsOpenBlockedByCombat()) return;

            // 순수하게 'Tab' 키로 인벤토리를 열 때는 보상 패널을 끔
            if (open && _rewardPanel != null)
            {
                _rewardPanel.SetActive(false);
            }

            SetInventoryOpen(open);
        }

        public void CloseInventoryScreen()
        {
            SetInventoryOpen(false);
        }

        /// <summary>
        /// 추후 전투 종료나 상자 오픈 시 호출할 메서드.
        /// 3단 패널(장비, 가방, 보상)을 모두 활성화하여 띄웁니다.
        /// </summary>
        public void OpenRewardScreen()
        {
            // 보상 패널만 켜진 채 남지 않도록 패널을 켜기 전에 검사한다
            if (IsOpenBlockedByCombat()) return;

            if (_rewardPanel != null)
            {
                _rewardPanel.SetActive(true);
            }
            SetInventoryOpen(true);
        }

        /// <summary>
        /// 가방 패널의 열림/닫힘을 바꾸는 유일한 경로. 여는 경로(Tab, 보상)와 닫는 경로(Tab, Esc)가 갈라지면
        /// 어느 한쪽에서 일시정지 해제를 빠뜨려 게임이 멈춘 채 남을 수 있어 여기로 모았다.
        /// </summary>
        private void SetInventoryOpen(bool open)
        {
            if (_inventoryScreenRoot == null) return;

            _inventoryScreenRoot.SetActive(open);

            if (_playerInput != null)
            {
                _playerInput.IsMovementBlocked = open;
            }

            if (open) GamePauseService.Request(this);
            else GamePauseService.Release(this);
        }

        private void OnDisable()
        {
            GamePauseService.Release(this);
        }
    }
}
