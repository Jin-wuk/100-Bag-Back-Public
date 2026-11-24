using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BagBack100.Data.Core;
using BagBack100.Dungeon.Data;
using BagBack100.Monster;

namespace BagBack100.Dungeon
{
    /// <summary>
    /// Role: 허브 포탈로 여는 전체 화면 월드맵과, 던전 아이콘 옆에 뜨는 입장 패널을 관리한다.
    ///       지도는 344×192 아트를 화면에 들어가는 최대 정수배로 키워 픽셀이 깨지지 않게 한다.
    ///       해금된 지역만 아이콘을 켜고, 입장 패널은 RegionConfigSO(이름·설명·보스·몬스터 풀)로 채운다.
    /// Dependencies: RegionConfigSO(Resources), PlayerSessionData(지역 해금), MonsterController(보스·몬스터 이름과 아이콘), WorldMapNodeUI
    /// Events: 없음 (WorldMapNodeUI 로컬 이벤트 구독)
    /// </summary>
    public class DungeonEntranceUI : MonoBehaviour
    {
        private const float MapWidth = 344f;
        private const float MapHeight = 192f;

        [Header("Scene Transition Settings")]
        [Tooltip("진입할 던전 씬의 이름 (Build Settings에 추가되어야 함)")]
        [SerializeField] private string _dungeonSceneName = "03_DungeonScene";

        [Header("World Map")]
        [Tooltip("344×192 지도 루트. 피벗·앵커 가운데. 이 아래 좌표는 지도 픽셀 단위")]
        [SerializeField] private RectTransform _mapRoot;
        [Tooltip("지역 아이콘 노드. RegionId 순으로 해금된 지역에 하나씩 쓴다")]
        [SerializeField] private WorldMapNodeUI[] _regionNodes;
        [Tooltip("지역 아이콘을 그리는 Image (_regionNodes와 같은 순서)")]
        [SerializeField] private Image[] _regionIcons;
        [Tooltip("미해금 안개 (호버 툴팁만, 클릭 없음)")]
        [SerializeField] private WorldMapNodeUI _fogNode;
        [Tooltip("지도를 화면에 맞출 때 화면 가장자리에 남길 여백(화면 픽셀)")]
        [SerializeField, Min(0f)] private float _screenMargin = 32f;

        [Header("Entry Panel")]
        [SerializeField] private RectTransform _entryPanel;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private TextMeshProUGUI _floorsText;
        [SerializeField] private TextMeshProUGUI _bossText;
        [Tooltip("몬스터 아이콘 슬롯. 몬스터 수만큼 켜고 나머지는 끈다")]
        [SerializeField] private WorldMapNodeUI[] _monsterSlots;
        [SerializeField] private Image[] _monsterIcons;
        [Tooltip("아이콘과 패널 사이 간격(지도 픽셀)")]
        [SerializeField, Min(0f)] private float _panelGap = 4f;

        [Header("Tooltip")]
        [SerializeField] private RectTransform _tooltip;
        [SerializeField] private TextMeshProUGUI _tooltipText;
        [SerializeField] private Vector2 _tooltipPadding = new Vector2(6f, 3f);

        private struct MonsterEntry
        {
            public string Name;
            public Sprite Icon;
        }

        private readonly List<RegionConfigSO> _regions = new List<RegionConfigSO>();
        private readonly Dictionary<RegionConfigSO, List<MonsterEntry>> _monsterCache = new Dictionary<RegionConfigSO, List<MonsterEntry>>();
        private readonly Vector3[] _corners = new Vector3[4];
        private WorldMapNodeUI _hoveredNode;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            if (_mapRoot == null || _entryPanel == null || _tooltip == null)
            {
                Debug.LogError("[DungeonEntranceUI] 지도·입장 패널·툴팁 참조가 비어 있습니다. Tools/UI Kit/Build World Map 을 실행하세요.");
            }

