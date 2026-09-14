//using UnityEngine;
//using UnityEditor;
//using System;
//using System.IO;
//using System.Collections.Generic;
//using System.Text.RegularExpressions;
//using DimensionLooter.Data.Core;
//using DimensionLooter.Data.Models;

//public class CSVImporterWindow : EditorWindow
//{
//    public TextAsset monsterCSV;
//    public TextAsset itemCSV;
//    public TextAsset skillCSV;
//    public TextAsset dropPoolCSV;
//    public TextAsset itemSkillCSV;
//    public TextAsset questCSV;
//    public TextAsset heatCSV;

//    [MenuItem("DimensionLooter/CSV Importer")]
//    public static void ShowWindow()
//    {
//        GetWindow<CSVImporterWindow>("CSV Importer");
//    }

//    void OnGUI()
//    {
//        GUILayout.Label("Assign CSV Files", EditorStyles.boldLabel);
//        monsterCSV = (TextAsset)EditorGUILayout.ObjectField("Monster Stats", monsterCSV, typeof(TextAsset), false);
//        itemCSV = (TextAsset)EditorGUILayout.ObjectField("Item Stats", itemCSV, typeof(TextAsset), false);
//        skillCSV = (TextAsset)EditorGUILayout.ObjectField("Skill Stats", skillCSV, typeof(TextAsset), false);
//        dropPoolCSV = (TextAsset)EditorGUILayout.ObjectField("Drop Pool", dropPoolCSV, typeof(TextAsset), false);
//        itemSkillCSV = (TextAsset)EditorGUILayout.ObjectField("Item Skill Pool", itemSkillCSV, typeof(TextAsset), false);
//        questCSV = (TextAsset)EditorGUILayout.ObjectField("NPC Quest", questCSV, typeof(TextAsset), false);
//        heatCSV = (TextAsset)EditorGUILayout.ObjectField("Heat Modifiers", heatCSV, typeof(TextAsset), false);

//        if (GUILayout.Button("Generate ScriptableObjects", GUILayout.Height(40)))
//        {
//            GenerateAll();
//        }
//    }

//    private void GenerateAll()
//    {
//        string basePath = "Assets/Data_Generated";
//        if (!AssetDatabase.IsValidFolder(basePath)) AssetDatabase.CreateFolder("Assets", "Data_Generated");

//        ParseMonsters(basePath);
//        ParseItems(basePath);
//        ParseSkills(basePath);
//        ParseDropPools(basePath);
//        ParseItemSkillPools(basePath);
//        ParseQuests(basePath);
//        ParseHeatModifiers(basePath);

//        AssetDatabase.SaveAssets();
//        AssetDatabase.Refresh();
//        Debug.Log("CSV Import Completed Successfully!");
//    }

//    private string[] GetLines(TextAsset csv)
//    {
//        if (csv == null) return new string[0];
//        return csv.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
//    }

//    private string[] SplitCsvLine(string line)
//    {
//        return Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
//    }

//    private void ParseMonsters(string basePath)
//    {
//        if (monsterCSV == null) return;
//        string folder = basePath + "/Monsters";
//        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(basePath, "Monsters");
        
//        string[] lines = GetLines(monsterCSV);
//        for (int i = 1; i < lines.Length; i++)
//        {
//            string[] cols = SplitCsvLine(lines[i]);
//            if (cols.Length < 10) continue;
            
//            MonsterDataSO so = ScriptableObject.CreateInstance<MonsterDataSO>();
//            so.monsterID = int.Parse(cols[0]);
//            so.monsterName = cols[1];
//            so.type = (MonsterType)Enum.Parse(typeof(MonsterType), cols[2], true);
//            so.hp = int.Parse(cols[3]);
//            so.atk = int.Parse(cols[4]);
//            so.def = int.Parse(cols[5]);
//            so.speed = float.Parse(cols[6]);
//            so.range = int.Parse(cols[7]);
//            so.zocRadius = int.Parse(cols[8]);
            
