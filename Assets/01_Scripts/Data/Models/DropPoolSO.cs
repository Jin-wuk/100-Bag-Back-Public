using UnityEngine;
using System.Collections.Generic;

namespace DimensionLooter.Data.Models
{
    [System.Serializable]
    public struct FixedDropEntry
    {
        public int itemID;
        public int minAmount;
        public int maxAmount;
    }

    [System.Serializable]
    public struct DropEntry
    {
        public int itemID;
        public int dropWeight;
        public int minAmount;
        public int maxAmount;
    }

    [System.Serializable]
    public class DropGroup
    {
        public string groupName = "New Group"; // 기획자 식별용
        public int minRolls = 1;
        public int maxRolls = 1;
        
        [Tooltip("X축(0~1): 랜덤 주사위, Y축(0~1): 결과 가중치\n우하향(오목한) 곡선일수록 최소 횟수가 자주 나옵니다.")]
        public AnimationCurve rollProbabilityCurve = AnimationCurve.Linear(0, 0, 1, 1);
        
        public List<DropEntry> entries = new List<DropEntry>();
    }

    [CreateAssetMenu(fileName = "NewDropPool", menuName = "DimensionLooter/Data/DropPool")]
    public class DropPoolSO : ScriptableObject
    {
        public int dropPoolID;

        [Header("1. 확정 재화 (Guaranteed Currency)")]
        public int goldItemID = 1; // 골드 고유 ID (기본값 1)
        public int minGold = 0;
        public int maxGold = 0;

        [Header("2. 확정 아이템 (100% Guaranteed Drops)")]
        public List<FixedDropEntry> guaranteedDrops = new List<FixedDropEntry>();

        [Header("3. 랜덤 다중 뽑기 (Weighted Group Rolls)")]
        public List<DropGroup> randomDropGroups = new List<DropGroup>();
    }
}
