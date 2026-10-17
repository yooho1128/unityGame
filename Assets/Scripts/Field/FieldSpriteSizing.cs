using UnityEngine;

namespace ShadowTheater.Field
{
    public static class FieldSpriteSizing
    {
        // Field sprites may have different source resolutions; one actor occupies one tile.
        public static void Fit(SpriteRenderer renderer, float height = 1f)
        {
            if (renderer == null || renderer.sprite == null) return;
            Vector3 size = renderer.sprite.bounds.size;
            float scale = Mathf.Min(height / Mathf.Max(.01f, size.y), 1.15f / Mathf.Max(.01f, size.x));
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
