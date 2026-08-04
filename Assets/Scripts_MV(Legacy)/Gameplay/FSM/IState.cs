// File: Assets/Scripts/Gameplay/FSM/IState.cs
namespace DimensionLooter.Gameplay.FSM
{
    /// <summary>
    /// 모든 FSM 상태가 구현해야 하는 인터페이스입니다.
    /// [Why] 각 상태를 독립적인 클래스로 분리하여(SRP 준수) 거대한 switch문을 방지하기 위함입니다.
    /// </summary>
    public interface IState
    {
        /// <summary>
        /// 상태에 진입할 때 1회 호출됩니다.
        /// </summary>
        void Enter();

        /// <summary>
        /// 상태가 활성화되어 있는 동안 매 프레임 호출됩니다.
        /// </summary>
        void UpdateState();

        /// <summary>
        /// 상태를 벗어날 때 1회 호출됩니다.
        /// </summary>
        void Exit();
    }
}
