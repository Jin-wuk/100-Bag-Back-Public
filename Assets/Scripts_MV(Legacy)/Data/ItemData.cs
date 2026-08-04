// File: Assets/Scripts/Data/ItemData.cs
namespace DimensionLooter.Data
{
    /// <summary>
    /// CSV에서 로드된 단일 아이템의 능력치 데이터를 담는 순수 C# 모델 클래스입니다.
    /// [Why] ScriptableObject를 쓰지 않고 POCO를 사용하여 런타임 메모리 할당 및 직렬화 충돌을 방지합니다.
    /// </summary>
    public class ItemData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DimensionLooter.Core.Define.ItemType Type { get; set; }
        public int AttackPower { get; set; }
        public int DefensePower { get; set; }
        public int HealHpAmount { get; set; }
        public int HealMpAmount { get; set; }
        public int Price { get; set; }
        
        // 인벤토리(테트리스)에서 차지하는 타일 크기
        public int Width { get; set; } = 1;
        public int Height { get; set; } = 1;
        
        // 가방(Backpack) 아이템일 경우, 장착 시 제공하는 인벤토리 타일 크기
        public int BagWidth { get; set; } = 0;
        public int BagHeight { get; set; } = 0;
        
        public string PossibleSkills { get; set; }
        
        public string Description { get; set; }
    }
}
