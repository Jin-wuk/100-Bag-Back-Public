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

        public Dictionary<string, ItemData> ItemDict { get; private set; } = new Dictionary<string, ItemData>();
        public Dictionary<string, MonsterData> MonsterDict { get; private set; } = new Dictionary<string, MonsterData>();
        public Dictionary<string, SkillData> SkillDict { get; private set; } = new Dictionary<string, SkillData>();

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
            LoadSkillData();
        }

        private void LoadItemData()
        {
            TextAsset csvFile = Resources.Load<TextAsset>(Define.CSV_PATH_ITEM_STATS);
            if (csvFile == null)
            {
                Debug.LogError($"[DataManager] 아이템 CSV 파일을 찾을 수 없습니다: {Define.CSV_PATH_ITEM_STATS}", this);
                return;
            }

            string[] lines = csvFile.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < lines.Length; i++)
            {
                string[] cols = lines[i].Split(',');
                if (cols.Length < 9) continue;

                ItemData item = new ItemData();
                item.Id = cols[0];
                item.Name = cols[1];
                
                if (Enum.TryParse(cols[2], out Define.ItemType itemType))
                    item.Type = itemType;
                else
                    item.Type = Define.ItemType.Misc;

                int.TryParse(cols[3], out int width);
                item.Width = width;
                
                int.TryParse(cols[4], out int height);
                item.Height = height;

                string statType = cols[5];
                int.TryParse(cols[6], out int statValue);
                if (statType == "ATK") item.AttackPower = statValue;
                else if (statType == "DEF") item.DefensePower = statValue;
                else if (statType == "Heal_HP") item.HealHpAmount = statValue;
                else if (statType == "Heal_MP") item.HealMpAmount = statValue;

                int.TryParse(cols[7], out int price);
                item.Price = price;
                
                if (cols.Length > 8)
                {
                    item.PossibleSkills = cols[8];
                }
                
                if (cols.Length > 9)
                {
                    item.Description = cols[9];
                }

                ItemDict[item.Id] = item;
            }

            Debug.Log($"[DataManager] 아이템 데이터 로드 완료! (총 {ItemDict.Count}개)");
        }

        private void LoadMonsterData()
        {
            TextAsset csvFile = Resources.Load<TextAsset>(Define.CSV_PATH_MONSTER_STATS);
            if (csvFile == null)
            {
                Debug.LogError($"[DataManager] 몬스터 CSV 파일을 찾을 수 없습니다: {Define.CSV_PATH_MONSTER_STATS}", this);
                return;
            }

            string[] lines = csvFile.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < lines.Length; i++)
            {
                string[] cols = lines[i].Split(',');
                if (cols.Length < 10) continue;

                MonsterData mob = new MonsterData();
                mob.Id = cols[0];
                mob.Name = cols[1];
                mob.Type = cols[2];
                
                int.TryParse(cols[3], out int hp);
                mob.MaxHealth = hp;
                
                int.TryParse(cols[4], out int atk);
                mob.AttackPower = atk;
                
                int.TryParse(cols[5], out int def);
                mob.DefensePower = def;
                
                int.TryParse(cols[6], out int heavyAtk);
                mob.HeavyAttackPower = heavyAtk;
                
                int.TryParse(cols[7], out int exp);
                mob.DropExp = exp;
                
                int.TryParse(cols[8], out int gold);
                mob.DropGold = gold;
                
                mob.Description = cols[9];

                MonsterDict[mob.Id] = mob;
            }

            Debug.Log($"[DataManager] 몬스터 데이터 로드 완료! (총 {MonsterDict.Count}개)");
        }

        private void LoadSkillData()
        {
            TextAsset csvFile = Resources.Load<TextAsset>(Define.CSV_PATH_SKILL_STATS);
            if (csvFile == null)
            {
                Debug.LogError($"[DataManager] 스킬 CSV 파일을 찾을 수 없습니다: {Define.CSV_PATH_SKILL_STATS}", this);
                return;
            }

            string[] lines = csvFile.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < lines.Length; i++)
            {
                string[] cols = lines[i].Split(',');
                if (cols.Length < 9) continue;

                SkillData skill = new SkillData();
                skill.Id = cols[0];
                skill.Name = cols[1];
                
                if (Enum.TryParse(cols[2], out Define.SkillType skillType))
                    skill.Type = skillType;
                
                int.TryParse(cols[3], out int mpCost);
                skill.MpCost = mpCost;
                
                float.TryParse(cols[4], out float cooldown);
                skill.Cooldown = cooldown;
                
                if (Enum.TryParse(cols[5], out Define.SkillEffectType effectType))
                    skill.EffectType = effectType;
                else
                    skill.EffectType = Define.SkillEffectType.None;
                
                int.TryParse(cols[6], out int effectValue);
                skill.EffectValue = effectValue;
                
                float.TryParse(cols[7], out float duration);
                skill.Duration = duration;
                
                skill.Description = cols[8];

                SkillDict[skill.Id] = skill;
            }

            Debug.Log($"[DataManager] 스킬 데이터 로드 완료! (총 {SkillDict.Count}개)");
        }
    }
}
