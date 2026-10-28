using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.Field
{
    /// <summary>Replaces legacy borrowed shadow art and adds a non-interactive world-space nameplate.</summary>
    [DisallowMultipleComponent]
    public sealed class FieldNpcIdentity : MonoBehaviour
    {
        [SerializeField] private string identityId;
        public string IdentityId => identityId;
        public void Configure(string id) => identityId = id;

        private void Start()
        {
            if (string.IsNullOrEmpty(identityId)) identityId = ResolveId();
            var entry = NpcIdentityCatalog.Find(identityId);
            var renderer = transform.Find("Visual")?.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = GetComponentInChildren<SpriteRenderer>();
            if (entry == null)
            {
                Debug.LogError($"[NpcIdentity] Unregistered NPC: {name} ({identityId})", this);
                if (renderer != null) renderer.enabled = false;
                return;
            }
            var sprite = NpcIdentityCatalog.Portrait(identityId);
            if (renderer != null)
            {
                // Never keep the borrowed monster sprite, even when an art asset is missing.
                renderer.sprite = sprite;
                renderer.color = Color.white;
                renderer.enabled = sprite != null;
                FieldSpriteSizing.Fit(renderer);
                var motion = renderer.GetComponent<ShadowFieldMotion>();
                if (motion != null) motion.ConfigureNpc(sprite);
            }
            if (sprite == null) Debug.LogError($"[NpcIdentity] Missing dedicated art: {identityId}", this);
            BuildNameplate(entry);
        }

        public static string ServiceId(string region, string role) => $"town_{region}_{role}";
        private string ResolveId()
        {
            var story = GetComponent<StoryNpc>();
            if (story != null) return story.NpcId;
            var region = RegionRepository.GetByScene(gameObject.scene.name);
            if (region == null) return string.Empty;
            string role = GetComponent<TownHealerNpc>() != null ? "healer" :
                GetComponent<TownRumorNpc>() != null ? "rumor" :
                GetComponent<BossInvestigatorNpc>() != null ? "investigator" :
                GetComponent<TownResidentRequest>() != null ? "requester" : string.Empty;
            var recovery = GetComponent<TownRecoveryNpc>();
            if (recovery != null) role = recovery.RequiredStage >= 3 ? "merchant" : "craftsman";
            return string.IsNullOrEmpty(role) ? string.Empty : ServiceId(region.regionId, role);
        }

        private void BuildNameplate(NpcIdentityCatalog.Entry entry)
        {
            if (transform.Find("NpcNameplate") != null) return;
            var root = new GameObject("NpcNameplate", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            root.transform.localScale = Vector3.one * .008f;
            var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(190f, 56f);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 306;
            // No GraphicRaycaster: nameplates must not intercept taps on the field.
            var plate = new GameObject("Plate", typeof(RectTransform), typeof(Image));
            plate.transform.SetParent(root.transform, false);
            Stretch((RectTransform)plate.transform, Vector2.zero, Vector2.one);
            var image = plate.GetComponent<Image>();
            image.color = new Color(.035f, .055f, .085f, .92f); image.raycastTarget = false;
            Label(root.transform, "Name", L10n.Text(entry.name), new Vector2(0f, .34f),
                Vector2.one, 24, TheatreUiStyle.Ivory);
            Label(root.transform, "Role", L10n.Text(entry.role), Vector2.zero,
                new Vector2(1f, .39f), 15, TheatreUiStyle.Brass);
        }
        private static void Label(Transform parent, string name, string value, Vector2 min, Vector2 max, int size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, min, max);
            var text = go.GetComponent<Text>(); text.font = LocalizationRuntime.CurrentFont;
            text.text = value; text.fontSize = size; text.color = color;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = size - 5; text.resizeTextMaxSize = size;
        }
        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = new Vector2(5f, 0f); rect.offsetMax = new Vector2(-5f, 0f);
        }
    }
}
