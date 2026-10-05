using UnityEngine;
using System.Collections.Generic;
using BagBack100.Data.Core;

namespace BagBack100.Data.Models
{
    [CreateAssetMenu(fileName = "NewMonsterData", menuName = "BagBack100/Data/MonsterData")]
    public class MonsterDataSO : ScriptableObject
    {
        public int monsterID;
        public string monsterName;
        public MonsterType type;
        public int hp;
        public int atk;
        public int def;
        public float speed;
        public int range;
        public int zocRadius;
        public List<string> traits = new List<string>();
        public int dropPoolID;
        public int goldDropBase;
        public AttackPatternType attackPattern = AttackPatternType.SingleTile;
        public float telegraphTime = 1f;
    }
}
