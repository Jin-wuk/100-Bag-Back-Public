// File: Assets/Scripts/UI/CombatUI.cs
using UnityEngine;
using UnityEngine.UI;
using DimensionLooter.Gameplay.Combat;

namespace DimensionLooter.UI
{
    /// <summary>
    /// 전투 중 발생하는 정보(체력바, 데미지 팝업 등)를 렌더링하는 UI View입니다.
    /// </summary>
    public class CombatUI : MonoBehaviour
    {
        [Header("Player UI")]
        [Tooltip("플레이어 체력 컴포넌트 (MVP 단계이므로 인스펙터에 직접 연결)")]
        [SerializeField] private PlayerHealth _playerHealth;
        
        [Tooltip("플레이어 체력바 슬라이더")]
        [SerializeField] private Slider _playerHpBar;
        
        // 몬스터 체력바나 팝업은 월드 캔버스(World Canvas) 형태로 몬스터 프리팹 자체에 붙거나 
        // PoolManager를 통해 동적으로 위치를 잡아주는 방식으로 구현할 예정입니다.

        private void Start()
        {
            Debug.Assert(_playerHealth != null, "[CombatUI] _playerHealth 할당 누락!", this);
            Debug.Assert(_playerHpBar != null, "[CombatUI] _playerHpBar 할당 누락!", this);
        }

        private void OnEnable()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnHealthChanged += UpdatePlayerHealthBar;
            }
        }

        private void OnDisable()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnHealthChanged -= UpdatePlayerHealthBar;
            }
        }

        private void UpdatePlayerHealthBar(int currentHealth, int maxHealth)
        {
            if (_playerHpBar != null)
            {
                _playerHpBar.value = (float)currentHealth / maxHealth;
            }
        }
    }
}
