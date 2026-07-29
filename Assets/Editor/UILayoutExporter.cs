using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class UILayoutExporter : EditorWindow
{
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

    [MenuItem("Tools/1. 현재 UI 레이아웃 추출하기 (Export Layout)")]
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
}
