using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using TMPro;
using BagBack100.Dungeon;
using BagBack100.Dungeon.Data;

namespace BagBack100.EditorTools
{
    /// <summary>
    /// Role: 층계 던전 개발용 에디터 메뉴. 회색 박스 층 3장(일반 2 + 보스 1)과 지역 1 SO를 만들고,
    ///       지역 설정을 검증하고, 03_DungeonScene을 층 경로(영속 Grid + RegionConfigSO)로 전환한다.
    /// Dependencies: FloorLayout, FloorMarker, DungeonZone, FloorDesignSO, RegionConfigSO, FloorDesignValidator, DungeonGenerator
    /// Events: 없음
    /// </summary>
    public static class FloorGrayBoxBuilder
    {
        private const string TemplateMapPath = "Assets/Resources/Prefabs/MapPrefabs/FixedMap.prefab";
        private const string FloorPrefabFolder = "Assets/Resources/Prefabs/MapPrefabs/Floors";
        private const string FloorSOFolder = "Assets/Resources/Data_SO/FloorSO/Region1";
        private const string RegionConfigPath = FloorSOFolder + "/Region1Config.asset";
        private const string DungeonSceneName = "03_DungeonScene";
        private const string FloorGridName = "FloorGrid";

        // '#' 벽, '.' 바닥, ' ' 빈 공간, 'S' 시작, 'U' 위층 계단, 'E' 탈출구, 'C' 상자 후보 (마커 칸은 바닥)
        private static readonly string[] Normal01Map =
        {
            "######################",
            "#S.....#.............#",
            "#......#.............#",
            "#......#....C........#",
            "#......#.............#",
            "#..........#####.....#",
            "#..........#.........#",
            "#####..#####.........#",
            "#.........#......#####",
            "#.........#..........#",
            "#...C.....#..........#",
            "#.........######..U..#",
            "#....................#",
            "#..E.................#",
            "#....................#",
            "######################",
        };

        private static readonly string[] Normal02Map =
        {
            "########################",
            "#........#.............#",
            "#..S.....#......C......#",
            "#........#.............#",
            "#......................#",
            "#........#.............#",
            "#####.####.......U.....#",
            "#........#######.#######",
            "#........#.............#",
            "#...C....#.............#",
            "#..............E.......#",
            "#........#.............#",
            "########################",
        };

        private static readonly string[] Boss01Map =
        {
            "########################",
            "#..U................E..#",
            "#......................#",
            "#......................#",
            "#.....##........##.....#",
            "#.....##........##.....#",
            "#......................#",
            "#......................#",
            "#......................#",
            "#......................#",
            "#......................#",
            "#.....##........##.....#",
            "#.....##........##.....#",
            "#......................#",
            "#......................#",
            "#......................#",
            "#......................#",
            "#..........S...........#",
            "#......................#",
            "########################",
        };

        private struct ZoneSpec
        {
            public RectInt Cells;   // 좌하단 기준 칸 사각형 (y 위쪽 증가)
            public int MonsterCount;
            public ZoneSpec(int x, int y, int w, int h, int monsterCount)
            {
                Cells = new RectInt(x, y, w, h);
                MonsterCount = monsterCount;
            }
        }

        private struct FloorSpec
        {
            public string Name;
            public string[] Map;
            public ZoneSpec[] Zones;
            public FloorBand Band;
        }

        private static FloorSpec[] GetSpecs()
        {
            return new[]
            {
                new FloorSpec
                {
                    Name = "Floor_GrayBox_Normal01", Map = Normal01Map, Band = FloorBand.Early,
                    Zones = new[] { new ZoneSpec(9, 11, 11, 4, 2), new ZoneSpec(11, 5, 10, 3, 2) }
                },
                new FloorSpec
                {
                    Name = "Floor_GrayBox_Normal02", Map = Normal02Map, Band = FloorBand.Early,
                    Zones = new[] { new ZoneSpec(10, 7, 13, 5, 2), new ZoneSpec(10, 1, 13, 4, 2) }
                },
                new FloorSpec
                {
                    // 보스 대체용 구역 (T7에서 보스 배치로 교체)
                    Name = "Floor_GrayBox_Boss01", Map = Boss01Map, Band = FloorBand.Boss,
                    Zones = new[] { new ZoneSpec(8, 9, 8, 5, 1) }
                },
            };
        }

