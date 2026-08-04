// File: Assets/Scripts/Gameplay/FSM/States/IdleState.cs
using UnityEngine;

namespace DimensionLooter.Gameplay.FSM.States
{
    /// <summary>
    /// 캐릭터(플레이어/몬스터)의 기본 대기 상태입니다.
    /// [Why] 독립적인 클래스로 분리하여 다른 상태와 로직이 꼬이는 것을 방지합니다.
    /// </summary>
    public class IdleState : IState
    {
        public void Enter()
        {
            Debug.Log("[IdleState] 대기 상태 진입", null);
        }

        public void UpdateState()
        {
            // TODO: 탐색 대상(적)이 범위 내에 들어왔는지 감지하는 로직
            // 적 발견 시 stateMachine.ChangeState<CombatState>() 호출
        }

        public void Exit()
        {
            Debug.Log("[IdleState] 대기 상태 종료", null);
        }
    }
}
