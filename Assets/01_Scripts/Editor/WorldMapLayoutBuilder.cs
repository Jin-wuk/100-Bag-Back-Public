using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BagBack100.Dungeon;
using BagBack100.Dungeon.Data;
using BagBack100.Hub;

namespace BagBack100.EditorTools
{
    /// <summary>
    /// Role: 허브 씬에 전체 화면 월드맵(양피지 지도 4겹, 던전 아이콘, 안개, 탑 옆 입장 패널, 툴팁)을 만들고
    ///       HubSystem·지역 1 SO(이름·설명·아이콘·좌표)를 연결하는 에디터 메뉴. 예전 DungeonPanel은 지운다.
    ///       지도 아래 좌표는 모두 지도 픽셀(344×192) 단위이고, 화면 배율은 DungeonEntranceUI가 열 때 정수배로 건다.
    ///       여러 번 실행해도 같은 결과가 나오게 이름으로 찾고 없으면 만든다.
    /// Dependencies: DungeonEntranceUI, WorldMapNodeUI, HubSystem, RegionConfigSO
    /// Events: 없음
    /// </summary>
    public static class WorldMapLayoutBuilder
    {
        private const string HubScene = "Assets/Scenes/02_HubScene.unity";
        private const string MapFolder = "Assets/Art/Sprites/UI/WorldMap/";
        private const string KitFolder = "Assets/Art/Sprites/UI/Kit/";
        private const string Region1ConfigPath = "Assets/Resources/Data_SO/FloorSO/Region1/Region1Config.asset";

        private const float MapW = 344f;
        private const float MapH = 192f;
        private const int MonsterSlotCount = 6;

        private static readonly Color TitleColor = new Color(0.98f, 0.91f, 0.72f, 1f);
        private static readonly Color BodyColor = new Color(0.85f, 0.82f, 0.76f, 1f);
        private static readonly Color LabelColor = new Color(0.62f, 0.78f, 0.55f, 1f);
        // UI 정점 색은 1을 넘지 못해, 평소에 살짝 어둡게 두고 호버 때 흰색으로 밝힌다
        private static readonly Color IconIdleColor = new Color(0.86f, 0.86f, 0.86f, 1f);

        [MenuItem("Tools/UI Kit/Build World Map")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ApplyRegion1Data();

            var active = SceneManager.GetActiveScene().path;
            var hub = EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);

            Canvas canvas = null;
            HubSystem hubSystem = null;
            foreach (var root in hub.GetRootGameObjects())
            {
                if (canvas == null && root.name == "Canvas") canvas = root.GetComponent<Canvas>();
                if (hubSystem == null) hubSystem = root.GetComponentInChildren<HubSystem>(true);
            }
            if (canvas == null || hubSystem == null)
            {
                Debug.LogError("[WorldMapLayoutBuilder] 허브 씬에서 Canvas 또는 HubSystem을 찾지 못했습니다.");
                return;
            }

            var oldPanel = canvas.transform.Find("DungeonPanel");
            if (oldPanel != null) Object.DestroyImmediate(oldPanel.gameObject);

            var ui = BuildWorldMap(canvas.transform);
            SetRef(hubSystem, "_dungeonEntranceUI", ui);

            EditorSceneManager.MarkSceneDirty(hub);
            EditorSceneManager.SaveScene(hub);
            if (!string.IsNullOrEmpty(active) && active != HubScene) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
            Debug.Log("[WorldMapLayoutBuilder] 허브 월드맵 적용 완료");
        }

        private static void ApplyRegion1Data()
        {
            var region = AssetDatabase.LoadAssetAtPath<RegionConfigSO>(Region1ConfigPath);
            if (region == null) { Debug.LogWarning("[WorldMapLayoutBuilder] Region1Config 없음"); return; }

            var so = new SerializedObject(region);
            so.FindProperty("_regionName").stringValue = "Ancient Forest";
            var desc = so.FindProperty("_description");
            if (string.IsNullOrEmpty(desc.stringValue))
            {
                desc.stringValue = "An ancient forest swallowed by amber. Beasts guard the old tower.";
            }
            so.FindProperty("_mapIcon").objectReferenceValue = MapSprite("ui_map_obj_forest-tower");
            so.FindProperty("_mapIconPosition").vector2IntValue = new Vector2Int(84, 58);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(region);
            AssetDatabase.SaveAssets();
        }

