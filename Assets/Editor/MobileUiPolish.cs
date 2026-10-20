#if UNITY_EDITOR
using System.Linq;
using System.IO;
using ShadowTheater.Field;
using ShadowTheater.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.EditorTools
{
    public static class MobileUiPolish
    {
        public static void ApplyToGeneratedPrefabs()
        {
            GenerateControlPlate();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/UI" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                foreach (CanvasScaler scaler in root.GetComponentsInChildren<CanvasScaler>(true))
                { scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 1f; }
                foreach (VerticalLayoutGroup layout in root.GetComponentsInChildren<VerticalLayoutGroup>(true))
                {
                    // A stretched template starts with sizeDelta.x == 0. Layout must own its width.
                    layout.childControlWidth = true; layout.childForceExpandWidth = true;
                    layout.childControlHeight = true; layout.childForceExpandHeight = false;
                }
                foreach (Text text in root.GetComponentsInChildren<Text>(true)) text.raycastTarget = false;
                foreach (Button button in root.GetComponentsInChildren<Button>(true))
                {
                    if (button.GetComponent<HandheldButtonFeedback>() != null || button.GetComponent<VirtualDPadButton>() != null) continue;
                    var image = button.targetGraphic as Image;
                    HandheldUiKit.Button(button, image != null ? image.color : HandheldUiKit.Violet, false);
                }
                var dockButtons = root.GetComponentsInChildren<Transform>(true).Where(x =>
                    x.name == "OpenScriptBook" || x.name == "OpenParty" || x.name == "OpenWorldMap" || x.name == "OpenShop" || x.name == "OpenMenu").ToArray();
                RectTransform dock = null;
                if (dockButtons.Length > 0)
                {
                    var existing = root.transform.Find("FieldMenuSafeArea");
                    var dockObject = existing != null ? existing.gameObject : new GameObject("FieldMenuSafeArea", typeof(RectTransform), typeof(MobileSafeArea));
                    dockObject.transform.SetParent(root.transform, false); dock = (RectTransform)dockObject.transform;
                    Anchors(dock, Vector2.zero, Vector2.one); dock.SetAsFirstSibling();
                }
                foreach (Transform button in dockButtons)
                {
                    button.SetParent(dock, false);
                    int slot = button.name == "OpenScriptBook" ? 0 : button.name == "OpenParty" ? 1
                        : button.name == "OpenWorldMap" ? 2 : button.name == "OpenShop" ? 3 : -1;
                    if (slot >= 0) Fixed(button as RectTransform, new Vector2(0, 1), new Vector2(20 + slot * 154, -18), new Vector2(140, 58));
                    else Fixed(button as RectTransform, Vector2.one, new Vector2(-20, -18), new Vector2(120, 58));
                }
                Transform menu = root.transform.Find("OpenPause");
                if (menu == null) menu = root.transform.Find("MenuButton");
                if (menu != null) Fixed(menu as RectTransform, Vector2.one, new Vector2(-20, -18), new Vector2(120, 58));
                if (root.name == "ScriptBookCanvas")
                {
                    // Side-by-side book: scrollable roster on the left, wide reading area on the right.
                    RectTransform panel = root.transform.Find("BookRoot") as RectTransform;
                    Anchors(panel.Find("ListPanel") as RectTransform, new Vector2(.035f, .055f), new Vector2(.38f, .82f));
                    Anchors(panel.Find("DetailPanel") as RectTransform, new Vector2(.40f, .055f), new Vector2(.965f, .82f));
                    foreach (Text text in panel.GetComponentsInChildren<Text>(true))
                    { text.resizeTextForBestFit = true; text.resizeTextMinSize = 16; text.resizeTextMaxSize = text.fontSize; }
                    RectTransform detail = panel.Find("DetailPanel") as RectTransform;
                    RectTransform lore = detail.Find("Lore") as RectTransform;
                    if (lore != null)
                    {
                        var viewportObject = new GameObject("LoreScroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
                        viewportObject.transform.SetParent(detail, false);
                        var viewport = (RectTransform)viewportObject.transform;
                        Anchors(viewport, lore.anchorMin, lore.anchorMax);
                        viewportObject.GetComponent<Image>().color = new Color(.03f, .02f, .06f, .3f);
                        lore.SetParent(viewport, false); lore.anchorMin = new Vector2(0, 1); lore.anchorMax = Vector2.one;
                        lore.pivot = new Vector2(.5f, 1); lore.offsetMin = lore.offsetMax = Vector2.zero;
                        lore.GetComponent<Text>().resizeTextForBestFit = false;
                        lore.GetComponent<Text>().verticalOverflow = VerticalWrapMode.Overflow;
                        lore.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                        var scroll = viewportObject.GetComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = lore;
                        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
                    }
                }
                if (root.name == "BattleCanvas")
                {
                    RectTransform options = root.transform.Find("SafeArea/Options") as RectTransform;
                    Anchors(options, new Vector2(.08f, .035f), new Vector2(.92f, .50f));
                    foreach (BattleUnitPanel unit in root.GetComponentsInChildren<BattleUnitPanel>(true))
                        foreach (Text text in unit.GetComponentsInChildren<Text>(true))
                        { text.resizeTextForBestFit = true; text.resizeTextMinSize = 14; text.resizeTextMaxSize = text.fontSize; }
                }
                if (root.name == "QuestHUDCanvas")
                {
                    Fixed(root.transform.Find("QuestPanel") as RectTransform, Vector2.one, new Vector2(-20, -92), new Vector2(360, 150));
                    foreach (Text text in root.transform.Find("QuestPanel").GetComponentsInChildren<Text>(true))
                        text.fontSize = text.name == "Title" ? 23 : text.name == "Objectives" ? 19 : 15;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path); PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
        }
        private static void GenerateControlPlate()
        {
            const string folder = "Assets/Resources/Art/UI";
            Directory.CreateDirectory(folder);
            string path = folder + "/ControlPlate.png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Abs(x - 31.5f) - 19.5f), dy = Mathf.Max(0, Mathf.Abs(y - 31.5f) - 19.5f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    Color color = distance > 11 ? Color.clear : distance > 9 ? new Color(.62f, .47f, .85f, .9f)
                        : Color.Lerp(new Color(.12f, .08f, .22f, .9f), new Color(.24f, .17f, .36f, .92f), y / 63f);
                    texture.SetPixel(x, y, color);
                }
                texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(13, 13, 13, 13); importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        public static void Fixed(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor; rect.sizeDelta = size; rect.anchoredPosition = position;
        }
        public static void Anchors(RectTransform rect, Vector2 min, Vector2 max)
        { if (rect == null) return; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
#endif
