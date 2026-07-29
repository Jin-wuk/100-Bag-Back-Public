// File: Assets/Editor/AddLobbySetup.cs
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using DimensionLooter.Managers;
using DimensionLooter.UI.Lobby;
using DimensionLooter.Gameplay;

public class AddLobbySetup : EditorWindow
{
    [MenuItem("Tools/Add Lobby UI and Portal")]
    public static void SetupLobby()
    {
        // 1. GameManager에 LobbyManager 부착
        GameObject managersObj = GameObject.Find("@Managers");
        if (managersObj == null)
        {
            Debug.LogError("@Managers 오브젝트를 찾을 수 없습니다.");
            return;
        }

        LobbyManager lobbyManager = managersObj.GetComponent<LobbyManager>();
        if (lobbyManager == null)
        {
            lobbyManager = managersObj.AddComponent<LobbyManager>();
        }

        // 2. Canvas에 LobbyUI 생성
        GameObject canvasObj = GameObject.Find("Canvas");
        if (canvasObj == null)
        {
            Debug.LogError("Canvas 오브젝트를 찾을 수 없습니다.");
            return;
        }

        GameObject lobbyUIObj = GameObject.Find("LobbyUI");
        if (lobbyUIObj != null)
        {
            DestroyImmediate(lobbyUIObj);
        }

        lobbyUIObj = new GameObject("LobbyUI");
        lobbyUIObj.transform.SetParent(canvasObj.transform, false);
        RectTransform rt = lobbyUIObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        LobbyUI lobbyUI = lobbyUIObj.AddComponent<LobbyUI>();

        // 인벤토리 버튼
        GameObject invBtnObj = new GameObject("Btn_Inventory");
        invBtnObj.transform.SetParent(lobbyUIObj.transform, false);
        Image invImg = invBtnObj.AddComponent<Image>();
        invImg.color = new Color(0.2f, 0.6f, 0.8f);
        Button invBtn = invBtnObj.AddComponent<Button>();
        RectTransform invRt = invBtnObj.GetComponent<RectTransform>();
        invRt.anchorMin = new Vector2(0.5f, 0.5f); invRt.anchorMax = new Vector2(0.5f, 0.5f);
        invRt.anchoredPosition = new Vector2(-150, 0);
        invRt.sizeDelta = new Vector2(200, 80);

        GameObject invTxtObj = new GameObject("Text");
        invTxtObj.transform.SetParent(invBtnObj.transform, false);
        Text invTxt = invTxtObj.AddComponent<Text>();
        invTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        invTxt.text = "가방 및 정비 (Inventory)";
        invTxt.color = Color.white;
        invTxt.alignment = TextAnchor.MiddleCenter;
        invTxt.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 80);

        // 던전 입장 버튼
        GameObject enterBtnObj = new GameObject("Btn_EnterDungeon");
        enterBtnObj.transform.SetParent(lobbyUIObj.transform, false);
        Image enterImg = enterBtnObj.AddComponent<Image>();
        enterImg.color = new Color(0.8f, 0.3f, 0.2f);
        Button enterBtn = enterBtnObj.AddComponent<Button>();
        RectTransform enterRt = enterBtnObj.GetComponent<RectTransform>();
        enterRt.anchorMin = new Vector2(0.5f, 0.5f); enterRt.anchorMax = new Vector2(0.5f, 0.5f);
        enterRt.anchoredPosition = new Vector2(150, 0);
        enterRt.sizeDelta = new Vector2(200, 80);

        GameObject enterTxtObj = new GameObject("Text");
        enterTxtObj.transform.SetParent(enterBtnObj.transform, false);
        Text enterTxt = enterTxtObj.AddComponent<Text>();
        enterTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        enterTxt.text = "던전 탐험 (Enter Dungeon)";
        enterTxt.color = Color.white;
        enterTxt.alignment = TextAnchor.MiddleCenter;
        enterTxt.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 80);

        // LobbyUI 연결
        SerializedObject so = new SerializedObject(lobbyUI);
        so.FindProperty("_btnInventory").objectReferenceValue = invBtn;
        so.FindProperty("_btnEnterDungeon").objectReferenceValue = enterBtn;
        so.ApplyModifiedProperties();

        // LobbyManager에 LobbyUI 연결
        SerializedObject mngrSo = new SerializedObject(lobbyManager);
        mngrSo.FindProperty("_lobbyUI").objectReferenceValue = lobbyUI;
        mngrSo.ApplyModifiedProperties();

        // 3. 던전 구역에 ReturnPortal 생성
        GameObject portalObj = GameObject.Find("ReturnPortal");
        if (portalObj != null) DestroyImmediate(portalObj);

        portalObj = new GameObject("ReturnPortal");
        portalObj.transform.position = new Vector3(105, 100, 0); // 던전 근처
        
        SpriteRenderer sr = portalObj.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        sr.color = Color.cyan;
        portalObj.transform.localScale = new Vector3(2, 2, 1);

        BoxCollider2D bc = portalObj.AddComponent<BoxCollider2D>();
        bc.isTrigger = true;

        portalObj.AddComponent<ReturnPortal>();

        Debug.Log("[Setup] 로비 UI와 탈출 포탈이 성공적으로 씬에 배치되었습니다!");
    }
}
