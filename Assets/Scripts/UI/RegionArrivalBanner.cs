using System.Collections;
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
            if (environmentText != null) environmentText.text =
                L10n.Get($"region.{region.regionId}.environment", L10n.Text(region.environment));
            if (accent != null) accent.color = region.AccentColor;

            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            yield return Fade(0f, 1f, fadeIn, 28f, 0f);
            if (hold > 0f) yield return new WaitForSecondsRealtime(hold);
            yield return Fade(1f, 0f, fadeOut, 0f, -18f);
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
