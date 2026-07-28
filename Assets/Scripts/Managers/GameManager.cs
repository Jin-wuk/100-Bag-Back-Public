// File: Assets/Scripts/Managers/GameManager.cs
using UnityEngine;
using DimensionLooter.Core;

namespace DimensionLooter.Managers
{
    /// <summary>
    /// 게임의 전반적인 상태(로비, 탐험, 전투)를 통제하는 최상위 매니저입니다.
    /// [Why] 싱글톤으로 구성하여 씬이 변경되어도 게임의 생명주기를 잃지 않도록 합니다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("State Settings")]
        [Tooltip("현재 게임의 진행 상태를 나타냅니다.")]
        [SerializeField] private Define.GameState _currentState = Define.GameState.None;

        public Define.GameState CurrentState => _currentState;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            ChangeState(Define.GameState.Lobby);
        }

        /// <summary>
        /// 게임의 전반적인 상태를 변경합니다.
        /// </summary>
        public void ChangeState(Define.GameState newState)
        {
            _currentState = newState;
            Debug.Log($"[GameManager] 게임 상태 변경: {_currentState}", this);
            
            // TODO: 추후 옵저버 패턴(Action)을 통해 상태 변경 이벤트를 뿌리고 UI들이 반응하도록 구현
        }
    }
}