//            string traitsRaw = cols[9].Replace("\"", "");
//            if (traitsRaw.ToLower() != "none" && !string.IsNullOrEmpty(traitsRaw))
//                so.traits = new List<string>(traitsRaw.Split(','));
            
//            if (cols.Length > 10 && !string.IsNullOrEmpty(cols[10])) so.dropPoolID = int.Parse(cols[10]);
//            if (cols.Length > 11 && !string.IsNullOrEmpty(cols[11])) so.goldDropBase = int.Parse(cols[11]);

//            AssetDatabase.CreateAsset(so, $"{folder}/Monster_{so.monsterID}.asset");
//        }
//    }

//    private void ParseItems(string basePath)
//    {
//        if (itemCSV == null) return;
//        string folder = basePath + "/Items";
//        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(basePath, "Items");
        
//        string[] lines = GetLines(itemCSV);
//        for (int i = 1; i < lines.Length; i++)
//        {
//            string[] cols = SplitCsvLine(lines[i]);
//            if (cols.Length < 8) continue;
            
//            ItemDataSO so = ScriptableObject.CreateInstance<ItemDataSO>();
//            so.itemID = int.Parse(cols[0]);
//            so.itemName = cols[1];
//            so.category = (ItemCategory)Enum.Parse(typeof(ItemCategory), cols[2], true);
//            so.equipType = (EquipType)Enum.Parse(typeof(EquipType), cols[3], true);
//            so.weight = float.Parse(cols[4]);
//            so.maxStack = int.Parse(cols[5]);
//            so.baseValue = int.Parse(cols[6]);
//            so.rarity = (ItemRarity)Enum.Parse(typeof(ItemRarity), cols[7], true);

//            if (so.equipType == EquipType.Backpack)
//            {
//                // CSV 포맷이 확장되어 가방 스탯이 있을 경우 처리 (임시 방어코드)
//                if (cols.Length > 8 && !string.IsNullOrEmpty(cols[8])) so.bagAddedSlots = int.Parse(cols[8]);
//                if (cols.Length > 9 && !string.IsNullOrEmpty(cols[9])) so.bagMaxWeight = float.Parse(cols[9]);
//            }

//            AssetDatabase.CreateAsset(so, $"{folder}/Item_{so.itemID}.asset");
//        }
//    }

//    private void ParseSkills(string basePath)
//    {
//        if (skillCSV == null) return;
//        string folder = basePath + "/Skills";
//        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(basePath, "Skills");
        
//        string[] lines = GetLines(skillCSV);
//        for (int i = 1; i < lines.Length; i++)
//        {
//            string[] cols = SplitCsvLine(lines[i]);
//            if (cols.Length < 8) continue;
            
//            SkillDataSO so = ScriptableObject.CreateInstance<SkillDataSO>();
//            so.skillID = cols[0];
//            so.skillName = cols[1];
//            so.activationType = (SkillActivationType)Enum.Parse(typeof(SkillActivationType), cols[2], true);
//            so.roleType = (SkillRoleType)Enum.Parse(typeof(SkillRoleType), cols[3], true);
//            so.mpCost = int.Parse(cols[4]);
//            so.cooldown = float.Parse(cols[5]);
//            so.effectValue = float.Parse(cols[6]);
//            so.duration = float.Parse(cols[7]);

//            AssetDatabase.CreateAsset(so, $"{folder}/Skill_{so.skillID}.asset");
//        }
//    }

//    private void ParseDropPools(string basePath)
//    {
//        if (dropPoolCSV == null) return;
//        string folder = basePath + "/DropPools";
//        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(basePath, "DropPools");

//        Dictionary<int, DropPoolSO> pools = new Dictionary<int, DropPoolSO>();
//        string[] lines = GetLines(dropPoolCSV);
//        for (int i = 1; i < lines.Length; i++)
//        {
//            string[] cols = SplitCsvLine(lines[i]);
//            if (cols.Length < 3) continue;