            // LootSystem과 같은 방식: Resources 아래 지역 SO를 모두 읽어 RegionId 순으로 둔다
            _regions.AddRange(Resources.LoadAll<RegionConfigSO>(""));
            _regions.Sort((a, b) => a.RegionId.CompareTo(b.RegionId));
        }

        private void OnEnable()
        {
            SetNodeEvents(true);
        }

        private void OnDisable()
        {
            SetNodeEvents(false);
            HideTooltip();
        }

        /// <summary>
        /// 허브씬의 던전 입장 버튼의 UI OnClick 이벤트에 연결할 메서드.
        /// </summary>
        public void OnClickEnterDungeon()
        {
            if (!string.IsNullOrEmpty(_dungeonSceneName))
            {
                SceneManager.LoadScene(_dungeonSceneName);
            }
        }

        public void ShowPanel()
        {
            gameObject.SetActive(true);
            FitMapToScreen();
            BindRegionNodes();
            CloseEntryPanel();
            HideTooltip();
        }

        public void HidePanel()
        {
            CloseEntryPanel();
            HideTooltip();
            gameObject.SetActive(false);
        }

        /// <summary>ESC 한 번에 한 겹씩 닫는다. 입장 패널 → 지도. 이미 닫혀 있으면 false.</summary>
        public bool TryCloseTopLayer()
        {
            if (!IsOpen) return false;

            if (_entryPanel != null && _entryPanel.gameObject.activeSelf)
            {
                CloseEntryPanel();
                return true;
            }

            HidePanel();
            return true;
        }

        public void CloseEntryPanel()
        {
            if (_entryPanel != null) _entryPanel.gameObject.SetActive(false);
        }

        private void SetNodeEvents(bool subscribe)
        {
            if (_regionNodes != null)
            {
                foreach (var node in _regionNodes) Subscribe(node, subscribe, true);
            }
            Subscribe(_fogNode, subscribe, false);
            if (_monsterSlots != null)
            {
                foreach (var slot in _monsterSlots) Subscribe(slot, subscribe, false);
            }
        }

        private void Subscribe(WorldMapNodeUI node, bool subscribe, bool listenClick)
        {
            if (node == null) return;
            if (subscribe)
            {
                node.Hovered += HandleNodeHovered;
                if (listenClick) node.Clicked += HandleRegionClicked;
            }
            else
            {
                node.Hovered -= HandleNodeHovered;
                if (listenClick) node.Clicked -= HandleRegionClicked;
            }
        }

