using ShadowTheater.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>Shared stage palette; applies to existing scenes as well as generated prefabs.</summary>
    public static class TheatreUiStyle
    {
        public static readonly Color Midnight = new Color(.035f, .055f, .09f, 1f);
        public static readonly Color Surface = new Color(.075f, .10f, .145f, 1f);
        public static readonly Color Raised = new Color(.12f, .155f, .205f, 1f);
        public static readonly Color Ivory = new Color(.96f, .93f, .85f, 1f);
        public static readonly Color Muted = new Color(.65f, .71f, .76f, 1f);
        public static readonly Color Brass = new Color(.84f, .70f, .43f, 1f);
        public static readonly Color Mint = new Color(.58f, .82f, .73f, 1f);

        public static void Panel(Image image, Color fill)
        {
            if (image == null) return;
            image.color = fill;
            var outline = image.GetComponent<Outline>() ?? image.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Brass.r, Brass.g, Brass.b, .25f);
            outline.effectDistance = new Vector2(1f, -1f);
            var shadow = image.GetComponent<Shadow>();
            // Outline derives from Shadow; never configure it as a drop shadow.
            foreach (var candidate in image.GetComponents<Shadow>())
                if (!(candidate is Outline)) { shadow = candidate; break; }
            if (shadow == null || shadow is Outline) shadow = image.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, .01f, .025f, .32f);
            shadow.effectDistance = new Vector2(0f, -4f);
        }

        public static void Button(Button button, bool primary = false)
        {
            if (button == null) return;
            Panel(button.targetGraphic as Image, primary ? Brass : Raised);
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, .96f, .85f);
            colors.selectedColor = new Color(1f, .94f, .76f);
            colors.pressedColor = new Color(.76f, .79f, .84f);
            colors.disabledColor = new Color(.48f, .53f, .60f, .6f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = .12f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
            foreach (var text in button.GetComponentsInChildren<Text>(true))
            {
                text.color = primary ? Midnight : Ivory;
                text.raycastTarget = false;
            }
        }

        public static void Text(Text text, Color color, bool bold = false)
        {
            if (text == null) return;
            text.color = color;
            text.raycastTarget = false;
            if (bold) text.fontStyle = FontStyle.Bold;
        }

        public static void ApplyTitle(GameObject root)
        {
            if (root == null) return;
            var parent = root.transform;
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                Button(button, button.name == "NewGameButton");
            ColorText(parent, "Eyebrow", Brass);
            ColorText(parent, "Title", Ivory);
            ColorText(parent, "Subtitle", Muted);
            ColorText(parent, "SaveSummary", Mint);

            var veil = Decoration(parent, "StageVeil", Vector2.zero, Vector2.one,
                new Color(Midnight.r, Midnight.g, Midnight.b, .72f));
            veil.transform.SetAsFirstSibling();
            Decoration(parent, "StageRuleTop", new Vector2(.055f, .94f), new Vector2(.945f, .942f), Brass);
            Decoration(parent, "StageRuleBottom", new Vector2(.055f, .045f), new Vector2(.945f, .047f),
                new Color(Brass.r, Brass.g, Brass.b, .38f));

            bool landscape = Screen.width >= Screen.height * 1.2f;
            if (!landscape) return;
            Position(parent, "Eyebrow", .08f, .80f, .53f, .87f, TextAnchor.MiddleLeft);
            Position(parent, "Title", .08f, .53f, .55f, .80f, TextAnchor.MiddleLeft);
            Position(parent, "Subtitle", .08f, .46f, .56f, .54f, TextAnchor.MiddleLeft);
            Position(parent, "SaveSummary", .08f, .39f, .56f, .46f, TextAnchor.MiddleLeft);
            Position(parent, "NewGameButton", .08f, .29f, .30f, .37f);
            Position(parent, "ContinueButton", .32f, .29f, .54f, .37f);
            Position(parent, "NewCycleButton", .08f, .19f, .54f, .27f);
            Position(parent, "EndingGalleryButton", .08f, .09f, .25f, .17f);
            Position(parent, "SettingsButton", .27f, .09f, .42f, .17f);
            Position(parent, "QuitButton", .44f, .09f, .54f, .17f);
            Position(parent, "Feedback", .08f, .05f, .54f, .09f, TextAnchor.MiddleLeft);
            var database = ShadowDatabase.Instance;
            var shadow = database != null ? database.GetShadow("knight") : null;
            if (shadow == null || shadow.silhouetteSprite == null) return;
            var stage = Decoration(parent, "CastStage", new Vector2(.60f, .17f), new Vector2(.91f, .84f), Surface);
            stage.transform.SetSiblingIndex(1);
            Decoration(stage.transform, "StageAccent", new Vector2(0f, .985f), Vector2.one, Brass);
            var portrait = Decoration(stage.transform, "FeaturedCast", new Vector2(.04f, .06f), new Vector2(.96f, .94f), Color.white);
            portrait.sprite = shadow.silhouetteSprite;
            portrait.color = ShadowPortraitStyle.Tint(portrait.sprite);
            portrait.preserveAspect = true;
        }

        public static void ApplyBook(GameObject root)
        {
            if (root == null) return;
            var backdrop = root.GetComponent<Image>();
            if (backdrop != null) backdrop.color = Midnight;
            foreach (var name in new[] { "ListPanel", "DetailPanel" })
                Panel(root.transform.Find(name)?.GetComponent<Image>(), Surface);
            ColorText(root.transform, "Header", Ivory);
            ColorText(root.transform, "Progress", Brass);
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                if (button.GetComponent<ScriptBookEntryView>() == null) Button(button);
            var detail = root.transform.Find("DetailPanel");
            if (detail == null) return;
            ColorText(detail, "Name", Ivory);
            ColorText(detail, "Title", Muted);
            ColorText(detail, "Type", Brass);
            ColorText(detail, "MemoryStage", Mint);
            ColorText(detail, "Stats", Muted);
            ColorText(detail, "Lore", Ivory);
        }

        public static void BookFilter(GameObject root, ScriptBookFilter selected)
        {
            if (root == null) return;
            Button(root.transform.Find("All")?.GetComponent<Button>(), selected == ScriptBookFilter.All);
            Button(root.transform.Find("Seen")?.GetComponent<Button>(), selected == ScriptBookFilter.Seen);
            Button(root.transform.Find("Recorded")?.GetComponent<Button>(), selected == ScriptBookFilter.Recorded);
        }

        public static void ApplyStarterSelection(GameObject root)
        {
            if (root == null) return;
            var image = root.GetComponent<Image>();
            if (image != null) image.color = Midnight;
            ColorText(root.transform, "Header", Ivory);
            ColorText(root.transform, "Hint", Muted);
            Button(root.transform.Find("BackButton")?.GetComponent<Button>());
            Decoration(root.transform, "CastingRule", new Vector2(.075f, .80f), new Vector2(.925f, .802f), Brass);
        }

        private static void ColorText(Transform root, string name, Color color)
        {
            Text(root.Find(name)?.GetComponent<Text>(), color);
        }

        private static void Position(Transform root, string name, float x0, float y0, float x1, float y1,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var rect = root.Find(name) as RectTransform;
            if (rect == null) return;
            rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = rect.GetComponent<Text>();
            if (text != null) text.alignment = alignment;
        }

        private static Image Decoration(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var child = parent.Find(name);
            var image = child != null ? child.GetComponent<Image>() : null;
            if (image == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                image = go.GetComponent<Image>();
            }
            var rect = image.rectTransform;
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            image.color = color; image.raycastTarget = false;
            return image;
        }
    }
}
