// File: Assets/Editor/MVPSceneSetup.cs
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using DimensionLooter.Managers;
using DimensionLooter.Gameplay.Combat;
using DimensionLooter.Gameplay;
using DimensionLooter.UI;
using DimensionLooter.UI.Inventory;

public class MVPSceneSetup : EditorWindow
{
    [MenuItem("Tools/Setup MVP Scene 2.5")]
    public static void SetupScene()
    {
        // 내장 기본 스프라이트 가져오기 (UISprite)
        Sprite defaultSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // 0. 카메라 2D 직교 모드 설정
        if (Camera.main != null)
        {
            Camera.main.orthographic = true;
            Camera.main.orthographicSize = 5f;
            Camera.main.transform.position = new Vector3(0, 0, -10);
        }

        // 1. Prefab 폴더 생성
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs")) AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");

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
        SpriteRenderer pSr = playerObj.AddComponent<SpriteRenderer>();
        pSr.sprite = defaultSprite;
        pSr.color = Color.blue; 
        pSr.sortingOrder = 10; // 바닥보다 위로 렌더링
        playerObj.AddComponent<PlayerHealth>();
        PlayerController pc = playerObj.AddComponent<PlayerController>();
        
        // PlayerController 인스펙터 세팅 (ObstacleMask = Water 레이어)
        SerializedObject pcSo = new SerializedObject(pc);
        pcSo.FindProperty("_obstacleMask").intValue = 1 << 4; 
        pcSo.ApplyModifiedProperties();

        // 5. UI 캔버스 및 이벤트 시스템
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
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
        combatBg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        RectTransform cpRect = combatPanel.GetComponent<RectTransform>();
        cpRect.anchorMin = Vector2.zero; cpRect.anchorMax = Vector2.one;
        cpRect.offsetMin = cpRect.offsetMax = Vector2.zero;

        CombatUI combatUI = combatPanel.AddComponent<CombatUI>();

        Slider CreateSlider(string name, Transform parent, Color color, Vector2 anchoredPos)
        {
            GameObject sliderObj = new GameObject(name);
            sliderObj.transform.SetParent(parent, false);
            Slider slider = sliderObj.AddComponent<Slider>();
            RectTransform rect = sliderObj.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(200, 20);

            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillRect = fillArea.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = color;
            RectTransform fRect = fill.GetComponent<RectTransform>();
            fRect.anchorMin = Vector2.zero; fRect.anchorMax = Vector2.one;
            
            slider.fillRect = fRect;
            return slider;
        }

        Slider pSlider = CreateSlider("PlayerHpBar", combatPanel.transform, Color.green, new Vector2(-150, 0));
        Slider mSlider = CreateSlider("MonsterHpBar", combatPanel.transform, Color.red, new Vector2(150, 0));

        GameObject mNameObj = new GameObject("MonsterName");
        mNameObj.transform.SetParent(combatPanel.transform, false);
        Text mText = mNameObj.AddComponent<Text>();
        mText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mText.color = Color.white;
        mNameObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(150, 30);

        SerializedObject combatSo = new SerializedObject(combatUI);
        combatSo.FindProperty("_popupPanel").objectReferenceValue = combatPanel;
        combatSo.FindProperty("_playerHealth").objectReferenceValue = playerObj.GetComponent<PlayerHealth>();
        combatSo.FindProperty("_playerHpBar").objectReferenceValue = pSlider;
        combatSo.FindProperty("_monsterHpBar").objectReferenceValue = mSlider;
        combatSo.FindProperty("_monsterNameText").objectReferenceValue = mText;
        combatSo.ApplyModifiedProperties();

        // 8. 하단 HUD 및 도주 버튼
        GameObject hudObj = new GameObject("BottomHUD");
        hudObj.transform.SetParent(canvasObj.transform, false);
        RectTransform hudRect = hudObj.AddComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(0, 0); hudRect.anchorMax = new Vector2(1, 0.3f);
        hudRect.offsetMin = hudRect.offsetMax = Vector2.zero;

