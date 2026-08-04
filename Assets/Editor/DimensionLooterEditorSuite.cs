using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using System.IO;
using System.Collections.Generic;
using DimensionLooter.Managers;
using DimensionLooter.Gameplay.Combat;
using DimensionLooter.Gameplay;
using DimensionLooter.UI;
using DimensionLooter.UI.Inventory;
using DimensionLooter.UI.Lobby;

public class DimensionLooterEditorSuite : EditorWindow
{
    // =========================================================================
    // UILayoutExporter
    // =========================================================================
    [System.Serializable]
    public class RectData
    {
        public string name;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
    }

    [System.Serializable]
    public class LayoutDump
    {
        public List<RectData> elements = new List<RectData>();
    }

    [MenuItem("Tools/DimensionLooter/1. 현재 UI 레이아웃 추출하기 (Export Layout)")]
    public static void ExportLayout()
    {
        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null)
        {
            Debug.LogError("Canvas를 찾을 수 없습니다!");
            return;
        }

        LayoutDump dump = new LayoutDump();

        // 모든 자식 RectTransform 탐색
        RectTransform[] allRects = canvas.GetComponentsInChildren<RectTransform>(true);
        foreach (RectTransform rt in allRects)
        {
            RectData data = new RectData();
            data.name = rt.name;
            data.anchoredPosition = rt.anchoredPosition;
            data.sizeDelta = rt.sizeDelta;
            data.anchorMin = rt.anchorMin;
            data.anchorMax = rt.anchorMax;
            data.pivot = rt.pivot;
            dump.elements.Add(data);
        }

        string json = JsonUtility.ToJson(dump, true);
        string path = Path.Combine(Application.dataPath, "Editor", "LayoutDump.json");
        File.WriteAllText(path, json);
        
