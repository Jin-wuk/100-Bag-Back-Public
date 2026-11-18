using System;
using System.Collections.Generic;
using UnityEngine;

namespace BagBack100.Data.Models
{
    [Serializable]
    public struct MaterialCost
    {
        [SerializeField] private ItemDataSO _item;
        [SerializeField, Min(1)] private int _amount;

        public ItemDataSO Item => _item;
        public int Amount => _amount;
    }

    /// <summary>
    /// Role: 제작소 테크트리 노드 1개의 정적 데이터 (결과 장비, 단계, 필요 지역, 같은 줄 선행 노드, 해금 재료·골드, 재제작 골드).
    ///       줄(무기·투구·갑옷·신발·배낭)은 결과 장비의 equipType으로 구분한다.
    /// Dependencies: EquipmentDataSO, ItemDataSO
    /// Events: 없음
    /// </summary>
    [CreateAssetMenu(fileName = "NewTechNode", menuName = "BagBack100/Data/TechNode")]
    public class TechNodeSO : ScriptableObject
    {
        [Tooltip("트리 안에서 고유한 노드 ID. 해금 상태를 이 ID로 기록한다")]
        [SerializeField] private string _nodeId;
        [Tooltip("해금·재제작 때 지급하는 장비")]
        [SerializeField] private EquipmentDataSO _resultItem;
        [SerializeField, Min(1)] private int _tier = 1;
        [Tooltip("이 노드가 열리는 지역 번호 (RegionConfigSO.RegionId)")]
        [SerializeField, Min(1)] private int _regionId = 1;
        [Tooltip("같은 줄의 이전 단계 노드. 첫 단계는 비운다")]
        [SerializeField] private TechNodeSO _prerequisite;

        [Header("Unlock Cost")]
        [SerializeField] private List<MaterialCost> _materials = new List<MaterialCost>();
        [SerializeField, Min(0)] private int _unlockGold;

        [Header("Recraft Cost")]
        [Tooltip("해금 후 같은 장비를 다시 만드는 비용(골드만). 제작 장비 판매가의 기준이 된다")]
        [SerializeField, Min(0)] private int _recraftGold;

        public string NodeId => _nodeId;
        public EquipmentDataSO ResultItem => _resultItem;
        public int Tier => _tier;
        public int RegionId => _regionId;
        public TechNodeSO Prerequisite => _prerequisite;
        public IReadOnlyList<MaterialCost> Materials => _materials;
        public int UnlockGold => _unlockGold;
        public int RecraftGold => _recraftGold;

        // 데이터 입력 실수를 에셋 저장 시점에 바로 알린다. 트리 단위 검증(ID 중복, 선행 줄·단계)은 TechTreeSO가 맡는다
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_nodeId)) Debug.LogWarning($"[TechNodeSO] {name}: 노드 ID가 비어 있습니다.", this);
            if (_resultItem == null) Debug.LogWarning($"[TechNodeSO] {name}: 결과 장비가 비어 있습니다.", this);
            if (_prerequisite == this) Debug.LogWarning($"[TechNodeSO] {name}: 자기 자신을 선행 노드로 지정했습니다.", this);
            if (_materials == null) return;

            for (int i = 0; i < _materials.Count; i++)
            {
                MaterialCost cost = _materials[i];
                if (cost.Item == null)
                {
                    Debug.LogWarning($"[TechNodeSO] {name}: 재료 {i}번 칸이 비어 있습니다.", this);
                    continue;
                }
                if (cost.Amount <= 0) Debug.LogWarning($"[TechNodeSO] {name}: 재료 {cost.Item.name}의 수량이 0 이하입니다.", this);
                for (int j = i + 1; j < _materials.Count; j++)
                {
                    if (_materials[j].Item == cost.Item) Debug.LogWarning($"[TechNodeSO] {name}: 재료 {cost.Item.name}가 중복됩니다. 한 칸으로 합쳐 주세요.", this);
                }
            }
        }
    }
}
