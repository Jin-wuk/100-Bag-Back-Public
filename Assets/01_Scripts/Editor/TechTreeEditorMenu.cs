using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BagBack100.Data.Models;

namespace BagBack100.EditorTools
{
    /// <summary>
    /// Role: 제작소 테크트리 에디터 메뉴. 제작 장비의 baseValue를 노드 판매가(floor(재제작 골드 × 판매 비율))에 맞춰
    ///       잡화상 판매(HubTradeSystem)와 결과 화면 가치(RunResult)가 같은 값을 쓰게 한다.
    /// Dependencies: TechTreeSO, TechNodeSO, EquipmentDataSO
    /// Events: 없음
    /// </summary>
    public static class TechTreeEditorMenu
    {
        [MenuItem("BagBack100/Tech/Sync Crafted Sell Prices")]
        public static void SyncCraftedSellPrices()
        {
            // 같은 장비가 여러 트리에서 다른 판매가를 요구하면 덮어쓰기 순서에 따라 값이 바뀌므로 먼저 모아서 충돌을 알린다
            var prices = new Dictionary<EquipmentDataSO, int>();
            var sources = new Dictionary<EquipmentDataSO, TechTreeSO>();
            string[] guids = AssetDatabase.FindAssets("t:TechTreeSO");
            for (int g = 0; g < guids.Length; g++)
            {
                var tree = AssetDatabase.LoadAssetAtPath<TechTreeSO>(AssetDatabase.GUIDToAssetPath(guids[g]));
                if (tree == null) continue;
                for (int i = 0; i < tree.Nodes.Count; i++)
                {
                    TechNodeSO node = tree.Nodes[i];
                    if (node == null || node.ResultItem == null) continue;
                    int price = tree.GetSellPrice(node);
                    if (prices.TryGetValue(node.ResultItem, out int existing) && existing != price)
                    {
                        Debug.LogError($"[TechTreeEditorMenu] {node.ResultItem.name}의 판매가가 {sources[node.ResultItem].name}({existing})와 {tree.name}({price})에서 다릅니다. 동기화를 중단합니다.", node);
                        return;
                    }
                    prices[node.ResultItem] = price;
                    sources[node.ResultItem] = tree;
                }
            }

            int changed = 0;
            foreach (var pair in prices)
            {
                if (pair.Key.baseValue == pair.Value) continue;
                Undo.RecordObject(pair.Key, "Sync Crafted Sell Prices");
                pair.Key.baseValue = pair.Value;
                EditorUtility.SetDirty(pair.Key);
                changed++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[TechTreeEditorMenu] 테크트리 {guids.Length}개, 제작 장비 {prices.Count}종 중 {changed}종의 baseValue를 판매가에 맞췄습니다.");
        }
    }
}
