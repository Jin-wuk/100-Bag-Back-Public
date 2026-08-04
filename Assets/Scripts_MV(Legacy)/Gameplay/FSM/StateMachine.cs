// File: Assets/Scripts/Gameplay/FSM/StateMachine.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DimensionLooter.Gameplay.FSM
{
    /// <summary>
    /// 상태(IState) 간의 전환을 관리하는 클래스입니다.
    /// [Why] 상태 객체 생성(new)으로 인한 GC 스파이크를 막기 위해 딕셔너리로 상태를 캐싱합니다.
    /// </summary>
    public class StateMachine
    {
        private IState _currentState;
        
        // 상태 객체를 캐싱하는 딕셔너리
        private Dictionary<Type, IState> _stateDictionary = new Dictionary<Type, IState>();

        /// <summary>
        /// 구동될 상태들을 초기화 단계에서 미리 등록(캐싱)합니다.
        /// </summary>
        public void AddState(IState state)
        {
            Type type = state.GetType();
            if (!_stateDictionary.ContainsKey(type))
            {
                _stateDictionary.Add(type, state);
            }
        }

        /// <summary>
        /// 특정 상태로 전환합니다. 제네릭을 사용하여 타입 안정성을 확보합니다.
        /// </summary>
        public void ChangeState<T>() where T : IState
        {
            Type type = typeof(T);
            if (!_stateDictionary.ContainsKey(type))
            {
                Debug.LogError($"[StateMachine] {type.Name} 상태가 등록되지 않았습니다! AddState로 먼저 등록해주세요.");
                return;
            }

            _currentState?.Exit();
            _currentState = _stateDictionary[type];
            _currentState.Enter();
        }

        /// <summary>
        /// 현재 상태의 로직을 갱신합니다. MonoBehaviour의 Update에서 호출해야 합니다.
        /// </summary>
        public void Update()
        {
            _currentState?.UpdateState();
        }
    }
}
