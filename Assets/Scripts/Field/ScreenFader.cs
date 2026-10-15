using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.Field
{
    /// <summary>
    /// 전체 화면 페이드. 최상단 Canvas(Sort Order 높게) 아래 검은 Image + CanvasGroup에 부착.
    /// DOTween 의존 없이 동작 (unscaledDeltaTime 사용 → 배속/일시정지 영향 없음).
    /// 나중에 "막(커튼)이 닫히는" 연출로 바꾸고 싶으면 이 클래스만 교체하면 된다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        [SerializeField] private float defaultDuration = 0.35f;
        [SerializeField] private Image overlay;
        [SerializeField] private Image encounterSilhouette;
        private CanvasGroup _group;

        private void Awake()
        {
            Instance = this;
            _group = GetComponent<CanvasGroup>();
            if (overlay == null) overlay = GetComponent<Image>();
            EnsureEncounterSilhouette();
            SetAlpha(0f);
        }

        public IEnumerator FadeOut(float duration = -1f) => Fade(1f, duration < 0 ? defaultDuration : duration);
        public IEnumerator FadeIn(float duration = -1f) => Fade(0f, duration < 0 ? defaultDuration : duration);
        public void SetOpaque() => SetAlpha(1f);
        public void SetClear() => SetAlpha(0f);

        /// <summary>지역 포인트 색과 적 실루엣을 보여 준 뒤 검은 화면으로 닫는 랜덤 조우 전용 전환.</summary>
        public IEnumerator PlayEncounterIntro(Sprite silhouette, Color regionColor, float duration = .7f)
        {
            EnsureEncounterSilhouette();
            duration = Mathf.Max(.1f, duration);
            regionColor.a = 1f;
            if (overlay != null) overlay.color = regionColor;
            bool showSilhouette = encounterSilhouette != null && silhouette != null;
            if (encounterSilhouette != null)
            {
                encounterSilhouette.sprite = silhouette;
                encounterSilhouette.preserveAspect = true;
                encounterSilhouette.color = new Color(0f, 0f, 0f, 0f);
                encounterSilhouette.rectTransform.localScale = Vector3.one * .72f;
                encounterSilhouette.gameObject.SetActive(showSilhouette);
            }

            _group.blocksRaycasts = true;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float p = Mathf.Clamp01(elapsed / duration);
                float reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p / .62f));
                float blacken = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p - .64f) / .36f));
                SetAlpha(reveal);
                if (overlay != null) overlay.color = Color.Lerp(regionColor, Color.black, blacken);
                if (showSilhouette)
                {
                    encounterSilhouette.color = new Color(0f, 0f, 0f, reveal * (1f - blacken));
                    encounterSilhouette.rectTransform.localScale = Vector3.one * Mathf.Lerp(.72f, 1.05f, reveal);
                }
                yield return null;
            }

            if (overlay != null) overlay.color = Color.black;
            if (encounterSilhouette != null) encounterSilhouette.gameObject.SetActive(false);
            SetAlpha(1f);
        }

        private IEnumerator Fade(float target, float duration)
        {
            _group.blocksRaycasts = true;
            float start = _group.alpha;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(start, target, t / duration));
                yield return null;
            }
            SetAlpha(target);
        }

        private void SetAlpha(float a)
        {
            _group.alpha = a;
            _group.blocksRaycasts = a > 0.01f;
            _group.interactable = false;
        }

        private void EnsureEncounterSilhouette()
        {
            if (encounterSilhouette != null) return;
            Transform existing = transform.Find("EncounterSilhouette");
            if (existing != null) encounterSilhouette = existing.GetComponent<Image>();
            if (encounterSilhouette != null) return;

            var go = new GameObject("EncounterSilhouette", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            encounterSilhouette = go.GetComponent<Image>();
            RectTransform rect = encounterSilhouette.rectTransform;
            rect.anchorMin = new Vector2(.22f, .22f);
            rect.anchorMax = new Vector2(.78f, .78f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            encounterSilhouette.raycastTarget = false;
            encounterSilhouette.gameObject.SetActive(false);
        }
    }
}
