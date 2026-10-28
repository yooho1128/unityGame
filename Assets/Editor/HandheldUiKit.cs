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
        internal static readonly Color Ink = TheatreUiStyle.Midnight;
        internal static readonly Color Paper = new Color(.94f, .91f, .83f, 1f);
        internal static readonly Color White = TheatreUiStyle.Ivory;
        internal static readonly Color Cyan = new Color(.30f, .66f, .69f, 1f);
        internal static readonly Color Yellow = TheatreUiStyle.Brass;
        internal static readonly Color Coral = new Color(.83f, .43f, .40f, 1f);
        internal static readonly Color Violet = new Color(.48f, .43f, .65f, 1f);
        internal static readonly Color Mint = TheatreUiStyle.Mint;

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
            var outline = image.GetComponent<Outline>() ?? image.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Yellow.r, Yellow.g, Yellow.b, outlineAlpha);
            outline.effectDistance = new Vector2(1f, -1f);
            if (!shadow) return;
            Shadow drop = null;
            foreach (var effect in image.GetComponents<Shadow>())
                if (!(effect is Outline)) { drop = effect; break; }
            if (drop == null) drop = image.gameObject.AddComponent<Shadow>();
            drop.effectColor = new Color(.02f, .03f, .08f, .36f);
            drop.effectDistance = new Vector2(0f, -4f);
        }

        internal static void Button(Button button, Color fill, bool strongOutline = true)
        {
            if (button == null) return;
            var image = button.targetGraphic as Image;
            Panel(image, fill, true, strongOutline ? .55f : .25f);
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, .96f, .85f, 1f);
            colors.selectedColor = new Color(1f, .94f, .76f, 1f);
            colors.pressedColor = new Color(.78f, .80f, .84f, 1f);
            colors.disabledColor = new Color(.48f, .50f, .54f, .55f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = .12f;
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
