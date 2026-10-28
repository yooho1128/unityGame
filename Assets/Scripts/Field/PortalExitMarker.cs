using ShadowTheater.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.Field
{
    /// <summary>Keep exits readable above terrain and place their direction/name toward the playable area.</summary>
    [DisallowMultipleComponent]
    public sealed class PortalExitMarker : MonoBehaviour
    {
        private MapPortal portal;
        private Text label;
        private float refreshAt;
        private Material unlit;
        private void Start()
        {
            portal = GetComponent<MapPortal>();
            if (portal == null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            if (shader != null) unlit = new Material(shader) { name = "RuntimeExitUnlit" };
            foreach (var sprite in GetComponentsInChildren<SpriteRenderer>(true))
            {
                sprite.sortingOrder = 320;
                sprite.color = new Color(.9f, .82f, 1f, 1f);
                if (unlit != null) sprite.sharedMaterial = unlit;
            }
            var root = new GameObject("ExitDestination", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false);
            Vector3 p = transform.position;
            Vector3 inward = Mathf.Abs(p.x) > Mathf.Abs(p.y) ? (p.x > 0 ? Vector3.left : Vector3.right)
                : (p.y > 0 ? Vector3.down : Vector3.up);
            root.transform.localPosition = inward * .95f;
            root.transform.localScale = Vector3.one * .0125f;
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 340;
            ((RectTransform)root.transform).sizeDelta = new Vector2(310, 54);
            var plate = new GameObject("Plate", typeof(RectTransform), typeof(Image)); plate.transform.SetParent(root.transform, false);
            Stretch((RectTransform)plate.transform); plate.GetComponent<Image>().color = new Color(.03f,.05f,.09f,.94f);
            plate.GetComponent<Image>().raycastTarget = false;
            var textObject = new GameObject("Name", typeof(RectTransform), typeof(Text)); textObject.transform.SetParent(root.transform, false);
            Stretch((RectTransform)textObject.transform); label = textObject.GetComponent<Text>();
            label.font = LocalizationRuntime.CurrentFont; label.fontSize = 25; label.color = new Color(1f,.88f,.59f);
            label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 14; label.resizeTextMaxSize = 25;
            UpdateLabel();
        }
        private void Update()
        { if (label != null && Time.unscaledTime >= refreshAt) UpdateLabel(); }
        private void UpdateLabel()
        {
            Vector3 p = transform.position;
            string arrow = Mathf.Abs(p.x) > Mathf.Abs(p.y) ? (p.x > 0 ? "→" : "←") : (p.y > 0 ? "↑" : "↓");
            label.text = arrow + " " + portal.DestinationName;
            refreshAt = Time.unscaledTime + .4f;
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
        private void OnDestroy() { if (unlit != null) Destroy(unlit); }
    }
}
