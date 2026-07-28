// File: Assets/Scripts/Data/ItemData.cs
namespace DimensionLooter.Data
{
    /// <summary>
    /// CSV에서 로드된 단일 아이템의 능력치 데이터를 담는 순수 C# 모델 클래스입니다.
    /// [Why] ScriptableObject를 쓰지 않고 POCO를 사용하여 런타임 메모리 할당 및 직렬화 충돌을 방지합니다.
    /// </summary>
    public class ItemData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DimensionLooter.Core.Define.ItemType Type { get; set; }
        public int AttackPower { get; set; }
        public int DefensePower { get; set; }
        public int Price { get; set; }
        
        // 추가 기획 속성들은 CSV 컬럼명에 맞춰 나중에 확장 가능합니다.
    }
}