        // 캔버스가 Constant Pixel Size여도 scaleFactor가 1이 아닐 수 있으므로, 정수배는 화면 픽셀 기준으로 구하고 캔버스 단위로 되돌린다
        private void FitMapToScreen()
        {
            if (_mapRoot == null) return;

            var canvas = _mapRoot.GetComponentInParent<Canvas>();
            float scaleFactor = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            if (scaleFactor <= 0f) scaleFactor = 1f;

            float fitX = (Screen.width - _screenMargin * 2f) / MapWidth;
            float fitY = (Screen.height - _screenMargin * 2f) / MapHeight;
            int pixelScale = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(fitX, fitY)));

            _mapRoot.localScale = Vector3.one * (pixelScale / scaleFactor);
        }

        private void BindRegionNodes()
        {
            if (_regionNodes == null) return;

            int nodeIndex = 0;
            foreach (var region in _regions)
            {
                if (region == null || !PlayerSessionData.IsRegionUnlocked(region.RegionId)) continue;

                if (nodeIndex >= _regionNodes.Length)
                {
                    Debug.LogWarning($"[DungeonEntranceUI] 지역 노드가 모자랍니다. '{region.RegionName}'은(는) 지도에 표시되지 않습니다.");
                    break;
                }

                var node = _regionNodes[nodeIndex];
                var icon = _regionIcons != null && nodeIndex < _regionIcons.Length ? _regionIcons[nodeIndex] : null;
                if (node != null)
                {
                    node.Bind(region.RegionName, region);
                    if (icon != null && region.MapIcon != null)
                    {
                        icon.sprite = region.MapIcon;
                        node.Rect.sizeDelta = region.MapIcon.rect.size;
                    }

                    // 좌표는 지도 좌상단 기준 픽셀. 호버 확대가 가운데서 되도록 노드 피벗은 가운데로 두고 중심점으로 옮긴다
                    var pos = region.MapIconPosition;
                    Vector2 size = node.Rect.sizeDelta;
                    node.Rect.anchorMin = node.Rect.anchorMax = new Vector2(0f, 1f);
                    node.Rect.pivot = new Vector2(0.5f, 0.5f);
                    node.Rect.anchoredPosition = new Vector2(pos.x + size.x * 0.5f, -(pos.y + size.y * 0.5f));
                    node.gameObject.SetActive(true);
                }
                nodeIndex++;
            }

            for (int i = nodeIndex; i < _regionNodes.Length; i++)
            {
                if (_regionNodes[i] != null) _regionNodes[i].gameObject.SetActive(false);
            }
        }

        private void HandleRegionClicked(WorldMapNodeUI node)
        {
            var region = node.Payload as RegionConfigSO;
            if (region == null) return;

            FillEntryPanel(region);
            _entryPanel.gameObject.SetActive(true);
            PlaceEntryPanel(node.Rect);
        }

        private void FillEntryPanel(RegionConfigSO region)
        {
            if (_titleText != null) _titleText.text = region.RegionName;
            if (_descriptionText != null) _descriptionText.text = region.Description;
            if (_floorsText != null) _floorsText.text = $"Floors 1-{RegionConfigSO.BossFloor}";
            if (_bossText != null) _bossText.text = BuildBossText(region);

            var monsters = GetMonsters(region);
            if (_monsterSlots == null) return;
            for (int i = 0; i < _monsterSlots.Length; i++)
            {
                var slot = _monsterSlots[i];
                if (slot == null) continue;

                bool hasMonster = i < monsters.Count;
                slot.gameObject.SetActive(hasMonster);
                if (!hasMonster) continue;

                slot.Bind(monsters[i].Name, null);
                var icon = _monsterIcons != null && i < _monsterIcons.Length ? _monsterIcons[i] : null;
                if (icon != null)
                {
                    icon.sprite = monsters[i].Icon;
                    icon.enabled = monsters[i].Icon != null;
                }
            }
            if (monsters.Count > _monsterSlots.Length)
            {
                Debug.LogWarning($"[DungeonEntranceUI] 몬스터 슬롯({_monsterSlots.Length})보다 몬스터({monsters.Count})가 많아 일부를 생략합니다.");
            }
        }

        private static string BuildBossText(RegionConfigSO region)
        {
            string midBoss = GetMonsterName(region.MidBossPrefab);
            string boss = GetMonsterName(region.BossPrefab);
            if (midBoss == null && boss == null) return "-";
            if (midBoss == null) return $"{RegionConfigSO.BossFloor}F  {boss}";
            if (boss == null) return $"{RegionConfigSO.MidBossFloor}F  {midBoss}";
            return $"{RegionConfigSO.MidBossFloor}F  {midBoss}\n{RegionConfigSO.BossFloor}F  {boss}";
        }

        private static string GetMonsterName(GameObject prefab)
        {
            if (prefab == null) return null;
            var controller = prefab.GetComponent<MonsterController>();
            return controller != null && controller.Data != null ? controller.Data.monsterName : prefab.name;
        }

        // MonsterDataSO에 아이콘 필드가 없어 프리팹의 SpriteRenderer 스프라이트를 쓴다. 같은 데이터가 풀에 중복되면 한 번만 둔다
        private List<MonsterEntry> GetMonsters(RegionConfigSO region)
        {
            if (_monsterCache.TryGetValue(region, out var cached)) return cached;

            var list = new List<MonsterEntry>();
            var seen = new HashSet<Object>();
            foreach (var prefab in region.MonsterPool)
            {
                if (prefab == null) continue;
                var controller = prefab.GetComponent<MonsterController>();
                Object key = controller != null && controller.Data != null ? (Object)controller.Data : prefab;
                if (!seen.Add(key)) continue;

                var renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
                list.Add(new MonsterEntry
                {
                    Name = GetMonsterName(prefab),
                    Icon = renderer != null ? renderer.sprite : null,
                });
            }
            _monsterCache[region] = list;
            return list;
        }

        // 기본은 아이콘 오른쪽. 지도 밖으로 나가면 왼쪽에 붙이고, 세로는 지도 안으로 당긴다
        private void PlaceEntryPanel(RectTransform anchor)
        {
            GetMapLocalRect(anchor, out Vector2 min, out Vector2 max);
            Vector2 size = _entryPanel.rect.size;
            float halfW = MapWidth * 0.5f;
            float halfH = MapHeight * 0.5f;

            bool placeRight = max.x + _panelGap + size.x <= halfW;
            float x = placeRight ? max.x + _panelGap : min.x - _panelGap - size.x;
            float y = Mathf.Clamp((min.y + max.y) * 0.5f - size.y * 0.5f, -halfH, halfH - size.y);

            // 패널 피벗을 왼쪽 아래로 두고 계산값을 쓴다. 호버 확대(1.1×)로 생긴 소수 좌표는 지도 픽셀 격자에 맞춰 반올림한다
            _entryPanel.pivot = Vector2.zero;
            _entryPanel.anchorMin = _entryPanel.anchorMax = new Vector2(0.5f, 0.5f);
            _entryPanel.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y));
        }

        private void HandleNodeHovered(WorldMapNodeUI node, bool hovered)
        {
            if (hovered)
            {
                ShowTooltip(node);
            }
            else if (_hoveredNode == node)
            {
                HideTooltip();
            }
        }

        private void ShowTooltip(WorldMapNodeUI node)
        {
            if (_tooltip == null || _tooltipText == null || string.IsNullOrEmpty(node.Label)) return;

            _hoveredNode = node;
            _tooltipText.text = node.Label;
            Vector2 textSize = _tooltipText.GetPreferredValues(node.Label);
            Vector2 size = textSize + _tooltipPadding * 2f;
            _tooltip.sizeDelta = new Vector2(Mathf.Ceil(size.x), Mathf.Ceil(size.y));

            GetMapLocalRect(node.Rect, out Vector2 min, out Vector2 max);
            float halfW = MapWidth * 0.5f;
            float halfH = MapHeight * 0.5f;
            float x = Mathf.Clamp((min.x + max.x) * 0.5f - _tooltip.sizeDelta.x * 0.5f, -halfW, halfW - _tooltip.sizeDelta.x);
            float y = Mathf.Min(max.y + 2f, halfH - _tooltip.sizeDelta.y);

            _tooltip.pivot = Vector2.zero;
            _tooltip.anchorMin = _tooltip.anchorMax = new Vector2(0.5f, 0.5f);
            _tooltip.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y));
            _tooltip.SetAsLastSibling();
            _tooltip.gameObject.SetActive(true);
        }

        private void HideTooltip()
        {
            _hoveredNode = null;
            if (_tooltip != null) _tooltip.gameObject.SetActive(false);
        }

        // 노드가 입장 패널 안(몬스터 아이콘)에 있어도 같은 기준으로 쓰도록 지도 루트 로컬 좌표(가운데 0)로 바꾼다
        private void GetMapLocalRect(RectTransform target, out Vector2 min, out Vector2 max)
        {
            target.GetWorldCorners(_corners);
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector2 local = _mapRoot.InverseTransformPoint(_corners[i]);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
        }
    }
}
