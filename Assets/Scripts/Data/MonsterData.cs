// File: Assets/Scripts/Data/MonsterData.cs
namespace DimensionLooter.Data
{
    /// <summary>
    /// CSV에서 로드된 몬스터의 능력치 데이터를 담는 순수 C# 모델 클래스입니다.
    /// </summary>
    public class MonsterData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int MaxHealth { get; set; }
        public int AttackPower { get; set; }
        public float AttackSpeed { get; set; }
        public int DropGold { get; set; }
        public int DropExp { get; set; }
    }
}