        private static DungeonEntranceUI BuildWorldMap(Transform canvas)
        {
            var panel = GetOrCreateChild(canvas, "WorldMapPanel");
            Stretch(panel);
            panel.SetAsLastSibling();
            var ui = GetOrAdd<DungeonEntranceUI>(panel.gameObject);

            // 전체 화면 어둡게 + 뒤쪽 허브 클릭 차단
            var dim = GetOrCreateChild(panel, "Dim");
            Stretch(dim);
            var dimImage = GetOrAdd<Image>(dim.gameObject);
            dimImage.sprite = null;
            dimImage.color = new Color(0.04f, 0.06f, 0.06f, 0.82f);
            dimImage.raycastTarget = true;

            var map = GetOrCreateChild(panel, "MapRoot");
            map.anchorMin = map.anchorMax = map.pivot = new Vector2(0.5f, 0.5f);
            map.anchoredPosition = Vector2.zero;
            map.sizeDelta = new Vector2(MapW, MapH);
            map.localScale = Vector3.one * 3f;

            Layer(map, "Bg", "ui_map_bg_parchment");
            Layer(map, "Continent", "ui_map_layer_continent");
            Layer(map, "Forest", "ui_map_region_forest");
            var fog = Layer(map, "Fog", "ui_map_layer_fog");
            fog.raycastTarget = true;
            var fogNode = GetOrAdd<WorldMapNodeUI>(fog.gameObject);
            SetNode(fogNode, "Coming Soon", false, 1f, null, 0.5f);

            // 지역 노드: 좌표·스프라이트는 DungeonEntranceUI가 RegionConfigSO로 연다. 여기서는 지역 1 값으로 미리 놓아 둔다
            var node = GetOrCreateChild(map, "Node_Region1");
            var tower = MapSprite("ui_map_obj_forest-tower");
            node.anchorMin = node.anchorMax = new Vector2(0f, 1f);
            node.pivot = new Vector2(0.5f, 0.5f);
            node.sizeDelta = new Vector2(48f, 48f);
            node.anchoredPosition = new Vector2(84f + 24f, -(58f + 24f));
            node.localScale = Vector3.one;
            var nodeImage = GetOrAdd<Image>(node.gameObject);
            nodeImage.sprite = tower;
            nodeImage.color = IconIdleColor;
            nodeImage.raycastTarget = true;
            var regionNode = GetOrAdd<WorldMapNodeUI>(node.gameObject);
            SetNode(regionNode, "Ancient Forest", true, 1.1f, nodeImage, 0f);

            var closeMap = Button(map, "CloseMapButton", "Close", 304f, -4f, 36f, 12f);
            Badge(map, 288f, -5.5f);
            BindClick(closeMap.GetComponent<Button>(), ui.HidePanel);

            var entry = BuildEntryPanel(map, ui, out var title, out var description, out var floors, out var boss,
                out var slots, out var icons);

            var tooltip = GetOrCreateChild(map, "Tooltip");
            tooltip.anchorMin = tooltip.anchorMax = new Vector2(0.5f, 0.5f);
            tooltip.pivot = Vector2.zero;
            tooltip.sizeDelta = new Vector2(48f, 11f);
            tooltip.localScale = Vector3.one;
            var tooltipImage = GetOrAdd<Image>(tooltip.gameObject);
            SetSliced(tooltipImage, Kit("ui_tooltip_9slice"));
            tooltipImage.raycastTarget = false;
            var tooltipText = Text(tooltip, "Text", "", 5f, TextAlignmentOptions.Center, BodyColor);
            Stretch((RectTransform)tooltipText.transform);
            tooltip.gameObject.SetActive(false);

            var so = new SerializedObject(ui);
            so.FindProperty("_mapRoot").objectReferenceValue = map;
            SetArray(so, "_regionNodes", new Object[] { regionNode });
            SetArray(so, "_regionIcons", new Object[] { nodeImage });
            so.FindProperty("_fogNode").objectReferenceValue = fogNode;
            so.FindProperty("_entryPanel").objectReferenceValue = entry;
            so.FindProperty("_titleText").objectReferenceValue = title;
            so.FindProperty("_descriptionText").objectReferenceValue = description;
            so.FindProperty("_floorsText").objectReferenceValue = floors;
            so.FindProperty("_bossText").objectReferenceValue = boss;
            SetArray(so, "_monsterSlots", slots);
            SetArray(so, "_monsterIcons", icons);
            so.FindProperty("_tooltip").objectReferenceValue = tooltip;
            so.FindProperty("_tooltipText").objectReferenceValue = tooltipText;
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.gameObject.SetActive(false);
            return ui;
        }

