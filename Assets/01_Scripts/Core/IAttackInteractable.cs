using UnityEngine;

namespace BagBack100.Core
{
    /// <summary>
    /// Role: 근접·차지 공격을 받으면 피해 대신 상호작용하는 오브젝트(상자 등)의 계약.
    ///       EntityCombat이 타격 대상에서 이 인터페이스를 먼저 확인한다. 누가 때렸는지는 구현 쪽이 판단한다.
    /// Dependencies: 없음
    /// Events: 없음
    /// </summary>
    public interface IAttackInteractable
    {
        void OnAttacked(GameObject attacker);
    }
}