        [MenuItem("BagBack100/Floor/Create Gray Box Floors")]
        public static void CreateGrayBoxFloors()
        {
            EnsureFolder(FloorPrefabFolder);
            EnsureFolder(FloorSOFolder);

            GameObject template = PrefabUtility.LoadPrefabContents(TemplateMapPath);
            var savedLayouts = new Dictionary<string, FloorLayout>();
            try
            {
                Tilemap templateFloor = FindTilemap(template, "Floor");
                Tilemap templateWall = FindTilemap(template, "Wall");
                if (templateFloor == null || templateWall == null)
                {
                    Debug.LogError($"[FloorGrayBoxBuilder] {TemplateMapPath}에서 Floor/Wall 타일맵을 찾지 못했습니다.");
                    return;
                }

                TileBase floorTile = FindMostUsedTile(templateFloor);
                TileBase wallTile = FindMostUsedTile(templateWall);

                foreach (FloorSpec spec in GetSpecs())
                {
                    FloorLayout saved = BuildAndSaveLayout(spec, templateFloor, templateWall, floorTile, wallTile);
                    if (saved == null) return;
                    savedLayouts[spec.Name] = saved;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(template);
            }

            FloorDesignSO normal01 = CreateOrUpdateDesign("FloorDesign_GrayBox_Normal01", savedLayouts["Floor_GrayBox_Normal01"], FloorBand.Early);
            FloorDesignSO normal02 = CreateOrUpdateDesign("FloorDesign_GrayBox_Normal02", savedLayouts["Floor_GrayBox_Normal02"], FloorBand.Early);
            FloorDesignSO boss01 = CreateOrUpdateDesign("FloorDesign_GrayBox_Boss01", savedLayouts["Floor_GrayBox_Boss01"], FloorBand.Boss);

            // 회색 박스는 2장뿐이라 6~9 풀에도 같은 디자인을 넣는다. 구간 검사를 통과하도록 Late용 SO를 따로 둔다.
            FloorDesignSO late01 = CreateOrUpdateDesign("FloorDesign_GrayBox_Normal01_Late", savedLayouts["Floor_GrayBox_Normal01"], FloorBand.Late);
            FloorDesignSO late02 = CreateOrUpdateDesign("FloorDesign_GrayBox_Normal02_Late", savedLayouts["Floor_GrayBox_Normal02"], FloorBand.Late);

            RegionConfigSO region = AssetDatabase.LoadAssetAtPath<RegionConfigSO>(RegionConfigPath);
            if (region == null)
            {
                region = ScriptableObject.CreateInstance<RegionConfigSO>();
                AssetDatabase.CreateAsset(region, RegionConfigPath);
            }
            region.EditorInitialize(
                1, "지역 1",
                new List<FloorDesignSO> { normal01, normal02 },
                new List<FloorDesignSO> { late01, late02 },
                boss01, boss01,
                CreateDefaultRules(),
                true);
            EditorUtility.SetDirty(region);
            AssetDatabase.SaveAssets();

            Debug.Log($"[FloorGrayBoxBuilder] 회색 박스 층 3장과 {RegionConfigPath} 생성 완료.");
            ValidateRegionConfigs();
        }

        [MenuItem("BagBack100/Floor/Validate Region Configs")]
        public static void ValidateRegionConfigs()
        {
            string[] guids = AssetDatabase.FindAssets("t:RegionConfigSO");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[FloorGrayBoxBuilder] RegionConfigSO 에셋이 없습니다.");
                return;
            }

            var errors = new List<string>();
            foreach (string guid in guids)
            {
                var region = AssetDatabase.LoadAssetAtPath<RegionConfigSO>(AssetDatabase.GUIDToAssetPath(guid));
                errors.Clear();
                if (FloorDesignValidator.ValidateRegion(region, errors))
                {
                    Debug.Log($"[FloorValidator] {region.name}: 오류 0건");
                }
                else
                {
                    Debug.LogError($"[FloorValidator] {region.name}: 오류 {errors.Count}건\n{string.Join("\n", errors)}", region);
                }
            }
        }

