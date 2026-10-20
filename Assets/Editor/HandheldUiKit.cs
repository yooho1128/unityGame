#if UNITY_EDITOR
using ShadowTheater.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.EditorTools
{
    /// <summary>Shared, original handheld-RPG visual language for generated UI prefabs.</summary>
    internal static class HandheldUiKit
    {
        internal static readonly Color Ink = new Color(.075f, .105f, .18f, 1f);
        internal static readonly Color Paper = new Color(.97f, .95f, .86f, 1f);
        internal static readonly Color White = new Color(1f, .995f, .96f, 1f);
        internal static readonly Color Cyan = new Color(.08f, .68f, .72f, 1f);
        internal static readonly Color Yellow = new Color(1f, .78f, .20f, 1f);
        internal static readonly Color Coral = new Color(.96f, .31f, .34f, 1f);
        internal static readonly Color Violet = new Color(.49f, .32f, .78f, 1f);
        internal static readonly Color Mint = new Color(.25f, .76f, .51f, 1f);

        private static Sprite _rounded;
        private static Sprite Rounded => _rounded != null
            ? _rounded
            : _rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        internal static void Panel(Image image, Color fill, bool shadow = true, float outlineAlpha = .34f)
        {
            if (image == null) return;
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            image.color = fill;
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Ink.r, Ink.g, Ink.b, outlineAlpha);
            outline.effectDistance = new Vector2(3f, -3f);
            if (!shadow) return;
            var drop = image.gameObject.AddComponent<Shadow>();
            drop.effectColor = new Color(.02f, .03f, .08f, .36f);
            drop.effectDistance = new Vector2(0f, -8f);
        }

        internal static void Button(Button button, Color fill, bool strongOutline = true)
        {
            if (button == null) return;
            var image = button.targetGraphic as Image;
            Panel(image, fill, true, strongOutline ? .55f : .25f);
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.selectedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(.78f, .80f, .84f, 1f);
            colors.disabledColor = new Color(.48f, .50f, .54f, .55f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = .08f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
            if (button.GetComponent<HandheldButtonFeedback>() == null)
                button.gameObject.AddComponent<HandheldButtonFeedback>();
        }

        internal static void DarkText(Text text)
        {
            if (text != null) text.color = Ink;
        }
    }
}
#endif
