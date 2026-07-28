// File: Assets/Editor/MVPSceneSetup.cs
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DimensionLooter.Managers;
using DimensionLooter.Gameplay.Combat;
using DimensionLooter.UI;
using System.IO;

public class MVPSceneSetup : EditorWindow
{
    [MenuItem("Tools/Setup MVP Scene")]
    public static void SetupScene()
    {
        // 1. Prefab 폴더 생성
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        // 2. 몬스터 더미 프리팹 생성 (큐브 기반)
        GameObject monsterObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        monsterObj.name = "Monster_Dummy";
        monsterObj.AddComponent<MonsterHealth>();
        GameObject monsterPrefab = PrefabUtility.SaveAsPrefabAsset(monsterObj, "Assets/Prefabs/Monster_Dummy.prefab");
        DestroyImmediate(monsterObj);

        // 3. 플레이어 오브젝트 생성
        GameObject playerObj = new GameObject("Player");
        PlayerHealth playerHealth = playerObj.AddComponent<PlayerHealth>();

        // 4. UI 캔버스 및 이벤트 시스템 생성
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();

        // 5. 인벤토리 UI 셋업
        GameObject invPanel = new GameObject("InventoryPanel");
        invPanel.transform.SetParent(canvasObj.transform, false);
        InventoryUI invUI = invPanel.AddComponent<InventoryUI>();

        // 인벤토리용 더미 슬롯 & 아이템 프리팹 생성
        GameObject slotObj = new GameObject("SlotPrefab");
        slotObj.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        GameObject slotPrefab = PrefabUtility.SaveAsPrefabAsset(slotObj, "Assets/Prefabs/SlotPrefab.prefab");
        DestroyImmediate(slotObj);

        GameObject itemObj = new GameObject("ItemPrefab");
        itemObj.AddComponent<Image>().color = Color.green;
        GameObject itemPrefab = PrefabUtility.SaveAsPrefabAsset(itemObj, "Assets/Prefabs/ItemPrefab.prefab");
        DestroyImmediate(itemObj);

        GameObject gridParent = new GameObject("GridParent");
        gridParent.transform.SetParent(invPanel.transform, false);
        gridParent.AddComponent<GridLayoutGroup>();

        // InventoryUI 인스펙터 레퍼런스 연결
        SerializedObject invSo = new SerializedObject(invUI);
        invSo.FindProperty("_slotPrefab").objectReferenceValue = slotPrefab;
        invSo.FindProperty("_itemIconPrefab").objectReferenceValue = itemPrefab;
        invSo.FindProperty("_gridParent").objectReferenceValue = gridParent.transform;
        invSo.ApplyModifiedProperties();

        // 6. 전투 UI 셋업
        GameObject combatPanel = new GameObject("CombatPanel");
        combatPanel.transform.SetParent(canvasObj.transform, false);
        CombatUI combatUI = combatPanel.AddComponent<CombatUI>();

        GameObject sliderObj = new GameObject("PlayerHpBar");
        sliderObj.transform.SetParent(combatPanel.transform, false);
        Slider hpSlider = sliderObj.AddComponent<Slider>();
        
        // CombatUI 인스펙터 레퍼런스 연결
        SerializedObject combatSo = new SerializedObject(combatUI);
        combatSo.FindProperty("_playerHealth").objectReferenceValue = playerHealth;
        combatSo.FindProperty("_playerHpBar").objectReferenceValue = hpSlider;
        combatSo.ApplyModifiedProperties();

        // 7. 매니저 오브젝트 연결 (CombatManager 스폰 위치/프리팹)
        GameObject managersObj = GameObject.Find("@Managers");
        if (managersObj != null)
        {
            CombatManager combatMgr = managersObj.GetComponent<CombatManager>();
            if (combatMgr != null)
            {
                GameObject spawnPoint = new GameObject("SpawnPoint");
                spawnPoint.transform.position = new Vector3(0, 0, 5); // 카메라 앞쪽
                
                SerializedObject mgrSo = new SerializedObject(combatMgr);
                mgrSo.FindProperty("_monsterSpawnPoint").objectReferenceValue = spawnPoint.transform;
                mgrSo.FindProperty("_monsterPrefab").objectReferenceValue = monsterPrefab;
                mgrSo.ApplyModifiedProperties();
            }
        }

        Debug.Log("[MVPSceneSetup] MVP 씬 셋업이 100% 자동 완료되었습니다!");
    }
}
