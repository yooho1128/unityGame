using ShadowTheater.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>Transient, non-interactive damage/healing feedback above the affected portrait.</summary>
    public sealed class BattleFloatingFeedback : MonoBehaviour
    {
        private RectTransform _rect;
        private CanvasGroup _group;
        private Vector2 _home;
        private float _elapsed, _duration;

        public static BattleFloatingFeedback Create(Transform parent, BattleUnitPanel target, Font font,
            string message, Color color, float speed)
        {
            if (parent == null || target == null) return null;
            var go = new GameObject("BattleFeedback", typeof(RectTransform), typeof(CanvasGroup), typeof(Text), typeof(Outline), typeof(BattleFloatingFeedback));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f); rect.sizeDelta = new Vector2(310,110);
            var portrait = target.Portrait != null ? target.Portrait.rectTransform : target.MotionRoot;
            Vector3 center = portrait.TransformPoint(portrait.rect.center + Vector2.up * portrait.rect.height * .3f);
            rect.position = center;
            // Keep numbers in the safe visible portion, including near the upper screen edge.
            var area = parent as RectTransform;
            if (area != null)
                rect.anchoredPosition = new Vector2(Mathf.Clamp(rect.anchoredPosition.x, area.rect.xMin + 160, area.rect.xMax - 160),
                    Mathf.Clamp(rect.anchoredPosition.y, area.rect.yMin + 60, area.rect.yMax - 110));
            var text = go.GetComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = message; text.fontSize = 34; text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter; text.color = color; text.raycastTarget = false;
            go.GetComponent<Outline>().effectColor = new Color(.02f,.01f,.04f,.95f);
            go.GetComponent<Outline>().effectDistance = new Vector2(2,-2);
            var feedback = go.GetComponent<BattleFloatingFeedback>();
            feedback._rect = rect; feedback._group = go.GetComponent<CanvasGroup>();
            feedback._group.blocksRaycasts = false; feedback._group.interactable = false;
            feedback._home = rect.anchoredPosition;
            feedback._duration = Mathf.Clamp(.75f / Mathf.Max(.1f,speed), .32f, 1.2f);
            feedback.Sample(0f);
            return feedback;
        }

        public static string HitText(HitResult hit)
        {
            if (hit.missed) return L10n.Get("battle.feedback_miss", "빗나감");
            string label = hit.critical ? L10n.Get("battle.feedback_critical", "치명타!") :
                hit.IsEffective ? L10n.Get("battle.feedback_effective", "효과 탁월") :
                hit.IsResisted ? L10n.Get("battle.feedback_resisted", "효과 약함") : "";
            return (string.IsNullOrEmpty(label) ? "" : label + "\n") + "−" + Mathf.Max(0,hit.damage);
        }

        public void Sample(float progress)
        {
            if (_rect == null) return;
            float p = Mathf.Clamp01(progress);
            _rect.anchoredPosition = _home + Vector2.up * (Mathf.SmoothStep(0,1,p) * 48f);
            _rect.localScale = Vector3.one * (1f + Mathf.Sin(Mathf.Clamp01(p / .35f) * Mathf.PI) * .18f);
            _group.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, 1f, p));
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            Sample(_elapsed / _duration);
            if (_elapsed >= _duration) Destroy(gameObject);
        }
        private void OnDisable() { if (Application.isPlaying) Destroy(gameObject); }
    }
}
