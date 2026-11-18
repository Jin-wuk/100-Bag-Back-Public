using System.Collections.Generic;
using UnityEngine;

namespace BagBack100.Data.Models
{
    /// <summary>
    /// Role: 제작소 테크트리 1개의 정적 데이터 (노드 목록, 제작 장비 판매 비율). 노드 조회와 트리 단위 데이터 검증을 맡는다.
    ///       제작 장비 판매가 = floor(재제작 골드 × 판매 비율). 실제 판매는 결과 장비의 baseValue를 쓰므로
    ///       에디터 메뉴 BagBack100/Tech/Sync Crafted Sell Prices로 baseValue를 이 값에 맞춘다.
    /// Dependencies: TechNodeSO, EquipmentDataSO
    /// Events: 없음
    /// </summary>
    [CreateAssetMenu(fileName = "NewTechTree", menuName = "BagBack100/Data/TechTree")]
    public class TechTreeSO : ScriptableObject
    {
        [SerializeField] private List<TechNodeSO> _nodes = new List<TechNodeSO>();
        [Tooltip("제작 장비 판매가 = floor(재제작 골드 × 이 비율)")]
        [SerializeField, Range(0f, 0.99f)] private float _craftedSellRatio = 0.3f;

        public IReadOnlyList<TechNodeSO> Nodes => _nodes;
        public float CraftedSellRatio => _craftedSellRatio;

        public int GetSellPrice(TechNodeSO node)
        {
            if (node == null) return 0;
            return Mathf.FloorToInt(node.RecraftGold * _craftedSellRatio);
        }

        public TechNodeSO GetNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return null;
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i] != null && _nodes[i].NodeId == nodeId) return _nodes[i];
            }
            return null;
        }

        /// <summary>
        /// 트리 단위 데이터 검증. OnValidate와 에디터 메뉴가 같은 결과를 쓰도록 메시지 목록을 채워 돌려준다.
        /// 노드 자체 입력 오류(재료 null·중복 등)는 TechNodeSO.OnValidate가 맡는다.
        /// </summary>
        public void CollectWarnings(List<string> warnings)
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                TechNodeSO node = _nodes[i];
                if (node == null)
                {
                    warnings.Add($"[{name}] 노드 {i}번 칸이 비어 있습니다.");
                    continue;
                }

                for (int j = i + 1; j < _nodes.Count; j++)
                {
                    TechNodeSO other = _nodes[j];
                    if (other == null) continue;
                    if (other == node) warnings.Add($"[{name}] 노드 {node.name}가 목록에 두 번 들어 있습니다.");
                    else if (!string.IsNullOrEmpty(node.NodeId) && other.NodeId == node.NodeId) warnings.Add($"[{name}] 노드 ID '{node.NodeId}'가 {node.name}, {other.name}에 중복됩니다.");
                    else if (node.ResultItem != null && other.ResultItem == node.ResultItem) warnings.Add($"[{name}] 결과 장비 {node.ResultItem.name}가 {node.name}, {other.name}에 중복됩니다.");
                }

                TechNodeSO pre = node.Prerequisite;
                if (pre != null && pre != node)
                {
                    if (!_nodes.Contains(pre)) warnings.Add($"[{name}] {node.name}의 선행 노드 {pre.name}가 이 트리에 없습니다.");
                    if (pre.ResultItem != null && node.ResultItem != null && pre.ResultItem.equipType != node.ResultItem.equipType)
                        warnings.Add($"[{name}] {node.name}의 선행 노드 {pre.name}가 다른 줄({pre.ResultItem.equipType})입니다.");
                    if (pre.Tier >= node.Tier) warnings.Add($"[{name}] {node.name}(단계 {node.Tier})의 선행 노드 {pre.name}(단계 {pre.Tier})가 더 낮은 단계가 아닙니다.");
                }
                else if (pre == null && node.Tier > 1)
                {
                    warnings.Add($"[{name}] {node.name}는 단계 {node.Tier}인데 선행 노드가 없습니다.");
                }

                if (node.ResultItem != null && node.ResultItem.baseValue != GetSellPrice(node))
                {
                    warnings.Add($"[{name}] {node.ResultItem.name}의 baseValue({node.ResultItem.baseValue})가 노드 판매가({GetSellPrice(node)})와 다릅니다. BagBack100/Tech/Sync Crafted Sell Prices를 실행하세요.");
                }
            }
        }

        private void OnValidate()
        {
            var warnings = new List<string>();
            CollectWarnings(warnings);
            for (int i = 0; i < warnings.Count; i++) Debug.LogWarning(warnings[i], this);
        }
    }
}
