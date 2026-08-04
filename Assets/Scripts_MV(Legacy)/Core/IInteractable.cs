// File: Assets/Scripts/Core/IInteractable.cs
using UnityEngine;

namespace DimensionLooter.Core
{
    /// <summary>
    /// 플레이어와 상호작용할 수 있는 맵 오브젝트(보물상자, 제단, 상인 등)가 구현해야 하는 인터페이스입니다.
    /// [Why] 플레이어의 Raycast나 Trigger 로직이 구체적인 상호작용 객체를 알 필요 없이 추상화된 메서드만 호출하게 만듭니다.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// 상호작용을 실행합니다.
        /// </summary>
        /// <param name="interactor">상호작용을 시도한 주체 (보통 플레이어)</param>
        void Interact(GameObject interactor);
    }
}
