using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BagBack100.Data.Core;
using BagBack100.Data.Models;
using BagBack100.Hub;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 제작소(대장간) 테크트리 화면. 탭별로 장비 줄(무기 / 투구·갑옷·신발·배낭)을 그리고, 노드를 고르면 결과 장비·재료 보유/필요·골드와
    ///       해금(Unlock)·재제작(Craft) 버튼을 보여 준다. 계산은 하지 않고 CraftingSystem에 묻기만 한다.
    /// Dependencies: CraftingSystem, TechTreeSO, UIToast, PlayerSessionData(골드 표시 색 판정만 읽음)
    /// Events: 없음 (구독: CraftingSystem.OnTechTreeChanged, OnCraftFailed, OnCraftCompleted)
    /// </summary>
    public class TechTreePanelUI : MonoBehaviour
    {
        [Serializable]
        private class TabDef
        {
            [SerializeField] private Button _button;
            [Tooltip("이 탭에 위에서부터 그릴 장비 줄. 줄마다 결과 장비의 equipType이 같은 노드를 단계 순으로 놓는다")]
            [SerializeField] private EquipType[] _lines = new EquipType[0];

            public Button Button => _button;
            public EquipType[] Lines => _lines;
        }

        private sealed class NodeView
        {
            public TechNodeSO Node { get; }
            public Button Button { get; }
            public Image Background { get; }
            public Image Icon { get; }
            public TextMeshProUGUI Label { get; }

            public NodeView(TechNodeSO node, GameObject go)
            {
                Node = node;
                Button = go.GetComponent<Button>();
                Background = go.GetComponent<Image>();
                Icon = go.transform.Find("Icon")?.GetComponent<Image>();
                Label = go.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
            }
        }

        private sealed class MaterialView
        {
            public ItemDataSO Item { get; }
            public int Need { get; }
            public TextMeshProUGUI Count { get; }

            public MaterialView(ItemDataSO item, int need, TextMeshProUGUI count)
            {
                Item = item;
                Need = need;
                Count = count;
            }
        }

        [SerializeField] private CraftingSystem _crafting;
        [SerializeField] private UIToast _toast;

        [Header("Tabs")]
        [SerializeField] private TabDef[] _tabs = new TabDef[0];
        [SerializeField] private Color _tabSelectedColor = new Color(0.45f, 0.38f, 0.25f, 1f);
        [SerializeField] private Color _tabNormalColor = new Color(0.25f, 0.22f, 0.2f, 1f);

        [Header("Tree")]
        [Tooltip("줄 템플릿이 복제될 부모 (세로 배치)")]
        [SerializeField] private RectTransform _lineContainer;
        [Tooltip("자식: Label(TMP), Nodes(가로 배치)")]
        [SerializeField] private GameObject _lineTemplate;
        [Tooltip("자식: Icon(Image), Name(TMP). 루트에 Button·Image")]
        [SerializeField] private GameObject _nodeTemplate;
        [Tooltip("같은 줄 노드 사이 연결선")]
        [SerializeField] private GameObject _connectorTemplate;

        [Header("Node Colors")]
        [SerializeField] private Color _hiddenColor = new Color(0.12f, 0.12f, 0.12f, 0.9f);
        [SerializeField] private Color _lockedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color _unlockableColor = new Color(0.55f, 0.45f, 0.2f, 1f);
        [SerializeField] private Color _unlockedColor = new Color(0.25f, 0.5f, 0.3f, 1f);
        [Tooltip("선택한 노드는 상태 색을 흰색 쪽으로 이만큼 밝힌다")]
        [SerializeField, Range(0f, 1f)] private float _selectedHighlight = 0.3f;
        [SerializeField] private Color _shortColor = new Color(1f, 0.45f, 0.4f, 1f);
        [SerializeField] private Color _enoughColor = Color.white;

        [Header("Detail")]
        [SerializeField] private GameObject _detailRoot;
        [SerializeField] private TextMeshProUGUI _detailName;
        [SerializeField] private TextMeshProUGUI _detailStats;
        [SerializeField] private RectTransform _materialContainer;
        [Tooltip("자식: Name(TMP), Count(TMP)")]
        [SerializeField] private GameObject _materialTemplate;
        [SerializeField] private TextMeshProUGUI _detailGold;
        [SerializeField] private Button _actionButton;
        [SerializeField] private TextMeshProUGUI _actionLabel;
        [SerializeField] private TextMeshProUGUI _emptyText;

        private const string HiddenName = "???";

        private readonly List<GameObject> _spawnedLines = new List<GameObject>();
        private readonly List<NodeView> _nodeViews = new List<NodeView>();
        private readonly List<MaterialView> _materialViews = new List<MaterialView>();
        private readonly List<GameObject> _spawnedMaterials = new List<GameObject>();
        private readonly List<TechNodeSO> _lineBuffer = new List<TechNodeSO>();

        private int _currentTab;
        private TechNodeSO _selected;
        private bool _dirty;
        private bool _lastActionWasUnlock;

        private void Awake()
        {
            if (_crafting == null) Debug.LogError("[TechTreePanelUI] CraftingSystem이 연결되지 않았습니다.", this);
            if (_lineTemplate != null) _lineTemplate.SetActive(false);
            if (_nodeTemplate != null) _nodeTemplate.SetActive(false);
            if (_connectorTemplate != null) _connectorTemplate.SetActive(false);
            if (_materialTemplate != null) _materialTemplate.SetActive(false);

            for (int i = 0; i < _tabs.Length; i++)
            {
                int index = i;
                if (_tabs[i].Button != null) _tabs[i].Button.onClick.AddListener(() => SelectTab(index));
            }
            if (_actionButton != null) _actionButton.onClick.AddListener(HandleActionClicked);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _tabs.Length; i++)
            {
                if (_tabs[i].Button != null) _tabs[i].Button.onClick.RemoveAllListeners();
            }
            if (_actionButton != null) _actionButton.onClick.RemoveAllListeners();
        }

        private void OnEnable()
        {
            if (_crafting != null)
            {
                _crafting.OnTechTreeChanged += MarkDirty;
                _crafting.OnCraftFailed += ShowMessage;
                _crafting.OnCraftCompleted += HandleCraftCompleted;
            }
            SelectTab(_currentTab);
        }

        private void OnDisable()
        {
            if (_crafting == null) return;
            _crafting.OnTechTreeChanged -= MarkDirty;
            _crafting.OnCraftFailed -= ShowMessage;
            _crafting.OnCraftCompleted -= HandleCraftCompleted;
        }

        // 한 번의 해금에서 컨테이너·골드 알림이 여러 번 와도 프레임 끝에 1회만 다시 그린다
        private void MarkDirty() => _dirty = true;

        private void LateUpdate()
        {
            if (!_dirty) return;
            _dirty = false;
            Refresh();
        }

        private void ShowMessage(string message)
        {
            if (_toast != null) _toast.Show(message);
        }

        private void HandleCraftCompleted(string nodeId)
        {
            TechNodeSO node = _crafting.TechTree != null ? _crafting.TechTree.GetNode(nodeId) : null;
            string itemName = node != null && node.ResultItem != null ? node.ResultItem.itemName : nodeId;
            ShowMessage(_lastActionWasUnlock ? $"Unlocked {itemName}" : $"Crafted {itemName}");
            // 해금으로 다음 단계 노드 상태와 버튼 문구가 바뀌므로 상세도 다시 그린다
            if (_selected != null) ShowDetail(_selected);
            MarkDirty();
        }

        private void SelectTab(int index)
        {
            if (_tabs.Length == 0) return;
            _currentTab = Mathf.Clamp(index, 0, _tabs.Length - 1);
            for (int i = 0; i < _tabs.Length; i++)
            {
                if (_tabs[i].Button != null && _tabs[i].Button.targetGraphic != null)
                    _tabs[i].Button.targetGraphic.color = i == _currentTab ? _tabSelectedColor : _tabNormalColor;
            }
            BuildLines();
            ShowDetail(null);
            MarkDirty();
        }

        // 탭 전환·패널 열기 때만 실행되므로 생성·문자열 할당을 허용한다
        private void BuildLines()
        {
            ClearLines();
            TechTreeSO tree = _crafting != null ? _crafting.TechTree : null;
            bool any = false;
            if (tree != null && _lineContainer != null && _lineTemplate != null && _nodeTemplate != null)
            {
                EquipType[] lines = _tabs[_currentTab].Lines;
                for (int l = 0; l < lines.Length; l++)
                {
                    CollectLine(tree, lines[l]);
                    if (_lineBuffer.Count == 0) continue;
                    any = true;
                    BuildLine(lines[l]);
                }
            }
            if (_emptyText != null) _emptyText.gameObject.SetActive(!any);
        }

        private void CollectLine(TechTreeSO tree, EquipType type)
        {
            _lineBuffer.Clear();
            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                TechNodeSO node = tree.Nodes[i];
                if (node != null && node.ResultItem != null && node.ResultItem.equipType == type) _lineBuffer.Add(node);
            }
            _lineBuffer.Sort(CompareNodes);
        }

        private static int CompareNodes(TechNodeSO a, TechNodeSO b)
        {
            int byRegion = a.RegionId.CompareTo(b.RegionId);
            return byRegion != 0 ? byRegion : a.Tier.CompareTo(b.Tier);
        }

        private void BuildLine(EquipType type)
        {
            GameObject line = Instantiate(_lineTemplate, _lineContainer);
            line.SetActive(true);
            _spawnedLines.Add(line);

            var label = line.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null) label.text = type.ToString();
            Transform nodesParent = line.transform.Find("Nodes");
            if (nodesParent == null) nodesParent = line.transform;

            for (int i = 0; i < _lineBuffer.Count; i++)
            {
                if (i > 0 && _connectorTemplate != null)
                {
                    GameObject connector = Instantiate(_connectorTemplate, nodesParent);
                    connector.SetActive(true);
                }

                TechNodeSO node = _lineBuffer[i];
                GameObject go = Instantiate(_nodeTemplate, nodesParent);
                go.SetActive(true);
                var view = new NodeView(node, go);
                if (view.Icon != null) view.Icon.sprite = node.ResultItem.itemIcon;
                if (view.Button != null) view.Button.onClick.AddListener(() => SelectNode(node));
                _nodeViews.Add(view);
            }
        }

        private void ClearLines()
        {
            for (int i = 0; i < _nodeViews.Count; i++)
            {
                if (_nodeViews[i].Button != null) _nodeViews[i].Button.onClick.RemoveAllListeners();
            }
            _nodeViews.Clear();
            for (int i = 0; i < _spawnedLines.Count; i++)
            {
                if (_spawnedLines[i] != null) Destroy(_spawnedLines[i]);
            }
            _spawnedLines.Clear();
        }

        private void SelectNode(TechNodeSO node)
        {
            if (_crafting == null || _crafting.GetState(node) == TechNodeState.Hidden) return;
            ShowDetail(node);
            MarkDirty();
        }

        // 노드를 고를 때만 실행: 이름·스탯 문자열과 재료 행을 만든다. 수량·버튼 상태는 Refresh가 갱신
        private void ShowDetail(TechNodeSO node)
        {
            _selected = node;
            ClearMaterials();
            if (_detailRoot != null) _detailRoot.SetActive(node != null);
            if (node == null) return;

            EquipmentDataSO item = node.ResultItem;
            if (_detailName != null) _detailName.text = $"{item.itemName}  (Tier {node.Tier})";
            if (_detailStats != null) _detailStats.text = BuildStats(item);

            if (_materialContainer != null && _materialTemplate != null)
            {
                bool showMaterials = _crafting.GetState(node) != TechNodeState.Unlocked;
                var materials = node.Materials;
                for (int i = 0; showMaterials && i < materials.Count; i++)
                {
                    if (materials[i].Item == null) continue;
                    GameObject row = Instantiate(_materialTemplate, _materialContainer);
                    row.SetActive(true);
                    _spawnedMaterials.Add(row);
                    var nameText = row.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
                    if (nameText != null) nameText.text = materials[i].Item.itemName;
                    _materialViews.Add(new MaterialView(materials[i].Item, materials[i].Amount, row.transform.Find("Count")?.GetComponent<TextMeshProUGUI>()));
                }
            }
        }

        private static string BuildStats(EquipmentDataSO item)
        {
            var sb = new System.Text.StringBuilder();
            if (item.attackBonus != 0) sb.Append("ATK +").Append(item.attackBonus).Append("   ");
            if (item.defenseBonus != 0) sb.Append("DEF +").Append(item.defenseBonus).Append("   ");
            if (item.hpBonus != 0) sb.Append("HP +").Append(item.hpBonus).Append("   ");
            if (item.speedBonus != 0f) sb.Append("SPD +").Append(item.speedBonus).Append("   ");
            if (item is BackpackDataSO bag && bag.bagAddedSlots != 0) sb.Append("Bag +").Append(bag.bagAddedSlots).Append(" slots");
            return sb.Length > 0 ? sb.ToString() : "-";
        }

        private void ClearMaterials()
        {
            _materialViews.Clear();
            for (int i = 0; i < _spawnedMaterials.Count; i++)
            {
                if (_spawnedMaterials[i] != null) Destroy(_spawnedMaterials[i]);
            }
            _spawnedMaterials.Clear();
        }

        // 상태 색·수량·버튼만 바꾸므로 문자열·객체 할당이 없다 (SetText와 문자열 상수만 사용)
        private void Refresh()
        {
            if (_crafting == null) return;

            for (int i = 0; i < _nodeViews.Count; i++)
            {
                NodeView view = _nodeViews[i];
                TechNodeState state = _crafting.GetState(view.Node);
                if (view.Background != null)
                {
                    Color color = StateColor(state);
                    view.Background.color = view.Node == _selected ? Color.Lerp(color, Color.white, _selectedHighlight) : color;
                }
                if (view.Label != null)
                {
                    if (state == TechNodeState.Hidden) view.Label.SetText(HiddenName);
                    else view.Label.SetText(view.Node.ResultItem.itemName);
                }
                // 잠긴 지역의 장비는 이름과 아이콘을 모두 가린다
                if (view.Icon != null) view.Icon.enabled = state != TechNodeState.Hidden && view.Icon.sprite != null;
                if (view.Button != null) view.Button.interactable = state != TechNodeState.Hidden;
            }

            if (_selected == null) return;
            TechNodeState selectedState = _crafting.GetState(_selected);
            bool unlocked = selectedState == TechNodeState.Unlocked;

            for (int i = 0; i < _materialViews.Count; i++)
            {
                MaterialView view = _materialViews[i];
                if (view.Count == null) continue;
                int owned = _crafting.GetOwnedAmount(view.Item);
                view.Count.SetText("{0}/{1}", owned, view.Need);
                view.Count.color = owned >= view.Need ? _enoughColor : _shortColor;
            }

            int gold = unlocked ? _selected.RecraftGold : _selected.UnlockGold;
            bool canAfford = unlocked ? _crafting.CanAffordRecraft(_selected) : _crafting.CanAffordUnlock(_selected);
            if (_detailGold != null)
            {
                _detailGold.SetText("{0}G", gold);
                _detailGold.color = PlayerSessionData.Gold >= gold ? _enoughColor : _shortColor;
            }
            if (_actionLabel != null)
            {
                if (unlocked) _actionLabel.SetText("Craft");
                else if (selectedState == TechNodeState.PrerequisiteLocked) _actionLabel.SetText("Locked");
                else _actionLabel.SetText("Unlock");
            }
            if (_actionButton != null)
                _actionButton.interactable = canAfford && (unlocked || selectedState == TechNodeState.Unlockable);
        }

        private Color StateColor(TechNodeState state)
        {
            switch (state)
            {
                case TechNodeState.Unlocked: return _unlockedColor;
                case TechNodeState.Unlockable: return _unlockableColor;
                case TechNodeState.PrerequisiteLocked: return _lockedColor;
                default: return _hiddenColor;
            }
        }

        private void HandleActionClicked()
        {
            if (_crafting == null || _selected == null) return;
            if (_crafting.GetState(_selected) == TechNodeState.Unlocked)
            {
                _lastActionWasUnlock = false;
                _crafting.TryRecraft(_selected);
            }
            else
            {
                _lastActionWasUnlock = true;
                _crafting.TryUnlock(_selected);
            }
        }
    }
}
