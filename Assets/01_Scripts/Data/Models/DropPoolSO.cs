using UnityEngine;
using System.Collections.Generic;

namespace BagBack100.Data.Models
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

    /// <summary>
    /// Role: 몬스터 처치 시 드랍 테이블 (확정 아이템 + 가중치 그룹 롤). 골드는 지갑 재화라 드랍하지 않고 고가 전리품으로 대신한다.
    /// Dependencies: 없음 (itemID로 ItemDataSO 참조)
    /// Events: 없음
    /// </summary>
    [CreateAssetMenu(fileName = "NewDropPool", menuName = "BagBack100/Data/DropPool")]
    public class DropPoolSO : ScriptableObject
    {
        public int dropPoolID;

        [Header("1. 확정 아이템 (100% Guaranteed Drops)")]
        public List<FixedDropEntry> guaranteedDrops = new List<FixedDropEntry>();

        [Header("2. 랜덤 다중 뽑기 (Weighted Group Rolls)")]
        public List<DropGroup> randomDropGroups = new List<DropGroup>();
    }
}
