// File: Assets/Scripts/Managers/DataManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using DimensionLooter.Core;
using DimensionLooter.Data;

namespace DimensionLooter.Managers
{
    /// <summary>
    /// 게임 내 모든 CSV 정적 데이터를 로드하고 딕셔너리로 캐싱하여 빠른 접근을 돕는 매니저입니다.
    /// [Why] 런타임 중 데이터를 찾기 위해 매번 로드하지 않고 O(1) 탐색 속도를 보장하기 위함입니다.
    /// </summary>
    public class DataManager : MonoBehaviour
    {
        // MVP 단계용 싱글톤
        public static DataManager Instance { get; private set; }

        public Dictionary<int, ItemData> ItemDict { get; private set; } = new Dictionary<int, ItemData>();
        public Dictionary<int, MonsterData> MonsterDict { get; private set; } = new Dictionary<int, MonsterData>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Init();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Init()
        {
            LoadItemData();
            LoadMonsterData();
        }

        private void LoadItemData()
        {
            // [Todo] 실제 CSV 파싱 로직 구현 (String.Split 기반 자체 파서)
            // 현재는 뼈대만 작성하며, CSV 파일 양식 확정 후 구체적인 파싱 로직을 채워넣습니다.
            TextAsset csvFile = Resources.Load<TextAsset>(Define.CSV_PATH_ITEM_STATS);
            if (csvFile == null)
            {
                Debug.LogError($"[DataManager] 아이템 CSV 파일을 찾을 수 없습니다: {Define.CSV_PATH_ITEM_STATS}", this);
                return;
            }

            // 임시 더미 데이터 삽입 (파서 완성 전까지의 테스트용)
            ItemDict.Add(1, new ItemData { Id = 1, Name = "초보자의 검", Type = Define.ItemType.Weapon, AttackPower = 5 });
            Debug.Log($"[DataManager] 아이템 데이터 로드 완료! (더미 로직 포함)");
        }

        private void LoadMonsterData()
        {
            TextAsset csvFile = Resources.Load<TextAsset>(Define.CSV_PATH_MONSTER_STATS);
            if (csvFile == null)
            {
                Debug.LogError($"[DataManager] 몬스터 CSV 파일을 찾을 수 없습니다: {Define.CSV_PATH_MONSTER_STATS}", this);
                return;
            }

            MonsterDict.Add(1, new MonsterData { Id = 1, Name = "슬라임", MaxHealth = 20, AttackPower = 2 });
            Debug.Log($"[DataManager] 몬스터 데이터 로드 완료! (더미 로직 포함)");
        }
    }
}
