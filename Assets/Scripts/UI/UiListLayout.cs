using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>Refresh list contents without leaving clickable old rows or losing the scroll position.</summary>
    public static class UiListLayout
    {
        public static Vector2 Position(Transform content) => content is RectTransform rect ? rect.anchoredPosition : Vector2.zero;

        public static void Clear(List<GameObject> rows)
        {
            foreach (var row in rows)
            {
                if (row == null) continue;
                row.SetActive(false);
                if (Application.isPlaying) Object.Destroy(row); else Object.DestroyImmediate(row);
            }
            rows.Clear();
        }

        public static void Rebuild(Transform parent, Vector2 position)
        {
            if (!(parent is RectTransform content)) return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var scroll = content.GetComponentInParent<ScrollRect>();
            float maximum = scroll != null && scroll.viewport != null
                ? Mathf.Max(0f, content.rect.height - scroll.viewport.rect.height) : Mathf.Max(0f, position.y);
            if (scroll != null) scroll.StopMovement();
            content.anchoredPosition = new Vector2(position.x, Mathf.Clamp(position.y, 0f, maximum));
        }
    }
}
