// File: Assets/Scripts/Core/IDamageable.cs
namespace DimensionLooter.Core
{
    /// <summary>
    /// 체력을 가지고 데미지를 받을 수 있는 모든 객체(Player, Monster, 파괴 가능한 사물 등)가 구현해야 하는 인터페이스입니다.
    /// [Why] 공격하는 측에서 대상의 구체적인 클래스(PlayerHealth, MonsterHealth 등)를 몰라도 데미지를 가할 수 있도록 결합도를 낮춥니다.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// 대상에게 데미지를 가합니다.
        /// </summary>
        /// <param name="amount">피해량</param>
        void TakeDamage(int amount);

        /// <summary>
        /// 대상이 살아있는지 여부를 반환합니다.
        /// </summary>
        bool IsAlive { get; }
    }
}
