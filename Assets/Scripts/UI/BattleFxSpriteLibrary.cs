using System.Collections.Generic;
using ShadowTheater.Data;
using UnityEngine;

namespace ShadowTheater.UI
{
    public enum BattleFxGlyph { Flame, Crystal, Crescent, Feather, Petal, Slash, Ring, Star, Rune, Thread, ContactShadow, Solid }

    /// <summary>속성별 이펙트 마스크를 공유하여 사각형 파편 대신 읽기 쉬운 실루엣을 사용한다.</summary>
    public static class BattleFxSpriteLibrary
    {
        private static readonly Dictionary<BattleFxGlyph, Sprite> Fallback = new Dictionary<BattleFxGlyph, Sprite>();
        public static Sprite Get(BattleFxGlyph glyph)
        {
            Sprite asset = Resources.Load<Sprite>("Art/FX/" + glyph);
            if (asset != null) return asset;
            if (Fallback.TryGetValue(glyph, out Sprite cached)) return cached;
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = glyph.ToString(), filterMode = FilterMode.Bilinear };
            texture.SetPixels32(Pixels(glyph)); texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 64);
            Fallback[glyph] = sprite;
            return sprite;
        }

        public static Sprite ForSkill(SkillData skill) => Get(skill.target == SkillTarget.Self ? BattleFxGlyph.Ring :
            skill.element == ShadowElement.Flame ? BattleFxGlyph.Flame : skill.element == ShadowElement.Frost ? BattleFxGlyph.Crystal :
            skill.element == ShadowElement.Shade ? BattleFxGlyph.Crescent :
            skill.damageType == DamageType.Physical ? BattleFxGlyph.Slash : BattleFxGlyph.Star);

        public static Sprite ForStyle(UltimateFxStyle style) => Get(
            style == UltimateFxStyle.FlameCrown ? BattleFxGlyph.Flame :
            style == UltimateFxStyle.FrozenArchive || style == UltimateFxStyle.FrozenMask ? BattleFxGlyph.Crystal :
            style == UltimateFxStyle.MoonBeast ? BattleFxGlyph.Crescent :
            style == UltimateFxStyle.PuppetThreads ? BattleFxGlyph.Thread :
            style == UltimateFxStyle.AshBird ? BattleFxGlyph.Feather :
            style == UltimateFxStyle.MoonPetals ? BattleFxGlyph.Petal :
            style == UltimateFxStyle.LivingScript ? BattleFxGlyph.Rune : BattleFxGlyph.Star);

        public static Color32[] Pixels(BattleFxGlyph glyph)
        {
            var pixels = new Color32[64 * 64];
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float u = (x + .5f) / 32f - 1, v = (y + .5f) / 32f - 1;
                float r = Mathf.Sqrt(u * u + v * v), a = 0;
                switch (glyph)
                {
                    case BattleFxGlyph.Solid: a = 1; break;
                    case BattleFxGlyph.Flame:
                        a = 1 - Mathf.Abs(u + Mathf.Sin(v * 5) * .13f) / Mathf.Max(.02f, (1 - v) * .32f);
                        a *= Mathf.Clamp01((v + .85f) * 6) * Mathf.Clamp01((.95f - v) * 8); break;
                    case BattleFxGlyph.Crystal: a = 1 - Mathf.Abs(u) * 3.8f - Mathf.Abs(v) * 1.15f; break;
                    case BattleFxGlyph.Crescent:
                        a = Mathf.Clamp01((.88f - r) * 12) * Mathf.Clamp01((Mathf.Sqrt((u - .32f) * (u - .32f) + v * v) - .67f) * 14); break;
                    case BattleFxGlyph.Feather:
                        a = Mathf.Clamp01((.3f * (1 - v * v) - Mathf.Abs(u - v * .23f)) * 18);
                        if (Mathf.Sin((v + u) * 38) < -.1f) a *= .55f; break;
                    case BattleFxGlyph.Petal: a = 1 - (u * u / .22f + v * v / .78f); break;
                    case BattleFxGlyph.Slash: a = 1 - Mathf.Abs(u - v * .27f) * 9 - Mathf.Abs(v) * .7f; break;
                    case BattleFxGlyph.Ring: a = 1 - Mathf.Abs(r - .67f) * 14; break;
                    case BattleFxGlyph.Star: a = Mathf.Max(1 - Mathf.Abs(u) * 12 - Mathf.Abs(v), 1 - Mathf.Abs(v) * 12 - Mathf.Abs(u)); break;
                    case BattleFxGlyph.Rune:
                        a = Mathf.Max(1 - Mathf.Abs(u) * 13, 1 - Mathf.Abs(v - .35f * Mathf.Sign(u)) * 16);
                        a *= Mathf.Clamp01((.72f - r) * 15); break;
                    case BattleFxGlyph.Thread: a = (1 - Mathf.Abs(u) * 15) * Mathf.Clamp01((.95f - Mathf.Abs(v)) * 14); break;
                    case BattleFxGlyph.ContactShadow: a = Mathf.Pow(Mathf.Clamp01(1 - u * u - v * v * 8), 1.5f); break;
                }
                pixels[y * 64 + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255));
            }
            return pixels;
        }
    }
}
