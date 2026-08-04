using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using DimensionLooter.Managers;
using DimensionLooter.UI.Lobby;
using DimensionLooter.UI.Inventory;

public class SetupHubEditor
{
    [MenuItem("Tools/Setup Hub Facility")]
    public static void SetupHub()
    {
        var managers = GameObject.Find("@Managers");
        if (managers != null && managers.GetComponent<EconomyManager>() == null) {
            managers.AddComponent<EconomyManager>();
        }

        var lobbyUI = GameObject.Find("LobbyUI");
        if (lobbyUI == null) {
            Debug.LogError("LobbyUI not found.");
            return;
        }

        var canvas = lobbyUI.GetComponentInParent<Canvas>();
        if (canvas == null) {
            Debug.LogError("Canvas not found.");
            return;
        }

        var hubPanel = GameObject.Find("HubFacilityPanel");
        if (hubPanel != null) { Object.DestroyImmediate(hubPanel); }

        hubPanel.transform.SetParent(canvas.transform, false);
        var hubRt = hubPanel.GetComponent<RectTransform>();
        hubRt.anchorMin = Vector2.zero;
        hubRt.anchorMax = Vector2.one;
        hubRt.sizeDelta = Vector2.zero;
        hubPanel.GetComponent<Image>().color = new Color(0, 0, 0, 0.8f);



        // Create Views
        var stashView = new GameObject("StashView", typeof(RectTransform));
        stashView.transform.SetParent(hubPanel.transform, false);
        var shopView = new GameObject("ShopView", typeof(RectTransform));
        shopView.transform.SetParent(hubPanel.transform, false);
        var bsView = new GameObject("BlacksmithView", typeof(RectTransform));
        bsView.transform.SetParent(hubPanel.transform, false);

        // Make Views fill right side
        foreach (var view in new[] { stashView, shopView, bsView }) {
            var rt = view.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.sizeDelta = Vector2.zero;
        }

        // Create Buttons on the left side
        var btnContainer = new GameObject("TabButtons", typeof(RectTransform), typeof(VerticalLayoutGroup));
        btnContainer.transform.SetParent(hubPanel.transform, false);
        var btnRt = btnContainer.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0, 1);
        btnRt.anchorMax = new Vector2(0, 1);
        btnRt.pivot = new Vector2(0, 1);
        btnRt.anchoredPosition = new Vector2(20, -20);
        btnRt.sizeDelta = new Vector2(200, 300);

        Button CreateBtn(string name) {
            var bGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            bGo.transform.SetParent(btnContainer.transform, false);
            var txtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtGo.transform.SetParent(bGo.transform, false);
            var txt = txtGo.GetComponent<Text>();
            txt.text = name;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.color = Color.black;
            txt.alignment = TextAnchor.MiddleCenter;
            var txtRt = txt.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one; txtRt.sizeDelta = Vector2.zero;
            return bGo.GetComponent<Button>();
        }

        var btnStash = CreateBtn("Btn_Stash");
        var btnShop = CreateBtn("Btn_Shop");
        var btnBs = CreateBtn("Btn_Blacksmith");
        var btnClose = CreateBtn("Btn_Close");

        // Link HubFacilityPanel
        //var so = new SerializedObject(hubComp);
        //so.FindProperty("_stashView").objectReferenceValue = stashView;
        //so.FindProperty("_shopView").objectReferenceValue = shopView;
        //so.FindProperty("_blacksmithView").objectReferenceValue = bsView;
        //so.FindProperty("_btnStash").objectReferenceValue = btnStash;
        //so.FindProperty("_btnShop").objectReferenceValue = btnShop;
        //so.FindProperty("_btnBlacksmith").objectReferenceValue = btnBs;
        //so.FindProperty("_btnClose").objectReferenceValue = btnClose;
        //so.ApplyModifiedProperties();

