using UnityEngine;
using UnityEngine.UI;
using BagBack100.Player;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 던전 HUD의 대시 쿨타임 표시. 남은 쿨타임 비율을 Filled 이미지에 채운다(퀵슬롯 쿨타임 표시와 같은 방식).
    /// Dependencies: PlayerInput(생성 알림), PlayerController(DashCooldownNormalized)
    /// Events: 없음
    /// </summary>
    public class DashCooldownUI : MonoBehaviour
    {
        [Tooltip("쿨타임 동안 채워지는 Filled 이미지 (fillAmount 1 → 0)")]
        [SerializeField] private Image _cooldownOverlay;

        private PlayerController _player;

        private void Awake()
        {
            if (_cooldownOverlay == null)
            {
                Debug.LogError("[DashCooldownUI] _cooldownOverlay가 비어 있습니다.", this);
            }

            PlayerInput.OnInputCreated += HandleInputCreated;
            var existing = FindObjectOfType<PlayerInput>();
            if (existing != null) HandleInputCreated(existing);
        }

        private void OnDestroy()
        {
            PlayerInput.OnInputCreated -= HandleInputCreated;
        }

        private void HandleInputCreated(PlayerInput input)
        {
            if (input != null) input.TryGetComponent(out _player);
        }

        private void Update()
        {
            if (_player == null || _cooldownOverlay == null) return;
            _cooldownOverlay.fillAmount = _player.DashCooldownNormalized;
        }
    }
}
