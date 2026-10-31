// Role: 게임 전역 enum 정의 (몬스터 등급, 아이템 분류, 장비 부위, 방어구 계열 등). 에셋에 int로 저장되므로 값은 뒤에만 추가한다.
// Dependencies: 없음 / Events: 없음
namespace BagBack100.Data.Core
{
    public enum MonsterType { Normal, Elite, Boss }
    // 에셋에 int로 직렬화되므로 뒤에만 추가. Valuable = 골드 가치가 높은 전리품(금접시 등), 허브에서 팔아 골드로 바꾼다
    public enum ItemCategory { Equipment, Consumable, Material, Key, Valuable }
    // 에셋에 int로 직렬화되므로 기존 값의 순서를 바꾸지 말고 뒤에만 추가할 것. Armor = 갑옷(몸통)
    public enum EquipType { None, Helmet, Armor, Weapon, Ring, Necklace, Backpack, Boots, Earring }
    // 방어구 테크트리 분기: 탐험형(이동·행동 속도) / 방어형(방어·체력) / 기동·전술형(이동 쿨 감소·MP·회피)
    public enum ArmorBranch { Explorer, Heavy, Tactical }
    public enum ItemRarity { Common, Rare, Legendary }
    public enum SkillActivationType { Active, Passive }
    public enum SkillRoleType { Interrupt, Survival, Execution, General }
    public enum GameState { Exploration, Combat }
    public enum AttackPatternType { SingleTile, Cross, ForwardLine }
    public enum WeaponHitboxPattern
    {
        SingleTile,      // 1칸 단일 타겟 (단검 등 기본 무기)
        ForwardLine2,    // 전방 2칸 직선 관통 (창, 찌르기 계열)
        WideSweep3       // 전방 3칸 부채꼴/횡베기 (대검, 해머 계열)
    }
    public enum MonsterMovementStyle
    {
        HeavyChase,   // 묵직한 근거리: 플레이어를 향해 A* 최단 경로 직진 추적
        AgileFlank,   // 날렵한 근거리: 플레이어 정면 축을 피하고 측면/후방 타일로 우회 진입
        RangedKiting  // 원거리 투사체: 적정 사거리(range)를 유지하며 후퇴 및 횡이동(카이팅)
    }
}