//            int poolID = int.Parse(cols[0]);
//            if (!pools.ContainsKey(poolID))
//            {
//                DropPoolSO newPool = ScriptableObject.CreateInstance<DropPoolSO>();
//                newPool.dropPoolID = poolID;
//                pools[poolID] = newPool;
//            }
//            DropEntry entry = new DropEntry();
//            entry.itemID = int.Parse(cols[1]);
//            entry.dropWeight = int.Parse(cols[2]);
//            pools[poolID].dropEntries.Add(entry);
//        }
//        foreach (var kvp in pools) AssetDatabase.CreateAsset(kvp.Value, $"{folder}/DropPool_{kvp.Key}.asset");
//    }

//    private void ParseItemSkillPools(string basePath)
//    {
//        if (itemSkillCSV == null) return;
//        string folder = basePath + "/ItemSkillPools";
//        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(basePath, "ItemSkillPools");

//        Dictionary<int, ItemSkillPoolSO> pools = new Dictionary<int, ItemSkillPoolSO>();
//        string[] lines = GetLines(itemSkillCSV);
//        for (int i = 1; i < lines.Length; i++)
//        {
//            string[] cols = SplitCsvLine(lines[i]);
//            if (cols.Length < 3) continue;

//            int itemID = int.Parse(cols[0]);
//            if (!pools.ContainsKey(itemID))
//            {
//                ItemSkillPoolSO newPool = ScriptableObject.CreateInstance<ItemSkillPoolSO>();
//                newPool.itemID = itemID;
//                pools[itemID] = newPool;
//            }
//            SkillRollEntry entry = new SkillRollEntry();
//            entry.skillID = cols[1];
//            entry.rollWeight = int.Parse(cols[2]);
//            pools[itemID].possibleSkills.Add(entry);
//        }
//        foreach (var kvp in pools) AssetDatabase.CreateAsset(kvp.Value, $"{folder}/ItemSkillPool_{kvp.Key}.asset");
//    }

//    private void ParseQuests(string basePath)
//    {
//        if (questCSV == null) return;
//        string folder = basePath + "/Quests";
//        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(basePath, "Quests");
        
//        string[] lines = GetLines(questCSV);
//        for (int i = 1; i < lines.Length; i++)
//        {
//            string[] cols = SplitCsvLine(lines[i]);
//            if (cols.Length < 5) continue;
            
//            NPCQuestSO so = ScriptableObject.CreateInstance<NPCQuestSO>();
//            so.questID = int.Parse(cols[0]);
//            so.npcID = cols[1];
//            so.reqItemID = int.Parse(cols[2]);
//            so.reqAmount = int.Parse(cols[3]);
//            so.rewardAffinity = int.Parse(cols[4]);

//            AssetDatabase.CreateAsset(so, $"{folder}/Quest_{so.questID}.asset");
//        }
//    }

//    private void ParseHeatModifiers(string basePath)
//    {
//        if (heatCSV == null) return;
//        string folder = basePath + "/HeatModifiers";
//        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(basePath, "HeatModifiers");
        
//        string[] lines = GetLines(heatCSV);
//        for (int i = 1; i < lines.Length; i++)
//        {
//            string[] cols = SplitCsvLine(lines[i]);
//            if (cols.Length < 5) continue;
            
//            HeatModifierSO so = ScriptableObject.CreateInstance<HeatModifierSO>();
//            so.modifierID = cols[0];
//            so.modifierName = cols[1];
//            so.heatScore = int.Parse(cols[2]);
//            so.targetStat = cols[3];
//            so.value = float.Parse(cols[4]);

//            AssetDatabase.CreateAsset(so, $"{folder}/HeatMod_{so.modifierID}.asset");
//        }
//    }
//}
