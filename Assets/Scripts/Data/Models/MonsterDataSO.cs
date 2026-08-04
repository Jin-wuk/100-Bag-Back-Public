using UnityEngine;
using System.Collections.Generic;
using DimensionLooter.Data.Core;

namespace DimensionLooter.Data.Models
{
    [CreateAssetMenu(fileName = "NewMonsterData", menuName = "DimensionLooter/Data/MonsterData")]
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
    }
}