        // 패널 112×104 (지도 픽셀). 위치는 열 때 DungeonEntranceUI가 아이콘 옆으로 옮긴다
        private static RectTransform BuildEntryPanel(RectTransform map, DungeonEntranceUI ui,
            out TextMeshProUGUI title, out TextMeshProUGUI description, out TextMeshProUGUI floors, out TextMeshProUGUI boss,
            out Object[] slots, out Object[] icons)
        {
            var entry = GetOrCreateChild(map, "EntryPanel");
            entry.anchorMin = entry.anchorMax = new Vector2(0.5f, 0.5f);
            entry.pivot = Vector2.zero;
            entry.sizeDelta = new Vector2(112f, 104f);
            entry.anchoredPosition = new Vector2(-36f, -28f);
            entry.localScale = Vector3.one;
            var bg = GetOrAdd<Image>(entry.gameObject);
            SetSliced(bg, Kit("ui_panel_9slice"));
            bg.raycastTarget = true;

            title = Text(entry, "Title", "Ancient Forest", 8f, TextAlignmentOptions.TopLeft, TitleColor);
            TopLeft((RectTransform)title.transform, 7f, -6f, 98f, 10f);

            description = Text(entry, "Description", "", 5f, TextAlignmentOptions.TopLeft, BodyColor);
            description.enableWordWrapping = true;
            TopLeft((RectTransform)description.transform, 7f, -17f, 98f, 18f);

            floors = Text(entry, "Floors", "Floors 1-10", 5f, TextAlignmentOptions.TopLeft, BodyColor);
            TopLeft((RectTransform)floors.transform, 7f, -37f, 98f, 7f);

            var bossLabel = Text(entry, "BossLabel", "Boss", 5f, TextAlignmentOptions.TopLeft, LabelColor);
            TopLeft((RectTransform)bossLabel.transform, 7f, -45f, 24f, 7f);
            boss = Text(entry, "Boss", "-", 5f, TextAlignmentOptions.TopLeft, BodyColor);
            TopLeft((RectTransform)boss.transform, 31f, -45f, 74f, 14f);

            var monsterLabel = Text(entry, "MonsterLabel", "Monsters", 5f, TextAlignmentOptions.TopLeft, LabelColor);
            TopLeft((RectTransform)monsterLabel.transform, 7f, -60f, 98f, 7f);

            slots = new Object[MonsterSlotCount];
            icons = new Object[MonsterSlotCount];
            for (int i = 0; i < MonsterSlotCount; i++)
            {
                var slot = GetOrCreateChild(entry, "MonsterSlot" + i);
                slot.anchorMin = slot.anchorMax = new Vector2(0f, 1f);
                slot.pivot = new Vector2(0.5f, 0.5f);
                slot.sizeDelta = new Vector2(14f, 14f);
                slot.anchoredPosition = new Vector2(7f + 7f + i * 16f, -68f - 7f);
                slot.localScale = Vector3.one;
                var hit = GetOrAdd<Image>(slot.gameObject);
                hit.sprite = null;
                hit.color = new Color(0f, 0f, 0f, 0.25f);
                hit.raycastTarget = true;

                var iconRect = GetOrCreateChild(slot, "Icon");
                Stretch(iconRect);
                var icon = GetOrAdd<Image>(iconRect.gameObject);
                icon.preserveAspect = true;
                icon.raycastTarget = false;

                var node = GetOrAdd<WorldMapNodeUI>(slot.gameObject);
                SetNode(node, "", false, 1.15f, null, 0f);
                slots[i] = node;
                icons[i] = icon;
            }

            var enter = Button(entry, "EnterButton", "Enter", 7f, -86f, 46f, 12f);
            BindClick(enter.GetComponent<Button>(), ui.OnClickEnterDungeon);
            var close = Button(entry, "CloseButton", "Close", 59f, -86f, 46f, 12f);
            BindClick(close.GetComponent<Button>(), ui.CloseEntryPanel);
            Badge(entry, 91f, -6f);

            entry.gameObject.SetActive(false);
            return entry;
        }

