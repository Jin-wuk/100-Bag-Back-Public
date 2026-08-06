using UnityEngine;

namespace DimensionLooter.Player
{
    public enum PlayerState
    {
        Idle,
        Moving
    }

    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerController : MonoBehaviour
    {
        public PlayerState CurrentState { get; private set; } = PlayerState.Idle;

        private PlayerInput _input;
        private PlayerMovement _movement;

        private void Awake()
        {
            _input = GetComponent<PlayerInput>();
            _movement = GetComponent<PlayerMovement>();
            
            // FSM 및 이벤트 바인딩 (DI & Event-driven)
            _input.OnTileClicked += HandleTileClicked;
            _movement.OnMovementFinished += HandleMovementFinished;
        }

        private void OnDestroy()
        {
            if (_input != null)
                _input.OnTileClicked -= HandleTileClicked;
            if (_movement != null)
                _movement.OnMovementFinished -= HandleMovementFinished;
        }

        private void HandleTileClicked(Vector3 targetPos)
        {
            // 이동 명령 하달
            _movement.MoveTo(targetPos);
            CurrentState = PlayerState.Moving;
        }

        private void HandleMovementFinished()
        {
            CurrentState = PlayerState.Idle;
        }
    }
}
