using System.IO;
using UnityEditor;
using UnityEngine;

namespace BagBack100.EditorTools
{
    /// <summary>
    /// Role: UI 키트(Assets/Art/Sprites/UI/Kit), 월드맵(Assets/Art/Sprites/UI/WorldMap), 허브 오브젝트·배경(Assets/Art/Sprites/Hub) 스프라이트의 임포트 설정을 일괄 적용하는 에디터 메뉴.
    ///       원본을 다시 복사해 덮어써도 메뉴 한 번으로 같은 결과가 나오게 한다 (12 설계안 3절).
    /// Dependencies: 없음
    /// Events: 없음
    /// </summary>
    public static class UIKitImportSettings
    {
        private const string UIKitFolder = "Assets/Art/Sprites/UI/Kit";
        private const string HubFolder = "Assets/Art/Sprites/Hub";
        private const string WorldMapFolder = "Assets/Art/Sprites/UI/WorldMap";
        // 허브 통짜 배경은 오브젝트의 2배 크기 아트라 PPU를 절반으로 둔다 (배경 1px = 오브젝트 2px)
        private const string HubBackgroundName = "hub_bg_camp";
        private const float HubBackgroundPixelsPerUnit = 16f;
        // 안개는 불투명한 곳만 마우스에 반응하도록(Image.alphaHitTestMinimumThreshold) 텍스처를 읽을 수 있어야 한다
        private const string WorldMapFogName = "ui_map_layer_fog";
        // UI 키트는 Image.pixelsPerUnitMultiplier 0.5와 짝을 이뤄 화면에 2배로 보인다
        private const float UIKitPixelsPerUnit = 100f;
        // 허브 오브젝트는 타일과 같은 PPU라야 크기가 맞는다
        private const float HubPixelsPerUnit = 32f;

        [MenuItem("Tools/UI Kit/Apply Import Settings")]
        public static void ApplyAll()
        {
            int count = 0;
            count += ApplyFolder(UIKitFolder, UIKitPixelsPerUnit, SpriteAlignment.Center);
            count += ApplyFolder(WorldMapFolder, UIKitPixelsPerUnit, SpriteAlignment.Center);
            count += ApplyFolder(HubFolder, HubPixelsPerUnit, SpriteAlignment.BottomCenter);
            AssetDatabase.Refresh();
            Debug.Log($"[UIKitImportSettings] {count}개 스프라이트 임포트 설정 적용");
        }

        private static int ApplyFolder(string folder, float pixelsPerUnit, SpriteAlignment alignment)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning($"[UIKitImportSettings] 폴더 없음: {folder}");
                return 0;
            }

            int count = 0;
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                string fileName = Path.GetFileNameWithoutExtension(path);
                bool isHubBackground = fileName == HubBackgroundName;
                importer.spritePixelsPerUnit = isHubBackground ? HubBackgroundPixelsPerUnit : pixelsPerUnit;
                importer.isReadable = fileName == WorldMapFogName;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)(isHubBackground ? SpriteAlignment.Center : alignment);
                // 9-slice는 Full Rect라야 Sliced 표시가 테두리를 정확히 지킨다
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);

                importer.spriteBorder = GetBorder(fileName);
                importer.SaveAndReimport();
                count++;
            }
            return count;
        }

        // Unity spriteBorder 순서: x = 왼쪽, y = 아래, z = 오른쪽, w = 위. 값은 12 설계안 3절(원본 스펙의 9-slice 마진)
        private static Vector4 GetBorder(string fileName)
        {
            if (fileName.StartsWith("ui_panel_")) return Uniform(6);
            if (fileName.StartsWith("ui_tooltip_")) return Uniform(4);
            if (fileName.StartsWith("ui_btn_")) return Uniform(4);
            if (fileName.StartsWith("ui_key_badge")) return Uniform(3);
            if (fileName.StartsWith("ui_scroll_")) return Uniform(2);
            if (fileName.StartsWith("ui_header_bar")) return new Vector4(4, 2, 2, 2);
            if (fileName.StartsWith("ui_slot_thin_")) return Uniform(1);
            if (fileName.StartsWith("ui_slot_thick_")) return Uniform(2);
            return Vector4.zero;
        }

        private static Vector4 Uniform(float v) => new Vector4(v, v, v, v);
    }
}