        private static Image Layer(RectTransform map, string name, string sprite)
        {
            var rt = GetOrCreateChild(map, name);
            Stretch(rt);
            var image = GetOrAdd<Image>(rt.gameObject);
            image.sprite = MapSprite(sprite);
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform Button(RectTransform parent, string name, string label, float x, float y, float w, float h)
        {
            var rt = GetOrCreateChild(parent, name);
            TopLeft(rt, x, y, w, h);
            var image = GetOrAdd<Image>(rt.gameObject);
            SetSliced(image, Kit("ui_btn_normal_9slice"));
            var button = GetOrAdd<Button>(rt.gameObject);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = Kit("ui_btn_hover_9slice"),
                pressedSprite = Kit("ui_btn_pressed_9slice"),
                selectedSprite = Kit("ui_btn_normal_9slice"),
            };
            var text = Text(rt, "Label", label, 5f, TextAlignmentOptions.Center, BodyColor);
            Stretch((RectTransform)text.transform);
            return rt;
        }

        // ESC 키 배지 (부모 왼쪽 위 기준 x, y)
        private static void Badge(RectTransform parent, float x, float y)
        {
            var badge = GetOrCreateChild(parent, "EscBadge");
            TopLeft(badge, x, y, 14f, 9f);
            var image = GetOrAdd<Image>(badge.gameObject);
            SetSliced(image, Kit("ui_key_badge_9slice"));
            image.raycastTarget = false;
            var text = Text(badge, "Key", "ESC", 4f, TextAlignmentOptions.Center, BodyColor);
            Stretch((RectTransform)text.transform);
        }

        private static void SetNode(WorldMapNodeUI node, string label, bool clickable, float hoverScale, Image highlight, float alphaHit)
        {
            var so = new SerializedObject(node);
            so.FindProperty("_label").stringValue = label;
            so.FindProperty("_clickable").boolValue = clickable;
            so.FindProperty("_hoverScale").floatValue = hoverScale;
            so.FindProperty("_highlightTarget").objectReferenceValue = highlight;
            so.FindProperty("_alphaHitThreshold").floatValue = alphaHit;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindClick(Button button, UnityAction action)
        {
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                UnityEventTools.RemovePersistentListener(button.onClick, i);
            }
            UnityEventTools.AddPersistentListener(button.onClick, action);
        }

        private static TextMeshProUGUI Text(Transform parent, string name, string value, float size, TextAlignmentOptions alignment, Color color)
        {
            var rt = GetOrCreateChild(parent, name);
            var text = GetOrAdd<TextMeshProUGUI>(rt.gameObject);
            if (text.font == null) text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.margin = Vector4.zero;
            rt.localScale = Vector3.one;
            return text;
        }

        private static Sprite MapSprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MapFolder + name + ".png");
            if (sprite == null) Debug.LogWarning($"[WorldMapLayoutBuilder] 스프라이트 없음: {name}");
            return sprite;
        }

        private static Sprite Kit(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(KitFolder + name + ".png");
            if (sprite == null) Debug.LogWarning($"[WorldMapLayoutBuilder] 스프라이트 없음: {name}");
            return sprite;
        }

        // 지도 아래는 1 단위 = 지도 1픽셀이고 키트 스프라이트 PPU가 100이라 배율 1이면 아트 1px = 1 단위
        private static void SetSliced(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = Color.white;
        }

        private static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            rt.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        private static RectTransform GetOrCreateChild(Transform parent, string name)
        {
            var found = parent.Find(name);
            if (found != null) return (RectTransform)found;
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[WorldMapLayoutBuilder] 필드 없음: {target.GetType().Name}.{field}"); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(SerializedObject so, string field, Object[] values)
        {
            var prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