        AssetDatabase.Refresh();
        Debug.Log($"[UI 레이아웃 추출 성공] {path} 에 저장되었습니다! 이제 AI에게 알려주세요.");
    }

    // =========================================================================
    // MVPSceneSetup (Unified)
    // =========================================================================
    [MenuItem("Tools/DimensionLooter/2. Setup Full Scene (MVP 2.5)")]
    public static void SetupScene()
    {
        Debug.Log("[Setup] 씬 초기화 및 통합 셋업을 시작합니다...");
        // 내장 기본 스프라이트 가져오기 (UISprite)
        Sprite defaultSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // 방금 생성한 픽셀 아트 이미지들을 Sprite로 강제 임포트 세팅
        AssetDatabase.Refresh();
        string[] spritePaths = {
            "Assets/Resources/Sprites/UI/fantasy_forest_bg.jpg",
            "Assets/Resources/Sprites/UI/icon_stun.jpg",
            "Assets/Resources/Sprites/UI/icon_heal.jpg",
            "Assets/Resources/Sprites/UI/icon_flee.jpg"
        };
        foreach (var path in spritePaths)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
        }

        Sprite bgSprite = Resources.Load<Sprite>("Sprites/UI/fantasy_forest_bg");
        Sprite stunSprite = Resources.Load<Sprite>("Sprites/UI/icon_stun");
        Sprite healSprite = Resources.Load<Sprite>("Sprites/UI/icon_heal");
        Sprite fleeSprite = Resources.Load<Sprite>("Sprites/UI/icon_flee");

        // 0. 카메라 2D 직교 모드 설정 및 추적 스크립트 부착
        if (Camera.main != null)
        {
            Camera.main.orthographic = true;
            Camera.main.orthographicSize = 5f;
            Camera.main.transform.position = new Vector3(0, 0, -10);
            if (Camera.main.gameObject.GetComponent<DimensionLooter.Gameplay.CameraFollow>() == null)
            {
                Camera.main.gameObject.AddComponent<DimensionLooter.Gameplay.CameraFollow>();
            }
        }

        // 1. Prefab 폴더 생성
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs")) AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");

        // 기존에 셋업된 오브젝트들 정리 (중복 방지)
        string[] oldObjects = { "Grid", "Player", "Canvas", "EventSystem", "@Managers", "Monster_Dummy", "Pouch", "ExplorationUI" };
        foreach (string objName in oldObjects)
        {
            GameObject obj = GameObject.Find(objName);
            if (obj != null) DestroyImmediate(obj);
        }
        
        // 추가적으로 씬에 남아있는 Monster_Dummy(Clone) 같은 애들도 정리
        var allMonsters = Object.FindObjectsOfType<DimensionLooter.Gameplay.Combat.MonsterHealth>();
        foreach (var m in allMonsters)
        {
            DestroyImmediate(m.gameObject);
        }

        // 2. 그리드 및 타일맵 생성 (바닥 시각화용)
        GameObject gridObj = new GameObject("Grid");
        gridObj.AddComponent<Grid>();
        GameObject tilemapObj = new GameObject("FloorTilemap");
        tilemapObj.transform.SetParent(gridObj.transform);
        Tilemap tilemap = tilemapObj.AddComponent<Tilemap>();
        tilemapObj.AddComponent<TilemapRenderer>();

        // 더미 바닥 타일 에셋 생성
        Tile floorTile = ScriptableObject.CreateInstance<Tile>();
        floorTile.sprite = defaultSprite;
        floorTile.color = new Color(0.2f, 0.2f, 0.2f, 1f); // 어두운 회색 바닥
        
        // 10x10 바닥 깔기
        for (int x = -5; x <= 5; x++)
        {
            for (int y = -5; y <= 5; y++)
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), floorTile);
            }
        }

        // 3. 몬스터 더미 프리팹 (스프라이트 할당)
        GameObject monsterObj = new GameObject("Monster_Dummy");
        SpriteRenderer mSr = monsterObj.AddComponent<SpriteRenderer>();
        mSr.sprite = defaultSprite;
        mSr.color = Color.red; 
        mSr.sortingOrder = 10; // 바닥보다 위로 렌더링
        monsterObj.AddComponent<BoxCollider2D>().size = new Vector2(0.9f, 0.9f); // 타일보다 약간 작게
        monsterObj.AddComponent<MonsterHealth>();
        
        // 몬스터는 Obstacle 레이어로 설정 (임시로 Water 레이어(4) 사용)
        monsterObj.layer = 4;
        GameObject monsterPrefab = PrefabUtility.SaveAsPrefabAsset(monsterObj, "Assets/Resources/Prefabs/Monster_Dummy.prefab");
        DestroyImmediate(monsterObj);

        // 3.5 파우치(Pouch) 프리팹 생성
        GameObject pouchObj = new GameObject("Pouch");
        SpriteRenderer pouchSr = pouchObj.AddComponent<SpriteRenderer>();
        pouchSr.sprite = defaultSprite;
        pouchSr.color = new Color(0.6f, 0.3f, 0f); // 갈색
        pouchSr.sortingOrder = 9;
        pouchObj.AddComponent<BoxCollider2D>().size = new Vector2(0.8f, 0.8f);
        pouchObj.AddComponent<DimensionLooter.Gameplay.Inventory.PouchInteractable>();
        pouchObj.layer = 4; // 상호작용 레이어
        PrefabUtility.SaveAsPrefabAsset(pouchObj, "Assets/Resources/Prefabs/Pouch.prefab");
        DestroyImmediate(pouchObj);

        // 4. 플레이어 오브젝트
        GameObject playerObj = new GameObject("Player");
        playerObj.tag = "Player"; // 플레이어 태그 할당
        SpriteRenderer pSr = playerObj.AddComponent<SpriteRenderer>();
        pSr.sprite = defaultSprite;
        pSr.color = Color.blue; 
        pSr.sortingOrder = 10; // 바닥보다 위로 렌더링
        playerObj.AddComponent<PlayerStats>();
        PlayerController pc = playerObj.AddComponent<PlayerController>();
        
        // PlayerController 인스펙터 세팅 (ObstacleMask = Water 레이어)
        SerializedObject pcSo = new SerializedObject(pc);
        pcSo.FindProperty("_obstacleMask").intValue = 1 << 4; 
        pcSo.ApplyModifiedProperties();

        // 5. UI 캔버스 및 이벤트 시스템
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();

        // 6. Tooltip UI 셋업
        GameObject tooltipObj = new GameObject("TooltipPanel");
        tooltipObj.transform.SetParent(canvasObj.transform, false);
        Image tooltipBg = tooltipObj.AddComponent<Image>();
        tooltipBg.color = new Color(0, 0, 0, 0.8f);
        tooltipBg.raycastTarget = false; // 마우스 클릭 차단 방지
        tooltipObj.GetComponent<RectTransform>().sizeDelta = new Vector2(250, 80); // 툴팁 크기
        
        GameObject tooltipTextObj = new GameObject("Text");
        tooltipTextObj.transform.SetParent(tooltipObj.transform, false);
        Text tText = tooltipTextObj.AddComponent<Text>();
        tText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tText.color = Color.white;
        tText.alignment = TextAnchor.MiddleCenter;
        tText.raycastTarget = false; // 마우스 클릭 차단 방지
        tText.horizontalOverflow = HorizontalWrapMode.Overflow; // 글자 잘림 방지
        tText.verticalOverflow = VerticalWrapMode.Overflow;
        
        TooltipUI tooltipUI = canvasObj.AddComponent<TooltipUI>(); // 캔버스에 붙여서 Awake가 항상 실행되도록 함
        SerializedObject tSo = new SerializedObject(tooltipUI);
        tSo.FindProperty("_tooltipPanel").objectReferenceValue = tooltipObj;
        tSo.FindProperty("_infoText").objectReferenceValue = tText;
        tSo.ApplyModifiedProperties();

        tooltipObj.SetActive(false); // 씬 셋업 직후 에디터 상에서 거슬리지 않도록 비활성화

        // 7. CombatUI 셋업
        GameObject combatPanel = new GameObject("CombatPanel");
        combatPanel.transform.SetParent(canvasObj.transform, false);
        Image combatBg = combatPanel.AddComponent<Image>();
        combatBg.color = new Color(0, 0, 0, 0.4f); // 배경을 반투명하게 하여 0,0,0 필드가 보이도록 함
        RectTransform cpRect = combatPanel.GetComponent<RectTransform>();
        cpRect.anchorMin = Vector2.zero; cpRect.anchorMax = Vector2.one;
        cpRect.offsetMin = cpRect.offsetMax = Vector2.zero;

        CombatUI combatUI = canvasObj.AddComponent<CombatUI>(); // 룰 적용: Canvas에 부착

        Slider CreateSlider(string name, Transform parent, Color color, Vector2 anchoredPos)
        {
            GameObject sliderObj = new GameObject(name);
            sliderObj.transform.SetParent(parent, false);
            Slider slider = sliderObj.AddComponent<Slider>();
            slider.interactable = false; // 마우스로 조작 불가능하게 설정
            RectTransform rect = sliderObj.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(300, 30); // 1080p 해상도에 맞게 스탯바 크기 확대

            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillRect = fillArea.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero; // 오프셋 초기화

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = color;
            RectTransform fRect = fill.GetComponent<RectTransform>();
            fRect.anchorMin = Vector2.zero; fRect.anchorMax = Vector2.one;
            fRect.offsetMin = Vector2.zero; fRect.offsetMax = Vector2.zero; // 오프셋 초기화
            
            slider.fillRect = fRect;
            return slider;
        }

        Slider pSlider = CreateSlider("PlayerHpBar", combatPanel.transform, Color.green, new Vector2(-400, 200));
        Slider pMpSlider = CreateSlider("PlayerMpBar", combatPanel.transform, Color.blue, new Vector2(-400, 150));

        Slider mSlider = CreateSlider("MonsterHpBar", combatPanel.transform, Color.red, new Vector2(400, 200));
        Slider mCastSlider = CreateSlider("MonsterCastBar", combatPanel.transform, Color.yellow, new Vector2(400, 150));

        GameObject mNameObj = new GameObject("MonsterName");
        mNameObj.transform.SetParent(combatPanel.transform, false);
        Text mText = mNameObj.AddComponent<Text>();
        mText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mText.fontSize = 32;
        mText.color = Color.white;
        mText.alignment = TextAnchor.MiddleCenter;
        mNameObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(400, 250);
        mNameObj.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 50);

        GameObject mWarnObj = new GameObject("MonsterWarning");
        mWarnObj.transform.SetParent(combatPanel.transform, false);
        Text mWarnText = mWarnObj.AddComponent<Text>();
        mWarnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mWarnText.fontSize = 28;
        mWarnText.color = Color.red;
        mWarnText.text = "캐스팅 중!";
        mWarnText.fontStyle = FontStyle.Bold;
        mWarnText.alignment = TextAnchor.MiddleCenter;
        mWarnObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(400, 100);
        mWarnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 50);

        SerializedObject combatSo = new SerializedObject(combatUI);
        combatSo.FindProperty("_popupPanel").objectReferenceValue = combatPanel;
        combatSo.FindProperty("_playerStats").objectReferenceValue = playerObj.GetComponent<PlayerStats>();
        combatSo.FindProperty("_playerHpBar").objectReferenceValue = pSlider;
        combatSo.FindProperty("_playerMpBar").objectReferenceValue = pMpSlider;
        combatSo.FindProperty("_monsterHpBar").objectReferenceValue = mSlider;
        combatSo.FindProperty("_monsterCastBar").objectReferenceValue = mCastSlider;
        combatSo.FindProperty("_monsterNameText").objectReferenceValue = mText;
        combatSo.FindProperty("_monsterCastWarningText").objectReferenceValue = mWarnText;
        combatSo.ApplyModifiedProperties();
        
        combatPanel.SetActive(false); // 씬 셋업 직후 에디터 상에서 거슬리지 않도록 비활성화

        // 8. 스킬 버튼 및 도주 버튼 (전투 패널 하단)
        Button CreateSkillButton(string name, string text, Vector2 pos, Sprite sprite, Color fallbackColor)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(combatPanel.transform, false);
            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.sprite = sprite != null ? sprite : defaultSprite;
            btnImg.color = sprite != null ? Color.white : fallbackColor;
            Button btn = btnObj.AddComponent<Button>();
            btnObj.GetComponent<RectTransform>().anchoredPosition = pos; 
            btnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 120);

            GameObject tObj = new GameObject("Text");
            tObj.transform.SetParent(btnObj.transform, false);
            Text t = tObj.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 24;
            t.color = Color.white;
            t.text = text;
            t.alignment = TextAnchor.UpperCenter;
            RectTransform tRect = t.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0, 0); tRect.anchorMax = new Vector2(1, 0); // 바닥에 고정
            tRect.anchoredPosition = new Vector2(0, -30); 
            tRect.sizeDelta = new Vector2(0, 30);
            return btn;
        }

        Button stunBtn = CreateSkillButton("StunButton", "STUN", new Vector2(-200, -300), stunSprite, Color.magenta);
        Button healBtn = CreateSkillButton("HealButton", "HEAL", new Vector2(0, -300), healSprite, Color.cyan);
        Button fleeBtn = CreateSkillButton("FleeButton", "FLEE", new Vector2(200, -300), fleeSprite, Color.yellow);

        // 8.5 DualUIPanel 셋업
        GameObject dualUIPanelObj = new GameObject("DualUIPanel");
        dualUIPanelObj.transform.SetParent(canvasObj.transform, false);
        Image dualBg = dualUIPanelObj.AddComponent<Image>();
        dualBg.color = new Color(0, 0, 0, 0.7f); // 반투명 배경
        RectTransform dualRect = dualUIPanelObj.GetComponent<RectTransform>();
        dualRect.anchorMin = Vector2.zero; dualRect.anchorMax = Vector2.one;
        dualRect.offsetMin = dualRect.offsetMax = Vector2.zero;

        // 좌측 통합 패널 (좌측 전체 레이아웃 그룹)
        GameObject leftPanelObj = new GameObject("LeftPanel");
        leftPanelObj.transform.SetParent(dualUIPanelObj.transform, false);
        RectTransform lpRect = leftPanelObj.AddComponent<RectTransform>();
        lpRect.anchorMin = new Vector2(0f, 0.5f); lpRect.anchorMax = new Vector2(0f, 0.5f);
        lpRect.pivot = new Vector2(0f, 0.5f);
        lpRect.anchoredPosition = new Vector2(50, 0);
        lpRect.sizeDelta = new Vector2(800, 1000); // 전체 폭 대폭 확대

        // 1. 좌측 가장자리 세로 탭 버튼들
        string[] tabNames = { "INVENTORY", "SKILLS" };
        Button[] tabButtons = new Button[2];
        for (int i = 0; i < 2; i++)
        {
            GameObject tabBtnObj = new GameObject(tabNames[i] + "Tab");
            tabBtnObj.transform.SetParent(leftPanelObj.transform, false);
            tabBtnObj.AddComponent<Image>().color = (i == 1) ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.2f, 0.2f, 0.2f);
            Button btn = tabBtnObj.AddComponent<Button>();
            tabButtons[i] = btn;
            RectTransform tabRect = tabBtnObj.GetComponent<RectTransform>();
            tabRect.anchorMin = new Vector2(0, 1); tabRect.anchorMax = new Vector2(0, 1);
            tabRect.pivot = new Vector2(0, 1);
            tabRect.anchoredPosition = new Vector2(0, -i * 120);
            tabRect.sizeDelta = new Vector2(150, 100);

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(tabBtnObj.transform, false);
            Text txt = txtObj.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 24;
            txt.color = Color.white;
            txt.text = tabNames[i];
            txt.alignment = TextAnchor.MiddleCenter;
            txt.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            txt.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 100);
        }

        // 2-1. 장비 UI (초기 비활성화)
        GameObject gearPanelObj = new GameObject("GearPanelUI");
        gearPanelObj.transform.SetParent(leftPanelObj.transform, false);
        Image gBg = gearPanelObj.AddComponent<Image>();
        gBg.color = new Color(0.15f, 0.2f, 0.25f, 0.9f); // 약간 푸른빛
        RectTransform gRect = gearPanelObj.GetComponent<RectTransform>();
        gRect.anchorMin = new Vector2(0, 1); gRect.anchorMax = new Vector2(0, 1);
        gRect.pivot = new Vector2(0, 1);
        gRect.anchoredPosition = new Vector2(170, 0); 
        gRect.sizeDelta = new Vector2(300, 700);
        gearPanelObj.SetActive(false); // 가방이 기본이므로 끔

        // 슬롯 생성용 로컬 함수
        GameObject CreateGearSlot(string name, DimensionLooter.Core.Define.ItemType type, Vector2 pos)
        {
            GameObject slotObj = new GameObject(name);
            slotObj.transform.SetParent(gearPanelObj.transform, false);
            Image slotBg = slotObj.AddComponent<Image>();
            slotBg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            RectTransform sRect = slotObj.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.5f, 1f); sRect.anchorMax = new Vector2(0.5f, 1f);
            sRect.pivot = new Vector2(0.5f, 1f);
            sRect.anchoredPosition = pos;
            sRect.sizeDelta = new Vector2(100, 100);

            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(slotObj.transform, false);
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.gameObject.SetActive(false);
            RectTransform iRect = iconObj.GetComponent<RectTransform>();
            iRect.anchorMin = Vector2.zero; iRect.anchorMax = Vector2.one;
            iRect.offsetMin = iRect.offsetMax = Vector2.zero;

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(slotObj.transform, false);
            Text txt = txtObj.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 20;
            txt.color = Color.gray;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            txt.GetComponent<RectTransform>().anchorMax = Vector2.one;
            txt.GetComponent<RectTransform>().offsetMin = txt.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            GearSlotUI slotUI = slotObj.AddComponent<GearSlotUI>();
            SerializedObject so = new SerializedObject(slotUI);
            so.FindProperty("slotType").enumValueIndex = (int)type;
            so.FindProperty("_iconImage").objectReferenceValue = iconImg;
            so.FindProperty("_placeholderText").objectReferenceValue = txt;
            so.ApplyModifiedProperties();
            
            slotUI.Init(type);
            return slotObj;
        }

        CreateGearSlot("HelmetSlot", DimensionLooter.Core.Define.ItemType.Helmet, new Vector2(0, -50));
        CreateGearSlot("ArmorSlot", DimensionLooter.Core.Define.ItemType.Armor, new Vector2(0, -170));
        CreateGearSlot("WeaponSlot", DimensionLooter.Core.Define.ItemType.Weapon, new Vector2(-110, -170));
        CreateGearSlot("AccessorySlot", DimensionLooter.Core.Define.ItemType.Accessory, new Vector2(110, -170));
        CreateGearSlot("BagSlot", DimensionLooter.Core.Define.ItemType.Backpack, new Vector2(0, -290));

        // 2-2. 스킬 UI (초기 비활성화)
        GameObject skillsPanelObj = new GameObject("SkillsPanelUI");
        skillsPanelObj.transform.SetParent(leftPanelObj.transform, false);
        Image skBg = skillsPanelObj.AddComponent<Image>();
        skBg.color = new Color(0.25f, 0.15f, 0.2f, 0.9f); // 약간 보라빛
        RectTransform skRect = skillsPanelObj.GetComponent<RectTransform>();
        skRect.anchorMin = new Vector2(0, 1); skRect.anchorMax = new Vector2(0, 1);
        skRect.pivot = new Vector2(0, 1);
        skRect.anchoredPosition = new Vector2(170, 0); 
        skRect.sizeDelta = new Vector2(820, 700);
        skillsPanelObj.SetActive(false);

        GameObject skTxtObj = new GameObject("PlaceholderText");
        skTxtObj.transform.SetParent(skillsPanelObj.transform, false);
        Text skTxt = skTxtObj.AddComponent<Text>();
        skTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        skTxt.fontSize = 32;
        skTxt.color = Color.white;
        skTxt.text = "스킬창 준비중 (스킬 트리/정보가 들어갈 예정)";
        skTxt.alignment = TextAnchor.MiddleCenter;
        skTxt.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        skTxt.GetComponent<RectTransform>().anchorMax = Vector2.one;
        skTxt.GetComponent<RectTransform>().offsetMin = skTxt.GetComponent<RectTransform>().offsetMax = Vector2.zero;

        // 2. 메인 인벤토리 UI (가방 탭의 내용물 - 기본 활성화)
        GameObject mainInvObj = new GameObject("MainInventoryUI");
        mainInvObj.transform.SetParent(leftPanelObj.transform, false);
        Image mBg = mainInvObj.AddComponent<Image>();
        mBg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        RectTransform mRect = mainInvObj.GetComponent<RectTransform>();
        mRect.anchorMin = new Vector2(0, 1); mRect.anchorMax = new Vector2(0, 1);
        mRect.pivot = new Vector2(0, 1);
        mRect.anchoredPosition = new Vector2(490, 0); // 170(탭) + 300(장비창) + 20(여백)
        mRect.sizeDelta = new Vector2(500, 700); // 메인 인벤토리 크기
        InventoryGridUI mainGridUI = mainInvObj.AddComponent<InventoryGridUI>();
        
        GameObject mItems = new GameObject("Items");
        mItems.transform.SetParent(mainInvObj.transform, false);
        RectTransform mItemsRect = mItems.AddComponent<RectTransform>();
        mItemsRect.anchorMin = new Vector2(0, 1); mItemsRect.anchorMax = new Vector2(0, 1);
        mItemsRect.pivot = new Vector2(0, 1);
        mItemsRect.anchoredPosition = Vector2.zero;
        
        SerializedObject mainGridSo = new SerializedObject(mainGridUI);
        mainGridSo.FindProperty("_itemsContainer").objectReferenceValue = mItems;
        mainGridSo.ApplyModifiedProperties();

        // 3. 보안 파우치 UI (메인 인벤토리 하단 좌측)
        GameObject secureInvObj = new GameObject("SecureInventoryUI");
        secureInvObj.transform.SetParent(leftPanelObj.transform, false);
        Image sBg = secureInvObj.AddComponent<Image>();
        sBg.color = new Color(0.8f, 0.6f, 0.2f, 0.9f); 
        RectTransform sRect = secureInvObj.GetComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0, 1); sRect.anchorMax = new Vector2(0, 1); 
        sRect.pivot = new Vector2(0, 1);
        sRect.anchoredPosition = new Vector2(170, -720); // 메인인벤(700) 바로 아래 여백(20)
        sRect.sizeDelta = new Vector2(200, 200); // 200x200
        InventoryGridUI secureGridUI = secureInvObj.AddComponent<InventoryGridUI>();
        
        GameObject sItems = new GameObject("Items");
        sItems.transform.SetParent(secureInvObj.transform, false);
        RectTransform sItemsRect = sItems.AddComponent<RectTransform>();
        sItemsRect.anchorMin = new Vector2(0, 1); sItemsRect.anchorMax = new Vector2(0, 1);
        sItemsRect.pivot = new Vector2(0, 1);
        sItemsRect.anchoredPosition = Vector2.zero;
        
        SerializedObject secureGridSo = new SerializedObject(secureGridUI);
        secureGridSo.FindProperty("_itemsContainer").objectReferenceValue = sItems;
        secureGridSo.ApplyModifiedProperties();

        // 4. 휴지통(버리기) 영역 UI
        GameObject trashCanObj = new GameObject("TrashCanUI");
        trashCanObj.transform.SetParent(leftPanelObj.transform, false);
        Image tBg = trashCanObj.AddComponent<Image>();
        tBg.color = new Color(0.5f, 0.1f, 0.1f, 0.9f); // 어두운 붉은색
        RectTransform tRect = trashCanObj.GetComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0, 1); tRect.anchorMax = new Vector2(0, 1);
        tRect.pivot = new Vector2(0, 1);
        tRect.anchoredPosition = new Vector2(0, -770); // 사용자 설정값 반영
        tRect.sizeDelta = new Vector2(150, 150); // 사용자 설정값 반영
        
        GameObject trashTextObj = new GameObject("Text");
        trashTextObj.transform.SetParent(trashCanObj.transform, false);
        Text trashText = trashTextObj.AddComponent<Text>();
        trashText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        trashText.fontSize = 24;
        trashText.color = Color.white;
        trashText.text = "TRASH";
        trashText.alignment = TextAnchor.MiddleCenter;
        
        trashCanObj.AddComponent<TrashCanUI>(); // 마커 스크립트 부착

        // 5. 보상 인벤토리 UI (우측 끝에 고정)
        GameObject rewardInvObj = new GameObject("RewardInventoryUI");
        rewardInvObj.transform.SetParent(dualUIPanelObj.transform, false);
        Image rBg = rewardInvObj.AddComponent<Image>();
        rBg.color = new Color(0.3f, 0.2f, 0.2f, 0.9f); // 약간 붉은 색
        RectTransform rRect = rewardInvObj.GetComponent<RectTransform>();
        rRect.anchorMin = new Vector2(1f, 0.5f); rRect.anchorMax = new Vector2(1f, 0.5f); // 우측 고정
        rRect.pivot = new Vector2(1f, 0.5f);
        rRect.anchoredPosition = new Vector2(-50, 0); // 우측 끝 여백
        rRect.sizeDelta = new Vector2(600, 800); // 보상/창고도 메인과 맞먹게 대형화
        InventoryGridUI rewardGridUI = rewardInvObj.AddComponent<InventoryGridUI>();

        GameObject rItems = new GameObject("Items");
        rItems.transform.SetParent(rewardInvObj.transform, false);
        RectTransform rItemsRect = rItems.AddComponent<RectTransform>();
        rItemsRect.anchorMin = new Vector2(0, 1); rItemsRect.anchorMax = new Vector2(0, 1);
        rItemsRect.pivot = new Vector2(0, 1);
        rItemsRect.anchoredPosition = Vector2.zero;

        SerializedObject rewardGridSo = new SerializedObject(rewardGridUI);
        rewardGridSo.FindProperty("_itemsContainer").objectReferenceValue = rItems;
        rewardGridSo.FindProperty("_drawGridLines").boolValue = false;
        rewardGridSo.ApplyModifiedProperties();

        // 6. 닫기 버튼 (가운데 상단)
        GameObject closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(dualUIPanelObj.transform, false);
        closeBtnObj.AddComponent<Image>().color = Color.gray;
        Button closeBtn = closeBtnObj.AddComponent<Button>();
        RectTransform cRect = closeBtnObj.GetComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0.5f, 1f); cRect.anchorMax = new Vector2(0.5f, 1f); 
        cRect.pivot = new Vector2(0.5f, 1f);
        cRect.anchoredPosition = new Vector2(0, -40);
        cRect.sizeDelta = new Vector2(200, 60);
        GameObject cTextObj = new GameObject("Text");
        cTextObj.transform.SetParent(closeBtnObj.transform, false);
        Text cText = cTextObj.AddComponent<Text>();
        cText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        cText.fontSize = 24;
        cText.color = Color.white;
        cText.text = "CLOSE";
        cText.alignment = TextAnchor.MiddleCenter;

        // 아이템 프리팹 (드래그 가능한 빈 객체)
        GameObject itemPrefab = new GameObject("ItemPrefab");
        itemPrefab.AddComponent<RectTransform>();
        itemPrefab.AddComponent<Image>().color = Color.white;
        GameObject iTextObj = new GameObject("NameText");
        iTextObj.transform.SetParent(itemPrefab.transform, false);
        Text iText = iTextObj.AddComponent<Text>();
        iText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        iText.color = Color.black;
        iText.alignment = TextAnchor.MiddleCenter;
        RectTransform iTextRect = iText.GetComponent<RectTransform>();
        iTextRect.anchorMin = Vector2.zero; iTextRect.anchorMax = Vector2.one;
        iTextRect.offsetMin = iTextRect.offsetMax = Vector2.zero;
        
        DraggableItemUI dragUI = itemPrefab.AddComponent<DraggableItemUI>();
        SerializedObject dragSo = new SerializedObject(dragUI);
        dragSo.FindProperty("_backgroundImage").objectReferenceValue = itemPrefab.GetComponent<Image>();
        dragSo.FindProperty("_nameText").objectReferenceValue = iText;
        dragSo.ApplyModifiedProperties();

        GameObject savedItemPrefab = PrefabUtility.SaveAsPrefabAsset(itemPrefab, "Assets/Resources/Prefabs/DraggableItem_Dummy.prefab");
        DestroyImmediate(itemPrefab);

        DualUIPanel dualUI = canvasObj.AddComponent<DualUIPanel>(); // Rule 12: 항상 활성화된 Canvas에 붙여서 Awake가 보장되도록 함
        SerializedObject dualSo = new SerializedObject(dualUI);
        dualSo.FindProperty("_panelRoot").objectReferenceValue = dualUIPanelObj;
        dualSo.FindProperty("_gearPanel").objectReferenceValue = gearPanelObj;
        dualSo.FindProperty("_bagPanel").objectReferenceValue = mainInvObj;
        dualSo.FindProperty("_skillsPanel").objectReferenceValue = skillsPanelObj;
        dualSo.FindProperty("_mainInventoryUI").objectReferenceValue = mainGridUI;
        dualSo.FindProperty("_secureInventoryUI").objectReferenceValue = secureGridUI;
        dualSo.FindProperty("_rewardInventoryUI").objectReferenceValue = rewardGridUI;
        dualSo.FindProperty("_itemPrefab").objectReferenceValue = savedItemPrefab;
        dualSo.ApplyModifiedProperties();

        UnityEditor.Events.UnityEventTools.AddPersistentListener(tabButtons[0].onClick, dualUI.SwitchToInventory);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(tabButtons[1].onClick, dualUI.SwitchToSkills);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(closeBtn.onClick, dualUI.ClosePanel);
        
        dualUIPanelObj.SetActive(false);

        // 8.8 Exploration UI 셋업 (Top-Left HUD & Top-Right Bag)
        GameObject expUIObj = new GameObject("ExplorationUI");
        expUIObj.transform.SetParent(canvasObj.transform, false);
        RectTransform expRect = expUIObj.AddComponent<RectTransform>();
        expRect.anchorMin = Vector2.zero; expRect.anchorMax = Vector2.one;
        expRect.offsetMin = expRect.offsetMax = Vector2.zero;

        Slider eHpSlider = CreateSlider("ExpHpBar", expUIObj.transform, Color.green, new Vector2(90, -30));
        eHpSlider.GetComponent<RectTransform>().anchorMin = new Vector2(0, 1);
        eHpSlider.GetComponent<RectTransform>().anchorMax = new Vector2(0, 1);
        eHpSlider.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 15);

        Slider eMpSlider = CreateSlider("ExpMpBar", expUIObj.transform, Color.blue, new Vector2(90, -50));
        eMpSlider.GetComponent<RectTransform>().anchorMin = new Vector2(0, 1);
        eMpSlider.GetComponent<RectTransform>().anchorMax = new Vector2(0, 1);
        eMpSlider.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 15);

        GameObject bagBtnObj = new GameObject("BagButton");
        bagBtnObj.transform.SetParent(expUIObj.transform, false);
        bagBtnObj.AddComponent<Image>().color = new Color(0.6f, 0.4f, 0.2f);
        Button bagBtn = bagBtnObj.AddComponent<Button>();
        RectTransform bRect = bagBtnObj.GetComponent<RectTransform>();
        bRect.anchorMin = new Vector2(1, 1); bRect.anchorMax = new Vector2(1, 1); // Top-Right
        bRect.anchoredPosition = new Vector2(-60, -40);
        bRect.sizeDelta = new Vector2(60, 60);

        GameObject bTextObj = new GameObject("Text");
        bTextObj.transform.SetParent(bagBtnObj.transform, false);
        Text bText = bTextObj.AddComponent<Text>();
        bText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bText.color = Color.white;
        bText.text = "BAG";
        bText.alignment = TextAnchor.MiddleCenter;
        
        ExplorationUI expUI = expUIObj.AddComponent<ExplorationUI>();
        SerializedObject expSo = new SerializedObject(expUI);
        expSo.FindProperty("_playerStats").objectReferenceValue = playerObj.GetComponent<PlayerStats>();
        expSo.FindProperty("_hpBar").objectReferenceValue = eHpSlider;
        expSo.FindProperty("_mpBar").objectReferenceValue = eMpSlider;
        expSo.FindProperty("_bagButton").objectReferenceValue = bagBtn;
        expSo.ApplyModifiedProperties();

        // 9. Managers 셋업
        GameObject managersObj = GameObject.Find("@Managers") ?? new GameObject("@Managers");
        GameManager gameMgr = managersObj.GetComponent<GameManager>() ?? managersObj.AddComponent<GameManager>();
        CombatManager combatMgr = managersObj.GetComponent<CombatManager>() ?? managersObj.AddComponent<CombatManager>();
        InventoryManager invMgr = managersObj.GetComponent<InventoryManager>() ?? managersObj.AddComponent<InventoryManager>();
        GearManager gearMgr = managersObj.GetComponent<GearManager>() ?? managersObj.AddComponent<GearManager>();
        
        UnityEditor.Events.UnityEventTools.AddPersistentListener(fleeBtn.onClick, combatMgr.Flee);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(stunBtn.onClick, combatUI.OnWeaponSkillClicked);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(healBtn.onClick, combatUI.OnArmorSkillClicked);

        SerializedObject mgrSo = new SerializedObject(combatMgr);
        mgrSo.FindProperty("_playerStats").objectReferenceValue = playerObj.GetComponent<PlayerStats>();
        mgrSo.FindProperty("_combatUI").objectReferenceValue = combatUI;
        mgrSo.ApplyModifiedProperties();

        SerializedObject uiSo = new SerializedObject(combatUI);
        uiSo.FindProperty("_weaponSkillBtn").objectReferenceValue = stunBtn;
        uiSo.FindProperty("_weaponSkillText").objectReferenceValue = stunBtn.GetComponentInChildren<Text>();
        uiSo.FindProperty("_armorSkillBtn").objectReferenceValue = healBtn;
        uiSo.FindProperty("_armorSkillText").objectReferenceValue = healBtn.GetComponentInChildren<Text>();
        uiSo.ApplyModifiedProperties();

        // 10. 레거시(더미 몬스터 단일 배치) 삭제됨

        // 11. Lobby UI 셋업
        GameObject lobbyGO = new GameObject("LobbyUI", typeof(RectTransform));
        lobbyGO.transform.SetParent(canvasObj.transform, false);
        LobbyUI lobbyUI = lobbyGO.AddComponent<LobbyUI>();
        RectTransform lobbyRT = lobbyGO.GetComponent<RectTransform>();
        lobbyRT.anchorMin = Vector2.zero; lobbyRT.anchorMax = Vector2.one;
        lobbyRT.offsetMin = lobbyRT.offsetMax = Vector2.zero;
        
        // 11-1. 내가방 보기 버튼
        GameObject invBtnGO = new GameObject("Btn_Inventory", typeof(RectTransform));
        invBtnGO.transform.SetParent(lobbyGO.transform, false);
        Image invImg = invBtnGO.AddComponent<Image>();
        invImg.color = new Color(0.2f, 0.4f, 0.8f);
        Button invBtn = invBtnGO.AddComponent<Button>();
        RectTransform invRT = invBtnGO.GetComponent<RectTransform>();
        invRT.anchorMin = new Vector2(0.5f, 0.5f); invRT.anchorMax = new Vector2(0.5f, 0.5f);
        invRT.anchoredPosition = new Vector2(-150, 0);
        invRT.sizeDelta = new Vector2(200, 80);
        
        GameObject invTxtGO = new GameObject("Text");
        invTxtGO.transform.SetParent(invBtnGO.transform, false);
        Text invTxt = invTxtGO.AddComponent<Text>();
        invTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        invTxt.fontSize = 24;
        invTxt.color = Color.white;
        invTxt.text = "내가방 보기";
        invTxt.alignment = TextAnchor.MiddleCenter;
        RectTransform invTxtRT = invTxt.GetComponent<RectTransform>();
        invTxtRT.anchorMin = Vector2.zero; invTxtRT.anchorMax = Vector2.one;
        invTxtRT.offsetMin = invTxtRT.offsetMax = Vector2.zero;
        
        // 11-2. 던전 입장 버튼
        GameObject dunBtnGO = new GameObject("Btn_EnterDungeon", typeof(RectTransform));
        dunBtnGO.transform.SetParent(lobbyGO.transform, false);
        Image dunImg = dunBtnGO.AddComponent<Image>();
        dunImg.color = new Color(0.8f, 0.2f, 0.2f);
        Button dunBtn = dunBtnGO.AddComponent<Button>();
        RectTransform dunRT = dunBtnGO.GetComponent<RectTransform>();
        dunRT.anchorMin = new Vector2(0.5f, 0.5f); dunRT.anchorMax = new Vector2(0.5f, 0.5f);
        dunRT.anchoredPosition = new Vector2(150, 0);
        dunRT.sizeDelta = new Vector2(200, 80);
        
        GameObject dunTxtGO = new GameObject("Text");
        dunTxtGO.transform.SetParent(dunBtnGO.transform, false);
        Text dunTxt = dunTxtGO.AddComponent<Text>();
        dunTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        dunTxt.fontSize = 24;
        dunTxt.color = Color.white;
        dunTxt.text = "던전 입장";
        dunTxt.alignment = TextAnchor.MiddleCenter;
        RectTransform dunTxtRT = dunTxt.GetComponent<RectTransform>();
        dunTxtRT.anchorMin = Vector2.zero; dunTxtRT.anchorMax = Vector2.one;
        dunTxtRT.offsetMin = dunTxtRT.offsetMax = Vector2.zero;

        SerializedObject lobbySO = new SerializedObject(lobbyUI);
        lobbySO.FindProperty("_btnInventory").objectReferenceValue = invBtn;
        lobbySO.FindProperty("_btnEnterDungeon").objectReferenceValue = dunBtn;
        lobbySO.ApplyModifiedProperties();
        
        // LobbyManager 바인딩
        DungeonManager dunMgr = managersObj.GetComponent<DungeonManager>() ?? managersObj.AddComponent<DungeonManager>();
        LobbyManager lobMgr = managersObj.GetComponent<LobbyManager>() ?? managersObj.AddComponent<LobbyManager>();
        SerializedObject lmSO = new SerializedObject(lobMgr);
        lmSO.FindProperty("_lobbyUI").objectReferenceValue = lobbyUI;
        lmSO.ApplyModifiedProperties();

        // 변경사항이 Play 모드 진입 시 날아가지 않도록 씬 더티 마킹
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[MVPSceneSetup] Tilemap 기반 A* 그리드 이동 셋업 완료!");
    }
}
