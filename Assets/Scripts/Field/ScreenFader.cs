using System.Collections;
using UnityEngine;

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
        private CanvasGroup _group;

        private void Awake()
        {
            Instance = this;
            _group = GetComponent<CanvasGroup>();
            SetAlpha(0f);
        }

        public IEnumerator FadeOut(float duration = -1f) => Fade(1f, duration < 0 ? defaultDuration : duration);
        public IEnumerator FadeIn(float duration = -1f) => Fade(0f, duration < 0 ? defaultDuration : duration);

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
    }
}
