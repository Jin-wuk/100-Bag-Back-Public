// File: Assets/Scripts/Core/Define.cs
namespace DimensionLooter.Core
{
    /// <summary>
    /// 게임 내에서 사용하는 모든 상수 및 열거형(Enum)을 관리하는 클래스입니다.
    /// [Why] 매직 스트링(Magic String)을 방지하여 오타로 인한 런타임 에러를 막습니다.
    /// </summary>
    public static class Define
    {
        /// <summary>
        /// 게임의 전반적인 상태를 정의합니다. (FSM과 연동)
        /// </summary>
        public enum GameState
        {
            None,
            Lobby,          // 기지 상태
            Exploration,    // 던전 탐험 상태
            Combat,         // 1:1 오토배틀 교전 상태
            Result          // 던전 탈출 또는 사망 결과 상태
        }

        /// <summary>
        /// 아이템의 논리적 타입을 정의합니다.
        /// </summary>
        public enum ItemType
        {
            Weapon,
            Armor,
            Backpack,
            Consumable,
            ReturnStone,    // 귀환석
            Misc            // 재화/잡템
        }

        // [Why] CSV 리소스 경로를 상수로 관리하여, 추후 경로가 변경되어도 여기서만 수정하면 되도록 합니다.
        public const string CSV_PATH_ITEM_STATS = "CSV/Item_Stats";
        public const string CSV_PATH_MONSTER_STATS = "CSV/Monster_Stats";
        public const string CSV_PATH_SKILL_STATS = "CSV/Skill_Stats";
    }
}
