using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using DimensionLooter.Managers;
using System.IO;

public class SetupDungeonTiles
{
    [MenuItem("Tools/Setup Dungeon Tiles")]
    public static void SetupTiles()
    {
        // 1. 타일 에셋 저장할 폴더 생성
        string folderPath = "Assets/Data/Tiles";
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder(folderPath))
            AssetDatabase.CreateFolder("Assets/Data", "Tiles");

        // 2. 타일 에셋 생성
        Tile floorTile = AssetDatabase.LoadAssetAtPath<Tile>(folderPath + "/FloorTile.asset");
        if (floorTile == null)
        {
            floorTile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(floorTile, folderPath + "/FloorTile.asset");
        }

        Tile wallTile = AssetDatabase.LoadAssetAtPath<Tile>(folderPath + "/WallTile.asset");
        if (wallTile == null)
        {
            wallTile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(wallTile, folderPath + "/WallTile.asset");
        }
        AssetDatabase.SaveAssets();

        // 3. 씬에서 DungeonManager 찾기
        DungeonManager dm = Object.FindObjectOfType<DungeonManager>();
        if (dm == null)
        {
            Debug.LogError("[SetupDungeonTiles] 씬에 DungeonManager가 없습니다.");
            return;
        }

        // 4. Grid와 Tilemap 생성
        Grid grid = Object.FindObjectOfType<Grid>();
        if (grid == null)
        {
            GameObject gridObj = new GameObject("Grid");
            grid = gridObj.AddComponent<Grid>();
        }

        Tilemap floorTilemap = CreateTilemap(grid.gameObject, "FloorTilemap");
        Tilemap wallTilemap = CreateTilemap(grid.gameObject, "WallTilemap");

        // 5. DungeonManager 인스펙터 자동 할당
        SerializedObject so = new SerializedObject(dm);
        so.FindProperty("_floorTilemap").objectReferenceValue = floorTilemap;
        so.FindProperty("_wallTilemap").objectReferenceValue = wallTilemap;
        so.FindProperty("_floorTile").objectReferenceValue = floorTile;
        so.FindProperty("_wallTile").objectReferenceValue = wallTile;
        so.ApplyModifiedProperties();

        // 6. 씬 변경사항 저장
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[SetupDungeonTiles] 타일맵과 타일 에셋이 임시로 생성되고 DungeonManager에 성공적으로 할당되었습니다!");
    }

    private static Tilemap CreateTilemap(GameObject parent, string name)
    {
        Transform child = parent.transform.Find(name);
        if (child != null)
        {
            return child.GetComponent<Tilemap>();
        }

        GameObject tmObj = new GameObject(name);
        tmObj.transform.SetParent(parent.transform);
        tmObj.transform.localPosition = Vector3.zero;
        
        Tilemap tm = tmObj.AddComponent<Tilemap>();
        tmObj.AddComponent<TilemapRenderer>();

        return tm;
    }
}