        [MenuItem("BagBack100/Floor/Setup Dungeon Scene For Floors")]
        public static void SetupDungeonScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != DungeonSceneName)
            {
                Debug.LogError($"[FloorGrayBoxBuilder] {DungeonSceneName}을 연 상태에서 실행하세요. (현재: {scene.name})");
                return;
            }

            var region = AssetDatabase.LoadAssetAtPath<RegionConfigSO>(RegionConfigPath);
            if (region == null)
            {
                Debug.LogError($"[FloorGrayBoxBuilder] {RegionConfigPath}가 없습니다. Create Gray Box Floors를 먼저 실행하세요.");
                return;
            }

            DungeonGenerator generator = Object.FindObjectOfType<DungeonGenerator>();
            if (generator == null)
            {
                Debug.LogError("[FloorGrayBoxBuilder] 씬에 DungeonGenerator가 없습니다.");
                return;
            }

            Undo.SetCurrentGroupName("Setup Dungeon Scene For Floors");
            int undoGroup = Undo.GetCurrentGroup();

            GameObject fixedMapRoot = FindFixedMapInstance(scene);
            bool removedFixedMap = fixedMapRoot != null;
            if (removedFixedMap)
            {
                ClearFogTilemapIfInside(fixedMapRoot.transform);
                Undo.DestroyObjectImmediate(fixedMapRoot);
            }

            if (GameObject.Find(FloorGridName) == null)
            {
                var gridGo = new GameObject(FloorGridName, typeof(Grid));
                gridGo.GetComponent<Grid>().cellSize = new Vector3(1f, 1f, 0f);
                SceneManager.MoveGameObjectToScene(gridGo, scene);
                Undo.RegisterCreatedObjectUndo(gridGo, "Create FloorGrid");
            }

