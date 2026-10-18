namespace BagBack100.Data.Core
{
    public enum MonsterType { Normal, Elite, Boss }
    public enum ItemCategory { Equipment, Consumable, Material, Key }
    public enum EquipType { None, Helmet, Armor, Weapon, Ring, Necklace, Backpack }
    public enum ItemRarity { Common, Rare, Legendary }
    public enum SkillActivationType { Active, Passive }
    public enum SkillRoleType { Interrupt, Survival, Execution, General }
    public enum GameState { Exploration, Combat }
    public enum AttackPatternType { SingleTile, Cross, ForwardLine }
    public enum MonsterMovementStyle
    {
        HeavyChase,   // 묵직한 근거리: 플레이어를 향해 A* 최단 경로 직진 추적
        AgileFlank,   // 날렵한 근거리: 플레이어 정면 축을 피하고 측면/후방 타일로 우회 진입
        RangedKiting  // 원거리 투사체: 적정 사거리(range)를 유지하며 후퇴 및 횡이동(카이팅)
    }
}
