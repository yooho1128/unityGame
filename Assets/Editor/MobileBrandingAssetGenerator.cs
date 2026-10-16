#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    /// <summary>스토어·홈 화면용 1024px 앱 아이콘을 생성하고 Android/iOS PlayerSettings에 연결한다.</summary>
    public static class MobileBrandingAssetGenerator
    {
        public const string IconPath = "Assets/Art/Branding/ShadowTheater_AppIcon.png";

        [MenuItem("Tools/Shadow Theater/Release/Generate Mobile Branding")]
        public static void GenerateAndApply()
        {
            EnsureFolders();
            if (!File.Exists(IconPath)) GenerateIcon();
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.mipmapEnabled = false;
                importer.sRGBTexture = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null || icon.width != 1024 || icon.height != 1024)
            {
                Debug.LogError($"[Branding] 앱 아이콘은 1024x1024 PNG여야 합니다: {IconPath}");
                return;
            }
            ApplyIcons(BuildTargetGroup.Android, icon);
            ApplyIcons(BuildTargetGroup.iOS, icon);
            PlayerSettings.SplashScreen.backgroundColor = new Color(.018f, .012f, .045f, 1f);
            AssetDatabase.SaveAssets();
            Debug.Log("[Branding] 1024px 앱 아이콘 생성 및 Android/iOS 연결 완료");
        }

        public static bool HasConfiguredIcon(BuildTargetGroup group) =>
            PlayerSettings.GetIconsForTargetGroup(group).Any(x => x != null);

        private static void ApplyIcons(BuildTargetGroup group, Texture2D icon)
        {
            if (icon == null) return;
            int count = PlayerSettings.GetIconSizesForTargetGroup(group).Length;
            if (count <= 0) return;
            PlayerSettings.SetIconsForTargetGroup(group, Enumerable.Repeat(icon, count).ToArray());
        }

        private static void GenerateIcon()
        {
            const int size = 1024;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x - size * .5f) / (size * .5f);
                float ny = (y - size * .5f) / (size * .5f);
                float radial = Mathf.Clamp01(Mathf.Sqrt(nx * nx + ny * ny));
                Color top = new Color(.105f, .045f, .19f);
                Color bottom = new Color(.015f, .012f, .055f);
                Color baseColor = Color.Lerp(bottom, top, Mathf.Clamp01(y / (float)size * .72f));
                texture.SetPixel(x, y, Color.Lerp(baseColor, Color.black, radial * radial * .52f));
            }

            // 극장 장막과 금빛 프로시니엄.
            Color curtain = new Color(.31f, .035f, .11f);
            Color curtainLight = new Color(.58f, .075f, .18f);
            Fill(texture, 0, 0, 135, size, curtain);
            Fill(texture, size - 136, 0, size - 1, size - 1, curtain);
            for (int i = 0; i < 5; i++)
            {
                Fill(texture, 22 + i * 23, 0, 34 + i * 23, size - 1, i % 2 == 0 ? curtainLight : curtain);
                Fill(texture, size - 35 - i * 23, 0, size - 23 - i * 23, size - 1,
                    i % 2 == 0 ? curtainLight : curtain);
            }
            Color gold = new Color(.94f, .64f, .22f);
            Fill(texture, 112, 76, 132, 948, gold);
            Fill(texture, 892, 76, 912, 948, gold);
            Fill(texture, 112, 928, 912, 948, gold);

            // 펼쳐진 각본집.
            Color page = new Color(.86f, .78f, .63f);
            Color pageShadow = new Color(.49f, .38f, .36f);
            Fill(texture, 250, 244, 506, 690, pageShadow);
            Fill(texture, 518, 244, 774, 690, pageShadow);
            Quad(texture, new Vector2Int(274, 274), new Vector2Int(494, 236),
                new Vector2Int(494, 655), new Vector2Int(274, 705), page);
            Quad(texture, new Vector2Int(530, 236), new Vector2Int(750, 274),
                new Vector2Int(750, 705), new Vector2Int(530, 655), page);
            Fill(texture, 501, 225, 523, 696, new Color(.24f, .09f, .18f));
            for (int i = 0; i < 7; i++)
            {
                Fill(texture, 314, 335 + i * 39, 458 - (i % 3) * 18, 345 + i * 39, pageShadow);
                Fill(texture, 566 + (i % 2) * 16, 335 + i * 39, 710, 345 + i * 39, pageShadow);
            }

            // 각본에서 깨어나는 그림자의 눈.
            Color aura = new Color(.36f, .10f, .68f);
            for (int radius = 160; radius >= 35; radius -= 8)
            {
                float t = (160 - radius) / 125f;
                Color ring = Color.Lerp(aura, new Color(.72f, .40f, 1f), t);
                Ring(texture, 512, 650, radius, 6, ring);
            }
            Color ink = new Color(.018f, .012f, .04f);
            Diamond(texture, 512, 658, 188, 92, ink);
            Diamond(texture, 512, 658, 103, 48, new Color(.68f, .29f, 1f));
            Ellipse(texture, 512, 658, 23, 47, Color.white);
            Ellipse(texture, 512, 658, 9, 31, new Color(.10f, .025f, .18f));

            texture.Apply(false, false);
            File.WriteAllBytes(IconPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static void Fill(Texture2D texture, int x0, int y0, int x1, int y1, Color color)
        {
            x0 = Mathf.Clamp(x0, 0, texture.width - 1); x1 = Mathf.Clamp(x1, 0, texture.width - 1);
            y0 = Mathf.Clamp(y0, 0, texture.height - 1); y1 = Mathf.Clamp(y1, 0, texture.height - 1);
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) texture.SetPixel(x, y, color);
        }

        private static void Ellipse(Texture2D texture, int cx, int cy, int rx, int ry, Color color)
        {
            for (int y = -ry; y <= ry; y++) for (int x = -rx; x <= rx; x++)
                if (x * x / (float)(rx * rx) + y * y / (float)(ry * ry) <= 1f)
                    texture.SetPixel(cx + x, cy + y, color);
        }

        private static void Ring(Texture2D texture, int cx, int cy, int radius, int width, Color color)
        {
            int inner = (radius - width) * (radius - width), outer = radius * radius;
            for (int y = -radius; y <= radius; y++) for (int x = -radius; x <= radius; x++)
            {
                int d = x * x + y * y;
                if (d >= inner && d <= outer) texture.SetPixel(cx + x, cy + y, color);
            }
        }

        private static void Diamond(Texture2D texture, int cx, int cy, int rx, int ry, Color color)
        {
            for (int y = -ry; y <= ry; y++)
            {
                int span = Mathf.RoundToInt(rx * (1f - Mathf.Abs(y) / (float)ry));
                Fill(texture, cx - span, cy + y, cx + span, cy + y, color);
            }
        }

        private static void Quad(Texture2D texture, Vector2Int a, Vector2Int b, Vector2Int c, Vector2Int d, Color color)
        {
            int minY = Mathf.Min(a.y, b.y, c.y, d.y), maxY = Mathf.Max(a.y, b.y, c.y, d.y);
            for (int y = minY; y <= maxY; y++)
            {
                float p = Mathf.InverseLerp(minY, maxY, y);
                int left = Mathf.RoundToInt(Mathf.Lerp(a.x, d.x, p));
                int right = Mathf.RoundToInt(Mathf.Lerp(b.x, c.x, p));
                Fill(texture, left, y, right, y, color);
            }
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder("Assets/Art/Branding")) AssetDatabase.CreateFolder("Assets/Art", "Branding");
        }
    }
}
#endif