        GameObject fleeBtnObj = new GameObject("FleeButton");
        fleeBtnObj.transform.SetParent(hudObj.transform, false);
        fleeBtnObj.AddComponent<Image>().color = Color.yellow;
        Button fleeBtn = fleeBtnObj.AddComponent<Button>();
        fleeBtnObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 50);
        fleeBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 40);

        GameObject fleeText = new GameObject("Text");
        fleeText.transform.SetParent(fleeBtnObj.transform, false);
        Text ft = fleeText.AddComponent<Text>();
        ft.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ft.color = Color.black;
        ft.text = "FLEE";
        ft.alignment = TextAnchor.MiddleCenter;

        // 8.5 DualUIPanel 셋업
        GameObject dualUIPanelObj = new GameObject("DualUIPanel");
        dualUIPanelObj.transform.SetParent(canvasObj.transform, false);
        Image dualBg = dualUIPanelObj.AddComponent<Image>();
        dualBg.color = new Color(0, 0, 0, 0.7f); // 반투명 배경
        RectTransform dualRect = dualUIPanelObj.GetComponent<RectTransform>();
        dualRect.anchorMin = Vector2.zero; dualRect.anchorMax = Vector2.one;
        dualRect.offsetMin = dualRect.offsetMax = Vector2.zero;

        // 메인 인벤토리 UI
        GameObject mainInvObj = new GameObject("MainInventoryUI");
        mainInvObj.transform.SetParent(dualUIPanelObj.transform, false);
        Image mBg = mainInvObj.AddComponent<Image>();
        mBg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        RectTransform mRect = mainInvObj.GetComponent<RectTransform>();
        mRect.anchoredPosition = new Vector2(-200, 0); // 좌측
        mRect.sizeDelta = new Vector2(300, 300); // 6x6 (50x50 cell)
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

        // 보상 인벤토리 UI
        GameObject rewardInvObj = new GameObject("RewardInventoryUI");
        rewardInvObj.transform.SetParent(dualUIPanelObj.transform, false);
        Image rBg = rewardInvObj.AddComponent<Image>();
        rBg.color = new Color(0.3f, 0.2f, 0.2f, 0.9f); // 약간 붉은 색
        RectTransform rRect = rewardInvObj.GetComponent<RectTransform>();
        rRect.anchoredPosition = new Vector2(200, 0); // 우측
        rRect.sizeDelta = new Vector2(300, 200); // 6x4 (50x50 cell)
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

        // 닫기 버튼
        GameObject closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(dualUIPanelObj.transform, false);
        closeBtnObj.AddComponent<Image>().color = Color.gray;
        Button closeBtn = closeBtnObj.AddComponent<Button>();
        RectTransform cRect = closeBtnObj.GetComponent<RectTransform>();
        cRect.anchoredPosition = new Vector2(0, -200);
        cRect.sizeDelta = new Vector2(100, 40);
        GameObject cTextObj = new GameObject("Text");
        cTextObj.transform.SetParent(closeBtnObj.transform, false);
        Text cText = cTextObj.AddComponent<Text>();
        cText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        dualSo.FindProperty("_mainInventoryUI").objectReferenceValue = mainGridUI;
        dualSo.FindProperty("_rewardInventoryUI").objectReferenceValue = rewardGridUI;
        dualSo.FindProperty("_itemPrefab").objectReferenceValue = savedItemPrefab;
        dualSo.ApplyModifiedProperties();

        UnityEditor.Events.UnityEventTools.AddPersistentListener(closeBtn.onClick, dualUI.ClosePanel);
        
        dualUIPanelObj.SetActive(false);

        // 9. Managers 셋업
        GameObject managersObj = GameObject.Find("@Managers") ?? new GameObject("@Managers");
        CombatManager combatMgr = managersObj.GetComponent<CombatManager>() ?? managersObj.AddComponent<CombatManager>();
        InventoryManager invMgr = managersObj.GetComponent<InventoryManager>() ?? managersObj.AddComponent<InventoryManager>();
        
        UnityEditor.Events.UnityEventTools.AddPersistentListener(fleeBtn.onClick, combatMgr.Flee);

        SerializedObject mgrSo = new SerializedObject(combatMgr);
        mgrSo.FindProperty("_playerHealth").objectReferenceValue = playerObj.GetComponent<PlayerHealth>();
        mgrSo.FindProperty("_combatUI").objectReferenceValue = combatUI;
        mgrSo.ApplyModifiedProperties();

        // 10. 몬스터 배치 (x=3, y=3 정수 좌표에 배치)
        GameObject testMonster = (GameObject)PrefabUtility.InstantiatePrefab(monsterPrefab);
        testMonster.transform.position = new Vector3(3, 3, 0);
        testMonster.GetComponent<MonsterHealth>().Init(80);

        // 변경사항이 Play 모드 진입 시 날아가지 않도록 씬 더티 마킹
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[MVPSceneSetup] Tilemap 기반 A* 그리드 이동 셋업 완료!");
    }
}
