using System.Collections;
using ShadowTheater.Field;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>필드 진입 시 막·지역·환경을 잠깐 보여 주는 비차단 타이틀 카드.</summary>
    public class RegionArrivalBanner : MonoBehaviour
    {
        [SerializeField] private CanvasGroup root;
        [SerializeField] private RectTransform card;
        [SerializeField] private Image accent;
        [SerializeField] private Image atmosphereGlow;
        [SerializeField] private Image motifLeft;
        [SerializeField] private Image motifRight;
        [SerializeField] private Text actText;
        [SerializeField] private Text regionText;
        [SerializeField] private Text environmentText;
        [SerializeField, Min(0f)] private float delay = .18f;
        [SerializeField, Min(.1f)] private float fadeIn = .32f;
        [SerializeField, Min(0f)] private float hold = 1.45f;
        [SerializeField, Min(.1f)] private float fadeOut = .42f;

        private Vector2 _home;

        private IEnumerator Start()
        {
            if (root != null) root.alpha = 0f;
            if (card != null) _home = card.anchoredPosition;
            yield return null;

            RegionData region = RegionRepository.GetByScene(SceneManager.GetActiveScene().name);
            if (region == null) yield break;
            if (actText != null) actText.text = L10n.Get($"region.{region.regionId}.act", L10n.Text(region.actTitle));
            if (regionText != null) regionText.text = L10n.Get($"region.{region.regionId}.name", L10n.Text(region.displayName));
            if (environmentText != null)
            {
                FieldWorldSnapshot world = FieldWorldState.Current(region);
                environmentText.text =
                    L10n.Get($"region.{region.regionId}.environment", L10n.Text(region.environment)) + " · " +
                    L10n.Text(FieldWorldState.TimeLabel(world.timeOfDay)) + " · " +
                    L10n.Text(FieldWorldState.WeatherLabel(world.weather));
            }
            if (accent != null) accent.color = region.AccentColor;
            ConfigureRegionalArt(region);

            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            yield return Fade(0f, 1f, fadeIn, 28f, 0f);
            if (hold > 0f) yield return new WaitForSecondsRealtime(hold);
            yield return Fade(1f, 0f, fadeOut, 0f, -18f);
        }

        private void ConfigureRegionalArt(RegionData region)
        {
            Color color = region.AccentColor;
            if (atmosphereGlow != null)
                atmosphereGlow.color = new Color(color.r, color.g, color.b, region.isBossStage ? .28f : .16f);
            if (motifLeft != null)
            {
                motifLeft.color = new Color(color.r, color.g, color.b, .34f);
                motifLeft.rectTransform.localRotation = Quaternion.Euler(0f, 0f,
                    region.isDungeon ? -38f : region.isSettlement ? -8f : -22f);
                motifLeft.rectTransform.localScale = region.isBossStage ? new Vector3(1.45f, 1.45f, 1f) : Vector3.one;
            }
            if (motifRight != null)
            {
                Color pale = Color.Lerp(color, Color.white, .38f);
                motifRight.color = new Color(pale.r, pale.g, pale.b, region.isHidden ? .42f : .24f);
                motifRight.rectTransform.localRotation = Quaternion.Euler(0f, 0f,
                    region.isLegendary ? 45f : region.isHidden ? 18f : 28f);
                motifRight.rectTransform.localScale = region.isLegendary ? new Vector3(1.35f, 1.35f, 1f) : Vector3.one;
            }
        }

        private IEnumerator Fade(float from, float to, float duration, float fromY, float toY)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                if (root != null) root.alpha = Mathf.Lerp(from, to, t);
                if (card != null) card.anchoredPosition = _home + Vector2.up * Mathf.Lerp(fromY, toY, t);
                yield return null;
            }
            if (root != null) root.alpha = to;
            if (card != null) card.anchoredPosition = _home + Vector2.up * toY;
        }
    }
}
