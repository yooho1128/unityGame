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

        internal static void Panel(Image image, Color fill, bool shadow = true, float outlineAlpha = .34f)
        {
            TheatreUiStyle.Panel(image, fill);
        }

        internal static void Button(Button button, Color fill, bool strongOutline = true)
        {
            TheatreUiStyle.Button(button, fill == Yellow);
            // Inventory templates use paper surfaces and supply their own dark labels afterwards.
            if (button != null && fill.r > .8f && fill.g > .8f && fill.b > .8f)
                TheatreUiStyle.SurfaceImage(button.targetGraphic as Image, fill);
        }

        internal static void DarkText(Text text)
        {
            if (text != null) text.color = Ink;
        }
    }
}
#endif
