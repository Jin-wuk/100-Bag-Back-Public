using System;
using System.Collections.Generic;
using UnityEngine;
using BagBack100.Data.Core;

namespace BagBack100.Dungeon.Data
{
    /// <summary>드랍 등급 비율 한 벌 (일반/희귀/전설). 합이 100이 아니어도 상대 비율로 쓴다.</summary>
    [Serializable]
    public struct RarityWeights
    {
        [SerializeField, Min(0f)] private float _common;
        [SerializeField, Min(0f)] private float _rare;
        [SerializeField, Min(0f)] private float _legendary;

        public RarityWeights(float common, float rare, float legendary)
        {
            _common = common;
            _rare = rare;
            _legendary = legendary;
        }

        public float Get(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return Mathf.Max(0f, _common);
                case ItemRarity.Rare: return Mathf.Max(0f, _rare);
                case ItemRarity.Legendary: return Mathf.Max(0f, _legendary);
                default: return 0f;
            }
        }

        public static RarityWeights Lerp(RarityWeights from, RarityWeights to, float t)
        {
            return new RarityWeights(
                Mathf.Lerp(from._common, to._common, t),
                Mathf.Lerp(from._rare, to._rare, t),
                Mathf.Lerp(from._legendary, to._legendary, t));
        }
    }

    /// <summary>
    /// 1~9층 중 한 층의 규칙 한 줄. 5층은 보스층이라 탈출구(100%)·상자(없음) 값은 쓰지 않고 몬스터 수(잡몹)만 쓴다.
    /// </summary>
    [Serializable]
    public struct FloorRule
    {
        [Tooltip("층 클리어 순간 탈출구가 나올 확률 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float _exitChance;
        [Tooltip("상자 굴림 횟수 (B안: 굴림마다 확률 판정 → 당첨 시 후보 중 1곳)")]
        [SerializeField, Min(0)] private int _chestRolls;
        [Tooltip("상자 굴림 1회의 당첨 확률 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float _chestChance;
        [Tooltip("이 층에 놓을 몬스터 수. 층 디자인의 몬스터 구역(DungeonZone)에 유효 칸 수 비율로 나눠 놓는다. 간격 규칙 때문에 덜 놓일 수 있다")]
        [SerializeField, Min(0)] private int _monsterCount;

        public float ExitChance => _exitChance;
        public int ChestRolls => _chestRolls;
        public float ChestChance => _chestChance;
        public int MonsterCount => _monsterCount;

        public FloorRule(float exitChance, int chestRolls, float chestChance, int monsterCount)
        {
            _exitChance = Mathf.Clamp01(exitChance);
            _chestRolls = Mathf.Max(0, chestRolls);
            _chestChance = Mathf.Clamp01(chestChance);
            _monsterCount = Mathf.Max(0, monsterCount);
        }
    }

    /// <summary>
    /// Role: 지역 1곳의 층 구성 정적 데이터 (디자인 풀, 일반 몬스터 풀, 보스층 디자인, 층별 몬스터 배율·드랍 등급 비율, 1~9층 탈출구·상자 표, 상자 드랍 풀, 디버그 옵션).
    /// Dependencies: FloorDesignSO
    /// Events: 없음
    /// </summary>
    [CreateAssetMenu(fileName = "NewRegionConfig", menuName = "BagBack100/Floor/RegionConfig")]
    public class RegionConfigSO : ScriptableObject
    {
        public const int RuleFloorCount = 9;
        public const int MidBossFloor = 5;
        public const int BossFloor = 10;
        public const int DefaultChestDropPoolId = 1000;

        [Header("Region Identity")]
        [SerializeField] private int _regionId = 1;
        [SerializeField] private string _regionName = "지역 1";

        [Header("Design Pools")]
        [Tooltip("1~5층 구간 풀 (5층은 보스층 고정이라 실제 추첨은 1~4층)")]
        [SerializeField] private List<FloorDesignSO> _earlyPool = new List<FloorDesignSO>();
        [Tooltip("6~9층 구간 풀")]
        [SerializeField] private List<FloorDesignSO> _latePool = new List<FloorDesignSO>();
        [Tooltip("5층 중간 보스층 디자인 (위층 계단 필수)")]
        [SerializeField] private FloorDesignSO _midBossFloor;
        [Tooltip("10층 보스층 디자인 (위층 계단은 있어도 무시)")]
        [SerializeField] private FloorDesignSO _bossFloor;

        [Header("Monsters")]
        [Tooltip("이 지역 일반 스폰 몬스터 프리팹 풀. 층마다 MonsterDataSO의 출현 층 범위로 거른다. 구역 전용 풀(DungeonZone)이 있으면 그 풀을 대신 거른다")]
        [SerializeField] private List<GameObject> _monsterPool = new List<GameObject>();

        [Header("Bosses")]
        [Tooltip("5층 BossSpawn 마커에 놓을 중간 보스 프리팹 (MonsterDataSO.type = Boss). 비우면 5층은 전원 처치 규칙")]
        [SerializeField] private GameObject _midBossPrefab;
        [Tooltip("10층 BossSpawn 마커에 놓을 보스 프리팹 (MonsterDataSO.type = Boss). 비우면 10층은 전원 처치 규칙")]
        [SerializeField] private GameObject _bossPrefab;

        [Header("Floor Rules (index 0 = 1F, 9칸)")]
        [SerializeField] private FloorRule[] _floorRules = new FloorRule[RuleFloorCount];

        [Header("Floor Scaling (유저 결정 2026-11-05, 플레이 테스트로 조정)")]
        [Tooltip("층이 1 오를 때마다 더하는 몬스터 HP 배율 (1층 1.0)")]
        [SerializeField, Min(0f)] private float _hpPerFloor = 0.10f;
        [Tooltip("층이 1 오를 때마다 더하는 몬스터 ATK 배율 (1층 1.0)")]
        [SerializeField, Min(0f)] private float _atkPerFloor = 0.05f;
        [Tooltip("1층 드랍 등급 비율 (일반/희귀/전설)")]
        [SerializeField] private RarityWeights _firstFloorRarity = new RarityWeights(70f, 25f, 5f);
        [Tooltip("9층 드랍 등급 비율. 1~9층은 선형 보간, 10층은 이 값")]
        [SerializeField] private RarityWeights _lastRuleFloorRarity = new RarityWeights(50f, 38f, 12f);

        [Header("Chest")]
        [Tooltip("이 지역 상자를 열었을 때 쓰는 DropPoolSO의 dropPoolID")]
        [SerializeField] private int _chestDropPoolId = DefaultChestDropPoolId;

        [Header("Debug")]
        [Tooltip("개발용: 한 판 안 디자인 중복 금지를 끈다 (회색 박스 2장으로 테스트할 때)")]
        [SerializeField] private bool _debugAllowDuplicateDesigns;

        public int RegionId => _regionId;
        public string RegionName => _regionName;
        public IReadOnlyList<FloorDesignSO> EarlyPool => _earlyPool;
        public IReadOnlyList<FloorDesignSO> LatePool => _latePool;
        public FloorDesignSO MidBossFloorDesign => _midBossFloor;
        public FloorDesignSO BossFloorDesign => _bossFloor;
        public IReadOnlyList<GameObject> MonsterPool => _monsterPool;
        public GameObject MidBossPrefab => _midBossPrefab;
        public GameObject BossPrefab => _bossPrefab;

        /// <summary>보스층의 보스 프리팹. 보스층이 아니거나 지정되지 않았으면 null.</summary>
        public GameObject GetBossPrefab(int floor)
        {
            if (floor == MidBossFloor) return _midBossPrefab;
            if (floor == BossFloor) return _bossPrefab;
            return null;
        }
        public bool DebugAllowDuplicateDesigns => _debugAllowDuplicateDesigns;
        public int ChestDropPoolId => _chestDropPoolId;

        /// <summary>5층 중간 보스층·10층 보스층 여부 (귀환석 사용 불가 등 보스층 규칙 판정용).</summary>
        public static bool IsBossFloor(int floor) => floor == MidBossFloor || floor == BossFloor;

        public float GetHpMultiplier(int floor) => 1f + _hpPerFloor * Mathf.Max(0, floor - 1);
        public float GetAtkMultiplier(int floor) => 1f + _atkPerFloor * Mathf.Max(0, floor - 1);

        public RarityWeights GetRarityWeights(int floor)
        {
            float t = Mathf.Clamp01((floor - 1f) / (RuleFloorCount - 1f));
            return RarityWeights.Lerp(_firstFloorRarity, _lastRuleFloorRarity, t);
        }

        /// <summary>1~9층 규칙. 범위 밖(10층 등)은 기본값(전부 0).</summary>
        public FloorRule GetRule(int floor)
        {
            int index = floor - 1;
            if (_floorRules == null || index < 0 || index >= _floorRules.Length) return default;
            return _floorRules[index];
        }

        /// <summary>
        /// 구간 디자인이 감당해야 하는 최대 상자 굴림 횟수. 디자인의 상자 후보 수가 이 값 이상이어야 한다.
        /// Early는 실제 추첨 층(1~4), Late는 6~9층 기준이며 보스층은 상자가 없다.
        /// </summary>
        public int GetMaxChestRolls(FloorBand band)
        {
            int from, to;
            switch (band)
            {
                case FloorBand.Early: from = 1; to = MidBossFloor - 1; break;
                case FloorBand.Late:  from = MidBossFloor + 1; to = RuleFloorCount; break;
                default: return 0;
            }

            int max = 0;
            for (int floor = from; floor <= to; floor++)
            {
                max = Mathf.Max(max, GetRule(floor).ChestRolls);
            }
            return max;
        }

        private void OnValidate()
        {
            if (_floorRules == null || _floorRules.Length != RuleFloorCount)
            {
                Array.Resize(ref _floorRules, RuleFloorCount);
            }
        }

#if UNITY_EDITOR
        /// <summary>회색 박스 생성 메뉴 전용 초기값 주입.</summary>
        public void EditorInitialize(
            int regionId, string regionName,
            List<FloorDesignSO> earlyPool, List<FloorDesignSO> latePool,
            FloorDesignSO midBossFloor, FloorDesignSO bossFloor,
            FloorRule[] floorRules, bool debugAllowDuplicateDesigns, int chestDropPoolId = DefaultChestDropPoolId)
        {
            _chestDropPoolId = chestDropPoolId;
            _regionId = regionId;
            _regionName = regionName;
            _earlyPool = new List<FloorDesignSO>(earlyPool);
            _latePool = new List<FloorDesignSO>(latePool);
            _midBossFloor = midBossFloor;
            _bossFloor = bossFloor;
            _floorRules = (FloorRule[])floorRules.Clone();
            _debugAllowDuplicateDesigns = debugAllowDuplicateDesigns;
            OnValidate();
        }
#endif
    }
}