            var so = new SerializedObject(generator);
            so.FindProperty("_regionConfig").objectReferenceValue = region;
            so.FindProperty("_floorTilemap").objectReferenceValue = null;
            so.FindProperty("_wallTilemap").objectReferenceValue = null;
            so.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[FloorGrayBoxBuilder] {DungeonSceneName}: FixedMap 제거({removedFixedMap}), {FloorGridName} 준비, Region1Config 연결 후 저장.");
        }

        // T2: 회색 박스 위층 계단 프리팹과 층 HUD를 만들고 03_DungeonScene에 연결한다
        [MenuItem("BagBack100/Floor/Setup Stairs And Floor HUD")]
        public static void SetupStairsAndFloorHud()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != DungeonSceneName)
            {
                Debug.LogError($"[FloorGrayBoxBuilder] {DungeonSceneName}을 연 상태에서 실행하세요. (현재: {scene.name})");
                return;
            }

            DungeonGenerator generator = Object.FindObjectOfType<DungeonGenerator>();
            Canvas canvas = FindRootCanvas();
            if (generator == null || canvas == null)
            {
                Debug.LogError("[FloorGrayBoxBuilder] 씬에 DungeonGenerator 또는 Canvas가 없습니다.");
                return;
            }

            FloorStairs stairsPrefab = CreateOrUpdateStairsPrefab();

            Undo.SetCurrentGroupName("Setup Stairs And Floor HUD");
            int undoGroup = Undo.GetCurrentGroup();

            var so = new SerializedObject(generator);
            so.FindProperty("_stairsPrefab").objectReferenceValue = stairsPrefab;
            so.ApplyModifiedProperties();

            BagBack100.UI.FloorHUD existingHud = Object.FindObjectOfType<BagBack100.UI.FloorHUD>(true);
            if (existingHud != null && existingHud.transform.parent != canvas.transform)
            {
                Undo.DestroyObjectImmediate(existingHud.gameObject); // 잘못된 캔버스 아래 만들어진 HUD 정리
                existingHud = null;
            }
            if (existingHud == null)
            {
                CreateFloorHud(canvas.transform);
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[FloorGrayBoxBuilder] {DungeonSceneName}: 계단 프리팹 연결, FloorHUD 준비 후 저장.");
        }

        private const string StairsPrefabPath = FloorPrefabFolder + "/FloorStairs_GrayBox.prefab";
        private const int StairsSortingOrder = 11; // 탈출 포탈(ReturnPortal)과 같은 정렬 순서

        private static FloorStairs CreateOrUpdateStairsPrefab()
        {
            EnsureFolder(FloorPrefabFolder);
            var go = new GameObject("FloorStairs_GrayBox");
            try
            {
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                renderer.color = new Color(0.65f, 0.4f, 1f, 0.9f);
                renderer.sortingOrder = StairsSortingOrder;
                go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = Vector2.one;
                go.AddComponent<FloorStairs>();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, StairsPrefabPath);
                return prefab != null ? prefab.GetComponent<FloorStairs>() : null;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static Canvas FindRootCanvas()
        {
            // 꺼진 하위 캔버스(Toast 등)도 isRootCanvas가 true이므로 켜진 Overlay 캔버스만 고른다
            foreach (Canvas c in Object.FindObjectsOfType<Canvas>())
            {
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) return c;
            }
            return null;
        }

        private static void CreateFloorHud(Transform canvas)
        {
            var root = new GameObject("FloorHUD", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Create FloorHUD");
            var rootRect = (RectTransform)root.transform;
            rootRect.SetParent(canvas, false);
            Stretch(rootRect);
            rootRect.SetAsLastSibling(); // 암전 이미지가 다른 HUD 위를 덮도록 맨 뒤에 둔다

            // 상시 층 표시: 우측 상단
            var label = CreateText(rootRect, "FloorLabel", 32f, TextAlignmentOptions.TopRight);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(1f, 1f);
            labelRect.anchoredPosition = new Vector2(-24f, -20f);
            labelRect.sizeDelta = new Vector2(360f, 50f);

            // 진입 배너: 상단 중앙
            var bannerGo = new GameObject("Banner", typeof(RectTransform), typeof(CanvasGroup));
            var bannerRect = (RectTransform)bannerGo.transform;
            bannerRect.SetParent(rootRect, false);
            bannerRect.anchorMin = bannerRect.anchorMax = bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = new Vector2(0f, -120f);
            bannerRect.sizeDelta = new Vector2(800f, 90f);
            var bannerGroup = bannerGo.GetComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerGroup.blocksRaycasts = false;
            bannerGroup.interactable = false;
            var bannerText = CreateText(bannerRect, "BannerText", 56f, TextAlignmentOptions.Center);
            Stretch(bannerText.rectTransform);

            // 층 전환 암전: 전체 화면 (마우스 입력을 막지 않는다)
            var fadeGo = new GameObject("Fade", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var fadeRect = (RectTransform)fadeGo.transform;
            fadeRect.SetParent(rootRect, false);
            Stretch(fadeRect);
            var fadeImage = fadeGo.GetComponent<UnityEngine.UI.Image>();
            fadeImage.color = new Color(0f, 0f, 0f, 0f);
            fadeImage.raycastTarget = false;

            var hud = root.AddComponent<BagBack100.UI.FloorHUD>();
            var so = new SerializedObject(hud);
            so.FindProperty("_floorLabel").objectReferenceValue = label;
            so.FindProperty("_bannerGroup").objectReferenceValue = bannerGroup;
            so.FindProperty("_bannerText").objectReferenceValue = bannerText;
            so.FindProperty("_fadeImage").objectReferenceValue = fadeImage;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TMPro.TextMeshProUGUI CreateText(Transform parent, string name, float fontSize, TMPro.TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TMPro.TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static FloorLayout BuildAndSaveLayout(FloorSpec spec, Tilemap templateFloor, Tilemap templateWall,
            TileBase floorTile, TileBase wallTile)
        {
            int height = spec.Map.Length;
            int width = spec.Map[0].Length;
            for (int r = 0; r < height; r++)
            {
                if (spec.Map[r].Length != width)
                {
                    Debug.LogError($"[FloorGrayBoxBuilder] {spec.Name} {r}행 길이 {spec.Map[r].Length} ≠ {width}");
                    return null;
                }
            }

            // Tilemap은 부모 Grid가 있어야 하므로 임시 Grid 아래에서 조립하고 레이아웃 루트만 프리팹으로 저장한다
            var tempGrid = new GameObject("TempGrid_GrayBox", typeof(Grid));
            try
            {
                var root = new GameObject(spec.Name);
                root.transform.SetParent(tempGrid.transform, false);
                FloorLayout layout = root.AddComponent<FloorLayout>();

                Tilemap floor = CloneTilemap(templateFloor, root.transform, "Floor");
                Tilemap wall = CloneTilemap(templateWall, root.transform, "Wall");
                layout.EditorSetTilemaps(floor, wall);

                var markers = new GameObject("Markers").transform;
                markers.SetParent(root.transform, false);
                int chestIndex = 0;

                for (int r = 0; r < height; r++)
                {
                    int y = height - 1 - r;
                    for (int x = 0; x < width; x++)
                    {
                        char c = spec.Map[r][x];
                        var cell = new Vector3Int(x, y, 0);
                        if (c == ' ') continue;
                        if (c == '#')
                        {
                            wall.SetTile(cell, wallTile);
                            continue;
                        }

                        floor.SetTile(cell, floorTile);
                        switch (c)
                        {
                            case 'S': CreateMarker(markers, "PlayerStart", FloorMarkerType.PlayerStart, x, y); break;
                            case 'U': CreateMarker(markers, "UpStairs", FloorMarkerType.UpStairs, x, y); break;
                            case 'E': CreateMarker(markers, "Exit", FloorMarkerType.Exit, x, y); break;
                            case 'C': CreateMarker(markers, $"Chest_{chestIndex++}", FloorMarkerType.ChestCandidate, x, y); break;
                        }
                    }
                }
                floor.CompressBounds();
                wall.CompressBounds();

                var zones = new GameObject("Zones").transform;
                zones.SetParent(root.transform, false);
                for (int i = 0; i < spec.Zones.Length; i++)
                {
                    CreateCombatZone(zones, $"Zone_Combat_{i}", spec.Zones[i]);
                }

                string path = $"{FloorPrefabFolder}/{spec.Name}.prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success || prefab == null)
                {
                    Debug.LogError($"[FloorGrayBoxBuilder] {path} 저장 실패");
                    return null;
                }
                return prefab.GetComponent<FloorLayout>();
            }
            finally
            {
                Object.DestroyImmediate(tempGrid);
            }
        }

        private static Tilemap CloneTilemap(Tilemap template, Transform parent, string name)
        {
            // 렌더러 정렬·레이어·콜라이더 설정을 기존 맵과 같게 맞추려고 템플릿 오브젝트를 복제한다
            GameObject go = Object.Instantiate(template.gameObject, parent, false);
            go.name = name;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            Tilemap tilemap = go.GetComponent<Tilemap>();
            tilemap.ClearAllTiles();
            return tilemap;
        }

        private static void CreateMarker(Transform parent, string name, FloorMarkerType type, int x, int y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x + 0.5f, y + 0.5f, 0f);
            go.AddComponent<FloorMarker>().EditorSetType(type);
        }

        private static void CreateCombatZone(Transform parent, string name, ZoneSpec spec)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(spec.Cells.x + spec.Cells.width * 0.5f, spec.Cells.y + spec.Cells.height * 0.5f, 0f);
            DungeonZone zone = go.AddComponent<DungeonZone>();

            var so = new SerializedObject(zone);
            so.FindProperty("_zoneType").enumValueIndex = (int)DungeonZoneType.Combat;
            so.FindProperty("_size").vector2Value = new Vector2(spec.Cells.width, spec.Cells.height);
            so.FindProperty("_monsterCount").intValue = spec.MonsterCount;
            so.FindProperty("_trapCount").intValue = 0;
            so.FindProperty("_chestCount").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static FloorDesignSO CreateOrUpdateDesign(string assetName, FloorLayout layout, FloorBand band)
        {
            string path = $"{FloorSOFolder}/{assetName}.asset";
            var design = AssetDatabase.LoadAssetAtPath<FloorDesignSO>(path);
            if (design == null)
            {
                design = ScriptableObject.CreateInstance<FloorDesignSO>();
                AssetDatabase.CreateAsset(design, path);
            }
            design.EditorInitialize(layout, band, 1);
            EditorUtility.SetDirty(design);
            return design;
        }

        // 기획 v7 초기값: 탈출구 층 x 10% (상한 50%), 상자 1~4층 1회·6~9층 2회·확률 40%. 5층은 보스층 규칙이라 0.
        private static FloorRule[] CreateDefaultRules()
        {
            var rules = new FloorRule[RegionConfigSO.RuleFloorCount];
            for (int floor = 1; floor <= RegionConfigSO.RuleFloorCount; floor++)
            {
                if (floor == RegionConfigSO.MidBossFloor)
                {
                    rules[floor - 1] = new FloorRule(0f, 0, 0f);
                    continue;
                }
                float exitChance = Mathf.Min(floor * 0.1f, 0.5f);
                int chestRolls = floor < RegionConfigSO.MidBossFloor ? 1 : 2;
                rules[floor - 1] = new FloorRule(exitChance, chestRolls, 0.4f);
            }
            return rules;
        }

        private static GameObject FindFixedMapInstance(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(root)) continue;
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == TemplateMapPath) return root;
            }
            return null;
        }

        private static void ClearFogTilemapIfInside(Transform fixedMapRoot)
        {
            FogOfWarSystem fog = Object.FindObjectOfType<FogOfWarSystem>();
            if (fog == null) return;
            var so = new SerializedObject(fog);
            SerializedProperty prop = so.FindProperty("_fogTilemap");
            if (prop == null) return;
            var current = prop.objectReferenceValue as Component;
            if (current != null && current.transform.IsChildOf(fixedMapRoot))
            {
                prop.objectReferenceValue = null;
                so.ApplyModifiedProperties();
            }
        }

        private static Tilemap FindTilemap(GameObject root, string childName)
        {
            foreach (Tilemap tm in root.GetComponentsInChildren<Tilemap>(true))
            {
                if (tm.gameObject.name == childName) return tm;
            }
            return null;
        }

        private static TileBase FindMostUsedTile(Tilemap tilemap)
        {
            var counts = new Dictionary<TileBase, int>();
            TileBase best = null;
            int bestCount = 0;
            foreach (Vector3Int pos in tilemap.cellBounds.allPositionsWithin)
            {
                TileBase tile = tilemap.GetTile(pos);
                if (tile == null) continue;
                counts.TryGetValue(tile, out int n);
                counts[tile] = ++n;
                if (n > bestCount)
                {
                    bestCount = n;
                    best = tile;
                }
            }
            return best;
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            Directory.CreateDirectory(assetFolder);
            AssetDatabase.Refresh();
        }
    }
}