        // StashUI Setup
        var stashUI = stashView.AddComponent<StashUI>();
        var stashTabs = new GameObject("StashTabs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        stashTabs.transform.SetParent(stashView.transform, false);
        var stRt = stashTabs.GetComponent<RectTransform>();
        stRt.anchorMin = new Vector2(0, 1); stRt.anchorMax = new Vector2(1, 1);
        stRt.pivot = new Vector2(0.5f, 1); stRt.anchoredPosition = new Vector2(0, -20);
        stRt.sizeDelta = new Vector2(0, 50);

        var gridUIObj = new GameObject("StashGrid", typeof(RectTransform), typeof(InventoryGridUI));
        gridUIObj.transform.SetParent(stashView.transform, false);
        var gRt = gridUIObj.GetComponent<RectTransform>();
        gRt.anchorMin = new Vector2(0, 1); gRt.anchorMax = new Vector2(0, 1);
        gRt.pivot = new Vector2(0, 1); gRt.anchoredPosition = new Vector2(20, -100);
        var invUI = gridUIObj.GetComponent<InventoryGridUI>();

        // Create ItemsContainer inside StashGrid
        var itemsCont = new GameObject("ItemsContainer", typeof(RectTransform));
        itemsCont.transform.SetParent(gridUIObj.transform, false);
        var icRt = itemsCont.GetComponent<RectTransform>();
        icRt.anchorMin = new Vector2(0,1); icRt.anchorMax = new Vector2(0,1); icRt.pivot = new Vector2(0,1);

        var soInv = new SerializedObject(invUI);
        soInv.FindProperty("_itemsContainer").objectReferenceValue = itemsCont.transform;
        soInv.FindProperty("_cellSize").floatValue = 50f;
        soInv.ApplyModifiedProperties();

        // Create Tab Button Prefab
        var tabBtnPrefab = new GameObject("TabBtnPrefab", typeof(RectTransform), typeof(Image), typeof(Button));
        var tTxt = new GameObject("Text", typeof(RectTransform), typeof(Text));
        tTxt.transform.SetParent(tabBtnPrefab.transform, false);
        var txtComp = tTxt.GetComponent<Text>();
        txtComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txtComp.color = Color.black; txtComp.alignment = TextAnchor.MiddleCenter;
        var tTxtRt = tTxt.GetComponent<RectTransform>();
        tTxtRt.anchorMin = Vector2.zero; tTxtRt.anchorMax = Vector2.one; tTxtRt.sizeDelta = Vector2.zero;

        if (!System.IO.Directory.Exists("Assets/Prefabs")) System.IO.Directory.CreateDirectory("Assets/Prefabs");
        string prefabPath = "Assets/Prefabs/TabBtnPrefab.prefab";
        var savedPrefab = PrefabUtility.SaveAsPrefabAsset(tabBtnPrefab, prefabPath);
        Object.DestroyImmediate(tabBtnPrefab);

        var soStash = new SerializedObject(stashUI);
        soStash.FindProperty("_stashGridUI").objectReferenceValue = invUI;
        soStash.FindProperty("_tabButtonContainer").objectReferenceValue = stashTabs.transform;
        soStash.FindProperty("_tabButtonPrefab").objectReferenceValue = savedPrefab.GetComponent<Button>();
        soStash.ApplyModifiedProperties();

        // Sell Zone Setup
        var sellZoneObj = new GameObject("SellZone", typeof(RectTransform), typeof(Image), typeof(SellZoneUI));
        sellZoneObj.transform.SetParent(bsView.transform, false);
        var szRt = sellZoneObj.GetComponent<RectTransform>();
        szRt.anchorMin = new Vector2(0.5f, 0.5f); szRt.anchorMax = new Vector2(0.5f, 0.5f);
        szRt.sizeDelta = new Vector2(400, 400);
        szRt.anchoredPosition = Vector2.zero;
        sellZoneObj.GetComponent<Image>().color = new Color(1, 0, 0, 0.5f);

        var szTxt = new GameObject("Text", typeof(RectTransform), typeof(Text));
        szTxt.transform.SetParent(sellZoneObj.transform, false);
        var stc = szTxt.GetComponent<Text>();
        stc.text = "Drop Items Here to Sell";
        stc.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        stc.color = Color.white; stc.alignment = TextAnchor.MiddleCenter;
        var szTxtRt = szTxt.GetComponent<RectTransform>();
        szTxtRt.anchorMin = Vector2.zero; szTxtRt.anchorMax = Vector2.one; szTxtRt.sizeDelta = Vector2.zero;

        // Hide all by default except Stash
        stashView.SetActive(false);
        shopView.SetActive(false);
        bsView.SetActive(false);
        hubPanel.SetActive(false);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log("HubFacilityPanel created successfully.");
    }
}
