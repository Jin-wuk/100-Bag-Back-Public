// File: Assets/Scripts/Data/InventoryItem.cs
namespace DimensionLooter.Data
{
    /// <summary>
    /// 인벤토리에 실제로 배치된 아이템의 상태 데이터를 관리하는 모델입니다.
    /// [Why] 정적 데이터인 ItemData와 분리하여, 회전 및 위치 등 동적인 인게임 상태를 관리하기 위함입니다.
    /// </summary>
    public class InventoryItem
    {
        public ItemData BaseData { get; private set; }
        public int GridX { get; set; }
        public int GridY { get; set; }
        public bool IsRotated { get; set; }

        public InventoryItem(ItemData data)
        {
            BaseData = data;
        }

        /// <summary>
        /// 회전 여부를 반영한 실제 타일 차지 가로 길이
        /// </summary>
        public int GetCurrentWidth()
        {
            return IsRotated ? BaseData.Height : BaseData.Width;
        }

        /// <summary>
        /// 회전 여부를 반영한 실제 타일 차지 세로 길이
        /// </summary>
        public int GetCurrentHeight()
        {
            return IsRotated ? BaseData.Width : BaseData.Height;
        }
    }
}
