using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BagBack100.UI;

namespace BagBack100.EditorTools
{
    /// <summary>
    /// Role: UI 키트 배치(12 설계안)를 인벤토리·장비·핫바 프리팹과 허브·던전 씬에 적용하는 에디터 메뉴.
    ///       패널은 1024×576 기준 단위로 만들고 배율 1080/576을 걸어, 1080 기준인 기존 캔버스·UI를 건드리지 않는다.
    ///       여러 번 실행해도 같은 결과가 나오게 이름으로 찾고 없으면 만든다.
    /// Dependencies: InventoryUIController, SecurePouchUIController, EquipmentSlotUI, PlayerStatsUIController, QuickSlotBarUI,
    ///               QuickSlotSlotUI, DashCooldownUI, GridCellUI, TrashSlotUI
    /// Events: 없음
    /// </summary>
    public static class UIKitLayoutBuilder
    {
        private const string KitFolder = "Assets/Art/Sprites/UI/Kit/";
        private const string PrefabFolder = "Assets/Resources/Prefabs/UIPrefabs/";
        private const string SlotPrefab = PrefabFolder + "Slot.prefab";
        private const string PouchSlotPrefab = PrefabFolder + "Slot_Pouch.prefab";
        private const string InventoryPrefab = PrefabFolder + "InventoryPanel.prefab";
        private const string EquipmentPrefab = PrefabFolder + "EquipmentPanel.prefab";
        private const string QuickSlotPrefab = PrefabFolder + "QuickSlotBar.prefab";
        private const string ItemUIPrefab = "Assets/Resources/Prefabs/ItemUI.prefab";
        private const string DungeonScene = "Assets/Scenes/03_DungeonScene.unity";
        private const string HubScene = "Assets/Scenes/02_HubScene.unity";
        private const string StoragePrefab = PrefabFolder + "StoragePanel.prefab";
        private const string HubSpriteFolder = "Assets/Art/Sprites/Hub/";

        // 기존 캔버스는 1080 높이 기준 픽셀. 스펙은 576 높이 기준이라 패널에 이 배율을 건다
        private const float KitScale = 1080f / 576f;
        // 아트 1px = 기준 단위 2 (스프라이트 PPU 100, 캔버스 Reference PPU 100 기준)
        private const float PixelMultiplier = 0.5f;
        // 잡화상·대장간 패널(1080 기준 단위 750×950)을 가방 패널과 같은 화면 높이(438 × KitScale)로 줄이는 배율
        private const float LegacyPanelScale = 438f * KitScale / 950f;
        // 그 배율에서도 가방 패널과 같은 테두리 두께가 나오게 하는 Image 배율
        private const float HubPixelMultiplier = PixelMultiplier / KitScale * LegacyPanelScale;

        private static readonly Color HeaderTextColor = new Color(0.85f, 0.82f, 0.76f, 1f);
        private static readonly Color HintColor = new Color(1f, 1f, 1f, 0.35f);

        [MenuItem("Tools/UI Kit/Build Inventory And Hotbar")]
        public static void BuildAll()
        {
            BuildSlotPrefabs();
            BuildItemUI();
            BuildEquipmentPanel();
            BuildInventoryPanel();
            BuildQuickSlotBar();
            AssetDatabase.SaveAssets();
            Debug.Log("[UIKitLayoutBuilder] 인벤토리·장비·핫바 프리팹 적용 완료");
        }

        [MenuItem("Tools/UI Kit/Apply To Scenes")]
        public static void ApplyToScenes()
        {
            // 씬을 Single로 열어 바꾸므로, 저장 안 한 변경이 있으면 먼저 묻는다
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ApplyScenes();
            Debug.Log("[UIKitLayoutBuilder] 허브·던전 씬 적용 완료");
        }

        // ================= Prefabs =================

