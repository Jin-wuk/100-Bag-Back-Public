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
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");

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
        monsterObj.AddComponent<BoxCollider2D>().size = new Vector2(0.9f, 0.9f); // 타일보다 약간 작게
        monsterObj.AddComponent<MonsterHealth>();
        
        // 몬스터는 Obstacle 레이어로 설정 (임시로 Water 레이어(4) 사용)
        monsterObj.layer = 4;
        GameObject monsterPrefab = PrefabUtility.SaveAsPrefabAsset(monsterObj, "Assets/Prefabs/Monster_Dummy.prefab");
        DestroyImmediate(monsterObj);

        // 4. 플레이어 오브젝트
        GameObject playerObj = new GameObject("Player");
        SpriteRenderer pSr = playerObj.AddComponent<SpriteRenderer>();
        pSr.sprite = defaultSprite;
        pSr.color = Color.blue; 
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
        tooltipObj.AddComponent<Image>().color = new Color(0, 0, 0, 0.8f);
        
        GameObject tooltipTextObj = new GameObject("Text");
        tooltipTextObj.transform.SetParent(tooltipObj.transform, false);
        Text tText = tooltipTextObj.AddComponent<Text>();
        tText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        tText.color = Color.white;
        tText.alignment = TextAnchor.MiddleCenter;
        
        TooltipUI tooltipUI = tooltipObj.AddComponent<TooltipUI>();
        SerializedObject tSo = new SerializedObject(tooltipUI);
        tSo.FindProperty("_tooltipPanel").objectReferenceValue = tooltipObj;
        tSo.FindProperty("_infoText").objectReferenceValue = tText;
        tSo.ApplyModifiedProperties();

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
        mText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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
        ft.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        ft.color = Color.black;
        ft.text = "FLEE";
        ft.alignment = TextAnchor.MiddleCenter;

        // 9. Managers 셋업
        GameObject managersObj = GameObject.Find("@Managers") ?? new GameObject("@Managers");
        CombatManager combatMgr = managersObj.GetComponent<CombatManager>() ?? managersObj.AddComponent<CombatManager>();
        
        UnityEditor.Events.UnityEventTools.AddPersistentListener(fleeBtn.onClick, combatMgr.Flee);

        SerializedObject mgrSo = new SerializedObject(combatMgr);
        mgrSo.FindProperty("_playerHealth").objectReferenceValue = playerObj.GetComponent<PlayerHealth>();
        mgrSo.FindProperty("_combatUI").objectReferenceValue = combatUI;
        mgrSo.ApplyModifiedProperties();

        // 10. 몬스터 배치 (x=3, y=3 정수 좌표에 배치)
        GameObject testMonster = (GameObject)PrefabUtility.InstantiatePrefab(monsterPrefab);
        testMonster.transform.position = new Vector3(3, 3, 0);
        testMonster.GetComponent<MonsterHealth>().Init(80);

        Debug.Log("[MVPSceneSetup] Tilemap 기반 A* 그리드 이동 셋업 완료!");
    }
}
