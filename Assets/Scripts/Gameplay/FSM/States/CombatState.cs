// File: Assets/Scripts/Gameplay/FSM/States/CombatState.cs
using UnityEngine;

namespace DimensionLooter.Gameplay.FSM.States
{
    /// <summary>
    /// 캐릭터가 적과 교전 중인 상태입니다. (오토 배틀 로직 수행)
    /// </summary>
    public class CombatState : IState
    {
        private float _attackCooldown;
        private float _lastAttackTime;

        public void Enter()
        {
            Debug.Log("[CombatState] 교전 상태 진입 (자동 전투 시작)", null);
            _attackCooldown = 1.5f; // 임시 스탯 연동 전 하드코딩
            _lastAttackTime = Time.time;
        }

        public void UpdateState()
        {
            if (Time.time - _lastAttackTime >= _attackCooldown)
            {
                PerformAttack();
                _lastAttackTime = Time.time;
            }
        }

        private void PerformAttack()
        {
            Debug.Log("[CombatState] 앗앗! 적을 공격합니다!", null);
            
            // TODO: 캐싱된 타겟(Target)에게 IDamageable.TakeDamage 호출 
            // 타겟 처치 시 stateMachine.ChangeState<IdleState>() 로 복귀
        }

        public void Exit()
        {
            Debug.Log("[CombatState] 교전 상태 종료", null);
        }
    }
}