        private static void BuildSlotPrefabs()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PouchSlotPrefab) == null)
            {
                AssetDatabase.CopyAsset(SlotPrefab, PouchSlotPrefab);
            }
            SetupCellPrefab(SlotPrefab, "ui_slot_thin_normal", "ui_slot_thin_locked");
            SetupCellPrefab(PouchSlotPrefab, "ui_slot_thick_equip", "ui_slot_thick_locked");
        }

        private static void SetupCellPrefab(string path, string normal, string locked)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            var image = root.GetComponent<Image>();
            SetSliced(image, Kit(normal));
            var cell = GetOrAdd<GridCellUI>(root);
            SetRef(cell, "_normalSprite", Kit(normal));
            SetRef(cell, "_lockedSprite", Kit(locked));
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void BuildItemUI()
        {
            var root = PrefabUtility.LoadPrefabContents(ItemUIPrefab);
            var icon = (RectTransform)root.transform.Find("Image");
            Stretch(icon, 0.1f, 0.1f, 0.9f, 0.9f);
            var count = (RectTransform)root.transform.Find("Text (TMP)");
            Stretch(count, 0.05f, 0.02f, 0.95f, 0.38f);
            var text = count.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.BottomRight;
            text.enableAutoSizing = true;
            text.fontSizeMin = 4f;
            text.fontSizeMax = 8f; // 칸 단위(42) 기준. 숫자가 아이콘을 덮지 않게
            text.raycastTarget = false;
            PrefabUtility.SaveAsPrefabAsset(root, ItemUIPrefab);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void BuildEquipmentPanel()
        {
            var root = PrefabUtility.LoadPrefabContents(EquipmentPrefab);
            var rootImage = root.GetComponent<Image>();
            if (rootImage != null) rootImage.enabled = false;

            var slotGroup = (RectTransform)root.transform.Find("SlotGroup");
            TopLeft(slotGroup, 0, 0, 161, 79);
            var grid = GetOrAdd<GridLayoutGroup>(slotGroup.gameObject);
            grid.cellSize = new Vector2(38, 38);
            grid.spacing = new Vector2(3, 3);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;

            // 시안 순서: 윗줄 무기·투구·갑옷·신발 / 아랫줄 배낭·반지·목걸이·귀걸이
            string[] order = { "Weapon", "Helmat", "Armor", "Shoes", "Backpack", "Ring", "Necklace", "Earring" };
            string[] hints = { "weapon", "helmet", "armor", "boots", "backpack", "ring", "necklace", "earring" };
            for (int i = 0; i < order.Length; i++)
            {
                var slot = slotGroup.Find(order[i]);
                if (slot == null) { Debug.LogWarning($"[UIKitLayoutBuilder] 장비 칸 없음: {order[i]}"); continue; }
                slot.SetSiblingIndex(i);
                SetSliced(slot.GetComponent<Image>(), Kit("ui_slot_thick_equip"));

                var hint = GetOrCreateChild(slot, "Hint");
                StretchInset(hint, 3);
                var hintImage = GetOrAdd<Image>(hint.gameObject);
                hintImage.sprite = Kit("ui_equip_hint_" + hints[i]);
                hintImage.type = Image.Type.Simple;
                hintImage.color = HintColor;
                hintImage.raycastTarget = false;

                var icon = GetOrCreateChild(slot, "Icon");
                StretchInset(icon, 4);
                var iconImage = GetOrAdd<Image>(icon.gameObject);
                iconImage.raycastTarget = false;
                iconImage.preserveAspect = true;
                iconImage.color = new Color(1f, 1f, 1f, 0f); // 장착 전에는 투명 (EquipmentSlotUI.UpdateSlot이 갱신)

                var slotUI = slot.GetComponent<EquipmentSlotUI>();
                SetRef(slotUI, "_itemIcon", iconImage);
                SetRef(slotUI, "_hintOverlay", hintImage);
                SetRef(slotUI, "_defaultIcon", null);
            }

            var stats = (RectTransform)root.transform.Find("StatsGroup");
            TopLeft(stats, 171, 0, 100, 79);
            string[] statNames = { "HPText", "MPText", "ATKText", "DEFText" };
            for (int i = 0; i < statNames.Length; i++)
            {
                var t = (RectTransform)stats.Find(statNames[i]);
                if (t == null) continue;
                TopLeft(t, 0, -2 - i * 16, 100, 14);
                StyleText(t.GetComponent<TextMeshProUGUI>(), 8f, TextAlignmentOptions.Left);
            }

            PrefabUtility.SaveAsPrefabAsset(root, EquipmentPrefab);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void BuildInventoryPanel()
        {
            var root = PrefabUtility.LoadPrefabContents(InventoryPrefab);
            var rootImage = root.GetComponent<Image>();
            // 루트는 화면 왼쪽 절반 영역일 뿐이라 배경을 그리지 않는다 (게임 화면 클릭을 막지 않게)
            if (rootImage != null) rootImage.enabled = false;
            var controller = root.GetComponent<InventoryUIController>();

            // 패널: 왼쪽 절반의 정중앙, 292×438 기준 단위 × KitScale
            var panel = (RectTransform)root.transform.Find("Inventory");
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(292, 438);
            panel.anchoredPosition = Vector2.zero;
            panel.localScale = new Vector3(KitScale, KitScale, 1f);
            SetSliced(panel.GetComponent<Image>(), Kit("ui_panel_9slice"));

            BuildHeader(panel, "EquipHeader", "EQUIP", 10, -10);

            var equip = (RectTransform)root.transform.Find("EquipmentPanel");
            if (equip == null) equip = (RectTransform)panel.Find("EquipmentPanel");
            if (equip != null)
            {
                // 예전 배치 때 남은 중첩 프리팹 오버라이드(스탯 위치·글자 크기 등)가 EquipmentPanel 프리팹 배치를 덮으므로 먼저 되돌린다
                if (PrefabUtility.IsAnyPrefabInstanceRoot(equip.gameObject))
                    PrefabUtility.RevertPrefabInstance(equip.gameObject, InteractionMode.AutomatedAction);
                equip.SetParent(panel, false);
                TopLeft(equip, 10, -32, 272, 80);
                equip.localScale = Vector3.one;
            }

            var bagHeader = BuildHeader(panel, "BagHeader", "BAG (0/25)", 10, -122);
            SetRef(controller, "_bagHeaderText", bagHeader);

            // BAG 스크롤: Viewport(RectMask2D) 안에 GridContainer, 3줄이 보인다
            var viewport = BuildScrollGrid(panel, "Bag", 8, -144, 267, 132, 6);
            SetRef(controller, "_slotViewport", viewport);

            var itemContainer = (RectTransform)panel.Find("ItemContainer");
            if (itemContainer != null) StretchInset(itemContainer, 0);

            // 하단: Secure Pouch 2칸(왼쪽) + 휴지통(오른쪽 끝)
            var pouch = (RectTransform)panel.Find("SecurePouch");
            TopLeft(pouch, 8, -372, 200, 54);
            var pouchLabel = (RectTransform)pouch.Find("Label");
            TopLeft(pouchLabel, 0, 0, 200, 14);
            var pouchLabelText = pouchLabel.GetComponent<TextMeshProUGUI>();
            pouchLabelText.text = "Secure Pouch";
            StyleText(pouchLabelText, 7f, TextAlignmentOptions.Left);
            var pouchGridRect = (RectTransform)pouch.Find("GridContainer");
            TopLeft(pouchGridRect, 0, -18, 76, 36);
            var pouchGrid = pouchGridRect.GetComponent<GridLayoutGroup>();
            pouchGrid.cellSize = new Vector2(36, 36);
            pouchGrid.spacing = new Vector2(4, 4);
            pouchGrid.padding = new RectOffset(0, 0, 0, 0);
            pouchGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            pouchGrid.constraintCount = 2;
            pouchGrid.childAlignment = TextAnchor.UpperLeft;
            SetRef(pouch.GetComponent<SecurePouchUIController>(), "_gridCellPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PouchSlotPrefab));

            var trash = (RectTransform)panel.Find("TrashSlot");
            TopLeft(trash, 248, -390, 36, 36);
            var trashImage = trash.GetComponent<Image>();
            trashImage.sprite = Kit("ui_slot_thin_trash");
            trashImage.type = Image.Type.Simple;
            var trashLabel = trash.Find("Label");
            if (trashLabel != null) trashLabel.gameObject.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, InventoryPrefab);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // Viewport(RectMask2D·ScrollRect) 안에 기존 GridContainer를 넣고 오른쪽에 세로 스크롤바를 단다. 칸 42, 간격 3
        private static RectTransform BuildScrollGrid(RectTransform panel, string prefix, float x, float y, float w, float h, int columns)
        {
            var viewport = GetOrCreateChild(panel, prefix + "Viewport");
            TopLeft(viewport, x, y, w, h);
            var viewportImage = GetOrAdd<Image>(viewport.gameObject);
            viewportImage.sprite = null;
            viewportImage.color = new Color(0, 0, 0, 0); // 스크롤 휠 입력을 받기 위한 투명 영역
            GetOrAdd<RectMask2D>(viewport.gameObject);
            var scroll = GetOrAdd<ScrollRect>(viewport.gameObject);

            var gridRect = (RectTransform)panel.Find("GridContainer");
            if (gridRect == null) gridRect = (RectTransform)viewport.Find("GridContainer");
            gridRect.SetParent(viewport, false);
            gridRect.anchorMin = new Vector2(0, 1);
            gridRect.anchorMax = new Vector2(1, 1);
            gridRect.pivot = new Vector2(0.5f, 1f);
            gridRect.anchoredPosition = Vector2.zero;
            gridRect.sizeDelta = Vector2.zero;
            gridRect.localScale = Vector3.one;
            var gridImage = gridRect.GetComponent<Image>();
            if (gridImage != null) gridImage.enabled = false;
            var grid = gridRect.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(42, 42);
            grid.spacing = new Vector2(3, 3);
            grid.padding = new RectOffset(0, 0, 0, 0);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            var fitter = GetOrAdd<ContentSizeFitter>(gridRect.gameObject);
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scrollbarRect = GetOrCreateChild(panel, prefix + "Scrollbar");
            TopLeft(scrollbarRect, x + w + 3, y, 6, h);
            SetSliced(GetOrAdd<Image>(scrollbarRect.gameObject), Kit("ui_scroll_track_9slice"));
            var slidingArea = GetOrCreateChild(scrollbarRect, "SlidingArea");
            StretchInset(slidingArea, 0);
            var handle = GetOrCreateChild(slidingArea, "Handle");
            StretchInset(handle, 0);
            var handleImage = GetOrAdd<Image>(handle.gameObject);
            SetSliced(handleImage, Kit("ui_scroll_thumb_9slice"));
            var scrollbar = GetOrAdd<Scrollbar>(scrollbarRect.gameObject);
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;

            scroll.content = gridRect;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 20f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return viewport;
        }

        private static TextMeshProUGUI BuildHeader(RectTransform panel, string name, string label, float x, float y)
        {
            var header = GetOrCreateChild(panel, name);
            TopLeft(header, x, y, 272, 16);
            SetSliced(GetOrAdd<Image>(header.gameObject), Kit("ui_header_bar"));
            header.GetComponent<Image>().raycastTarget = false;
            var textRect = GetOrCreateChild(header, "Label");
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 0);
            textRect.offsetMax = new Vector2(-4, 0);
            var text = GetOrAdd<TextMeshProUGUI>(textRect.gameObject);
            text.text = label;
            StyleText(text, 8f, TextAlignmentOptions.Left);
            return text;
        }

        private static void BuildQuickSlotBar()
        {
            var root = PrefabUtility.LoadPrefabContents(QuickSlotPrefab);
            PlaceHotbarRoot((RectTransform)root.transform);
            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 4;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;

            for (int i = 1; i <= 3; i++)
            {
                var slot = root.transform.Find("QuickSlot_" + i);
                if (slot != null) StyleHotbarSlot((RectTransform)slot, i.ToString());
            }

            // DASH 칸: QuickSlot_3을 복제해 퀵슬롯 컴포넌트를 빼고 DashCooldownUI를 붙인다
            var dash = root.transform.Find("DashSlot");
            if (dash == null)
            {
                var source = root.transform.Find("QuickSlot_3");
                dash = Object.Instantiate(source.gameObject, root.transform).transform;
                dash.name = "DashSlot";
                Object.DestroyImmediate(dash.GetComponent<QuickSlotSlotUI>());
            }
            dash.SetAsLastSibling();
            StyleHotbarSlot((RectTransform)dash, "DASH");
            var dashUI = GetOrAdd<DashCooldownUI>(dash.gameObject);
            SetRef(dashUI, "_cooldownOverlay", dash.Find("Cooldown").GetComponent<Image>());
            var dashCount = dash.Find("Count");
            if (dashCount != null) dashCount.GetComponent<TextMeshProUGUI>().text = "";
            SetRef(root.GetComponent<QuickSlotBarUI>(), "_dashSlot", dash.gameObject);

            PrefabUtility.SaveAsPrefabAsset(root, QuickSlotPrefab);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 화면 아래 중앙, 44×44 4칸 + 간격 4, 바닥에서 8 기준 단위
        private static void PlaceHotbarRoot(RectTransform rootRect)
        {
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.sizeDelta = new Vector2(4 * 44 + 3 * 4, 44);
            rootRect.anchoredPosition = new Vector2(0, 8 * KitScale);
            rootRect.localScale = new Vector3(KitScale, KitScale, 1f);
        }

        private static void StyleHotbarSlot(RectTransform slot, string key)
        {
            slot.sizeDelta = new Vector2(44, 44);
            SetSliced(slot.GetComponent<Image>(), Kit("ui_slot_thick_normal"));
            var icon = (RectTransform)slot.Find("Icon");
            if (icon != null) StretchInset(icon, 6);
            var cooldown = (RectTransform)slot.Find("Cooldown");
            if (cooldown != null) StretchInset(cooldown, 2);
            var count = (RectTransform)slot.Find("Count");
            if (count != null)
            {
                StretchInset(count, 3);
                StyleText(count.GetComponent<TextMeshProUGUI>(), 7f, TextAlignmentOptions.BottomRight);
            }
            var keyRect = (RectTransform)slot.Find("Key");
            if (keyRect != null)
            {
                StretchInset(keyRect, 3);
                var keyText = keyRect.GetComponent<TextMeshProUGUI>();
                keyText.text = key;
                StyleText(keyText, 7f, TextAlignmentOptions.TopLeft);
            }
        }

        // ================= Hub =================

        [MenuItem("Tools/UI Kit/Apply Hub")]
        public static void ApplyHub()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildStoragePanel();
            AssetDatabase.SaveAssets();

            var active = SceneManager.GetActiveScene().path;
            var hub = EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);
            foreach (var go in hub.GetRootGameObjects())
            {
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    switch (t.name)
                    {
                        case "StoragePanel":
                            RevertChildRectOverrides(t.gameObject);
                            PlaceRightHalf((RectTransform)t);
                            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                            break;
                        case "ShopPanel":
                        case "BlacksmithPanel":
                            PlaceRightHalf((RectTransform)t);
                            SkinLegacyPanel(t);
                            break;
                        case "GeneralStore": ApplyHubObject(t, "hub_shop"); break;
                        case "Storage": if (t.GetComponent<SpriteRenderer>() != null) ApplyHubObject(t, "hub_stash"); break;
                        case "BlackSmith": ApplyHubObject(t, "hub_forge"); break;
                    }
                }
            }
            EditorSceneManager.MarkSceneDirty(hub);
            EditorSceneManager.SaveScene(hub);
            if (!string.IsNullOrEmpty(active) && active != HubScene) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
            Debug.Log("[UIKitLayoutBuilder] 허브 패널·오브젝트 적용 완료");
        }

        // 창고는 가방과 같은 키트 단위 패널(292×438 × KitScale)로 만들어 좌우 대칭을 맞춘다. 6열, 8줄이 보인다
        private static void BuildStoragePanel()
        {
            var root = PrefabUtility.LoadPrefabContents(StoragePrefab);
            var rootImage = root.GetComponent<Image>();
            if (rootImage != null) rootImage.enabled = false;
            var panel = (RectTransform)root.transform.Find("Storage");
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(292, 438);
            panel.anchoredPosition = Vector2.zero;
            panel.localScale = new Vector3(KitScale, KitScale, 1f);
            SetSliced(panel.GetComponent<Image>(), Kit("ui_panel_9slice"));
            BuildHeader(panel, "StorageHeader", "STORAGE", 10, -10);
            var viewport = BuildScrollGrid(panel, "Storage", 8, -34, 267, 8 * 42 + 7 * 3, 6);
            SetRef(root.GetComponent<StorageUIController>(), "_slotViewport", viewport);
            var itemContainer = (RectTransform)panel.Find("ItemContainer");
            if (itemContainer != null) StretchInset(itemContainer, 0);
            PrefabUtility.SaveAsPrefabAsset(root, StoragePrefab);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 화면 오른쪽 절반. 안의 패널은 가운데 앵커라 그 중앙에 놓인다
        private static void PlaceRightHalf(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        // 잡화상·대장간은 내용이 1080 기준 단위라 크기는 두고 배경·제목·버튼·판매 칸·상세 창 스킨만 바꾼다
        private static void SkinLegacyPanel(Transform panelRoot)
        {
            var background = (RectTransform)panelRoot.Find("Background");
            if (background == null) return;
            background.anchorMin = background.anchorMax = new Vector2(0.5f, 0.5f);
            background.pivot = new Vector2(0.5f, 0.5f);
            background.anchoredPosition = Vector2.zero;
            background.localScale = new Vector3(LegacyPanelScale, LegacyPanelScale, 1f);
            SetSliced(background.GetComponent<Image>(), Kit("ui_panel_9slice"), HubPixelMultiplier);

            var title = (RectTransform)background.Find("Title");
            if (title != null)
            {
                var bar = GetOrCreateChild(background, "TitleBar");
                bar.SetSiblingIndex(title.GetSiblingIndex());
                bar.anchorMin = new Vector2(0f, 1f);
                bar.anchorMax = new Vector2(1f, 1f);
                bar.pivot = new Vector2(0.5f, 1f);
                bar.offsetMin = new Vector2(18f, -18f - 60f);
                bar.offsetMax = new Vector2(-18f, -18f);
                var barImage = GetOrAdd<Image>(bar.gameObject);
                SetSliced(barImage, Kit("ui_header_bar"), HubPixelMultiplier);
                barImage.raycastTarget = false;
                var titleText = title.GetComponent<TextMeshProUGUI>();
                if (titleText != null) titleText.color = HeaderTextColor;
            }

            foreach (var button in background.GetComponentsInChildren<Button>(true))
            {
                var image = button.GetComponent<Image>();
                if (image == null) continue;
                SetSliced(image, Kit("ui_btn_normal_9slice"), HubPixelMultiplier);
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState
                {
                    highlightedSprite = Kit("ui_btn_hover_9slice"),
                    selectedSprite = Kit("ui_btn_hover_9slice"),
                    pressedSprite = Kit("ui_btn_pressed_9slice"),
                    disabledSprite = Kit("ui_btn_normal_9slice"),
                };
            }

            var sellSlot = background.Find("SellSlot");
            if (sellSlot != null) SetSliced(sellSlot.GetComponent<Image>(), Kit("ui_slot_thick_normal"), HubPixelMultiplier);
            var detail = background.Find("TechTree/Detail");
            if (detail != null) SetSliced(detail.GetComponent<Image>(), Kit("ui_tooltip_9slice"), HubPixelMultiplier);
        }

        // 스프라이트 교체. 피벗이 아래 중앙이라, 예전 가운데 피벗 스프라이트의 바닥에 맞춰 내린다. 콜라이더는 그림 크기에 맞춘다
        private static void ApplyHubObject(Transform obj, string spriteName)
        {
            var renderer = obj.GetComponent<SpriteRenderer>();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(HubSpriteFolder + spriteName + ".png");
            if (renderer == null || sprite == null) { Debug.LogWarning("[UIKitLayoutBuilder] 허브 오브젝트 적용 실패: " + obj.name); return; }
            if (renderer.sprite != sprite)
            {
                var old = renderer.sprite;
                bool wasCentered = old != null && Mathf.Abs(old.pivot.y - old.rect.height * 0.5f) < 0.01f;
                if (wasCentered)
                {
                    float oldHalf = old.bounds.extents.y * obj.localScale.y;
                    obj.position -= new Vector3(0f, oldHalf, 0f);
                }
                renderer.sprite = sprite;
            }
            var collider = obj.GetComponent<BoxCollider2D>();
            if (collider != null)
            {
                collider.size = sprite.bounds.size;
                collider.offset = sprite.bounds.center;
            }
        }

        // ================= Scenes =================

        private static void ApplyScenes()
        {
            var active = SceneManager.GetActiveScene().path;

            var dungeon = EditorSceneManager.OpenScene(DungeonScene, OpenSceneMode.Single);
            foreach (var go in dungeon.GetRootGameObjects())
            {
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "InventoryPanelRoot")
                    {
                        StretchInset((RectTransform)t, 0);
                        var hl = t.GetComponent<HorizontalLayoutGroup>();
                        if (hl != null) hl.enabled = false; // 패널 하나만 담아 레이아웃이 필요 없고, 왼쪽 절반 앵커를 덮어쓴다
                    }
                }
                // 기존 씬의 별도 DASH 칸은 핫바 프리팹 안으로 옮겼다
                var oldDash = FindDirectChild(go.transform, "Canvas", "DashSlot");
                if (oldDash != null) Object.DestroyImmediate(oldDash.gameObject);
            }
            ApplyInventoryRoots(dungeon);
            ApplyQuickSlotBars(dungeon, true);
            EditorSceneManager.MarkSceneDirty(dungeon);
            EditorSceneManager.SaveScene(dungeon);

            var hub = EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);
            ApplyInventoryRoots(hub);
            ApplyQuickSlotBars(hub, false);
            EditorSceneManager.MarkSceneDirty(hub);
            EditorSceneManager.SaveScene(hub);

            if (!string.IsNullOrEmpty(active)) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
        }

        private static void ApplyInventoryRoots(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                foreach (var inv in go.GetComponentsInChildren<InventoryUIController>(true))
                {
                    RevertChildRectOverrides(inv.gameObject);
                    // 화면 왼쪽 절반. 패널은 그 중앙에 놓인다
                    var rt = (RectTransform)inv.transform;
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = Vector2.zero;
                    rt.anchoredPosition = Vector2.zero;
                    rt.localScale = Vector3.one;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rt);
                }
            }
        }

        private static void ApplyQuickSlotBars(Scene scene, bool showDash)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                foreach (var bar in go.GetComponentsInChildren<QuickSlotBarUI>(true))
                {
                    // 씬 오버라이드가 프리팹 배치를 덮지 않게 루트 RectTransform 오버라이드를 되돌린다
                    PrefabUtility.RevertObjectOverride(bar.transform, InteractionMode.AutomatedAction);
                    RevertChildRectOverrides(bar.gameObject);
                    // 프리팹 인스턴스 루트의 RectTransform 값은 씬에 늘 저장되므로 직접 맞춘다
                    PlaceHotbarRoot((RectTransform)bar.transform);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(bar.transform);
                    SetBool(bar, "_showDashSlot", showDash);
                }
            }
        }

        // 프리팹 구조가 바뀌면 Unity가 씬 인스턴스의 하위 RectTransform에 앵커·크기 0 같은 오버라이드를 남겨 새 배치를 덮는다.
        // 레이아웃은 프리팹이 정하므로 인스턴스 루트를 뺀 하위 RectTransform 오버라이드만 되돌린다 (컴포넌트 참조 오버라이드는 유지)
        private static void RevertChildRectOverrides(GameObject instanceRoot)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(instanceRoot)) return;
            foreach (var rect in instanceRoot.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect.gameObject == instanceRoot) continue;
                if (!PrefabUtility.IsPartOfPrefabInstance(rect)) continue;
                if (PrefabUtility.HasPrefabInstanceAnyOverrides(PrefabUtility.GetNearestPrefabInstanceRoot(rect), false) &&
                    PrefabUtility.GetPropertyModifications(rect) != null)
                {
                    PrefabUtility.RevertObjectOverride(rect, InteractionMode.AutomatedAction);
                }
            }
        }

        // ================= Helpers =================

        private static Sprite Kit(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(KitFolder + name + ".png");
            if (sprite == null) Debug.LogWarning($"[UIKitLayoutBuilder] 스프라이트 없음: {name}");
            return sprite;
        }

        private static void SetSliced(Image image, Sprite sprite, float multiplier = PixelMultiplier)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = multiplier;
            image.color = Color.white;
            image.enabled = true;
        }

        private static void StyleText(TextMeshProUGUI text, float size, TextAlignmentOptions alignment)
        {
            if (text == null) return;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.alignment = alignment;
            text.color = HeaderTextColor;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.margin = Vector4.zero;
        }

        // 부모 왼쪽 위 기준 (x, y는 오른쪽·아래가 +x·-y)
        private static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            rt.localScale = Vector3.one;
        }

        private static void StretchInset(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            rt.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rt, float minX, float minY, float maxX, float maxY)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
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

        private static Transform FindDirectChild(Transform root, string canvasName, string childName)
        {
            if (root.name != canvasName) return null;
            return root.Find(childName);
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static void SetRef(Object target, string field, Object value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[UIKitLayoutBuilder] 필드 없음: {target.GetType().Name}.{field}"); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) return;
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
