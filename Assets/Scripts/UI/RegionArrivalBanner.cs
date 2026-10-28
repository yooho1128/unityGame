using System.Collections;
using System.Collections.Generic;
using ShadowTheater.Field;
using ShadowTheater.Save;
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
        [SerializeField] private Text discoveryText;
        [SerializeField, Min(0f)] private float delay = .18f;
        [SerializeField, Min(.1f)] private float fadeIn = .32f;
        [SerializeField, Min(0f)] private float hold = 1.45f;
        [SerializeField, Min(.1f)] private float fadeOut = .42f;

        private Vector2 _home;

        private IEnumerator Start()
        {
            StylePresentation();
            if (root != null) root.alpha = 0f;
            if (card != null) _home = card.anchoredPosition;
            yield return null;

            RegionData region = RegionRepository.GetByScene(SceneManager.GetActiveScene().name);
            if (region == null) yield break;
            bool hasReward = ExplorationRewardService.TryClaimArrival(region, out ExplorationReward reward);
            if (discoveryText != null)
            {
                discoveryText.gameObject.SetActive(hasReward);
                discoveryText.text = hasReward ? RewardText(reward) : string.Empty;
            }
            if (hasReward) SaveManager.Instance?.TrySave(false);
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

        private static string RewardText(ExplorationReward reward)
        {
            var parts = new List<string>();
            if (reward.regionDiscovered) parts.Add(L10n.Get("exploration.new_region", "새 지역 기록"));
            if (reward.gold > 0) parts.Add(L10n.Format("exploration.gold", "금화 +{0}", reward.gold));
            if (reward.bondExperience > 0) parts.Add(L10n.Format("exploration.bond", "동행 유대 +{0}", reward.bondExperience));
            if (reward.potionCount > 0) parts.Add(L10n.Format("exploration.potion", "작은 회복 시약 x{0}", reward.potionCount));
            string text = string.Join(" · ", parts);
            if (reward.milestone > 0)
                text += "\n" + L10n.Format("exploration.milestone", "탐험 이정표 {0}/{1} 달성",
                    reward.milestone, reward.totalRegions);
            return text;
        }

        private void StylePresentation()
        {
            Color brass = new Color(.85f, .71f, .43f);
            if (card != null)
            {
                var background = card.GetComponent<Image>();
                if (background != null) background.color = new Color(.035f, .055f, .085f, .95f);
                var outline = card.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = new Color(brass.r, brass.g, brass.b, .5f);
                    outline.effectDistance = new Vector2(1f, -1f);
                }
            }
            if (actText != null) actText.color = brass;
            if (regionText != null)
            {
                regionText.color = new Color(.95f, .92f, .85f);
                regionText.resizeTextForBestFit = true;
                regionText.resizeTextMinSize = 32;
                regionText.resizeTextMaxSize = 48;
            }
            if (environmentText != null) environmentText.color = new Color(.72f, .80f, .84f);
            if (discoveryText != null) discoveryText.color = brass;
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
