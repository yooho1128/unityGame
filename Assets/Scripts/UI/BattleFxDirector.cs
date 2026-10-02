using System.Collections;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>별도 이펙트 에셋 없이도 진명 필살기 컷인과 테마 문양을 만드는 UI 연출기.</summary>
    public class BattleFxDirector : MonoBehaviour
    {
        [SerializeField] private RectTransform fxRoot;
        [SerializeField] private RectTransform stageRoot;
        [SerializeField] private BattleSfxPlayer sfxPlayer;
        [SerializeField] private Camera battleCamera;

        public IEnumerator Play(BattleUnitPanel attacker, BattleUnitPanel defender, SkillData skill,
                                HitResult hit, float playbackSpeed)
        {
            if (attacker == null || skill == null) yield break;
            float speed = Mathf.Max(0.1f, playbackSpeed);
            if (sfxPlayer == null) sfxPlayer = GetComponent<BattleSfxPlayer>();
            if (battleCamera == null || !battleCamera.isActiveAndEnabled) battleCamera = Camera.main;
            if (sfxPlayer != null) sfxPlayer.PlaySkill(skill);
            if (!skill.isUltimate)
            {
                if (skill.target == SkillTarget.Self) yield return Pulse(attacker, skill, 0.18f / speed, speed);
                else yield return Lunge(attacker, defender, skill, hit, 0.18f / speed, skill.cameraShake, speed);
                yield break;
            }

            RectTransform layer = CreateLayer($"Ultimate_{skill.skillId}");
            Image dim = AddImage("Dim", layer, Vector2.zero, Vector2.one,
                new Color(0.01f, 0.005f, 0.03f, 0f));
            Image flash = AddImage("Flash", layer, Vector2.zero, Vector2.one,
                WithAlpha(skill.primaryFxColor, 0f));
            Text title = AddText("TrueNameCutIn", layer, new Vector2(0.05f, 0.63f), new Vector2(0.95f, 0.79f),
                $"진명 필살기\n{skill.displayName}", skill.primaryFxColor, 46);

            Vector3 originalScale = attacker.MotionRoot.localScale;
            Vector3 originalPosition = attacker.MotionRoot.localPosition;
            Color originalPortrait = attacker.Portrait != null ? attacker.Portrait.color : Color.black;
            BuildMotif(layer, skill);

            float t = 0f;
            float intro = 0.28f / speed;
            while (t < intro)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / intro);
                dim.color = new Color(0.01f, 0.005f, 0.03f, Mathf.Lerp(0f, 0.82f, p));
                title.rectTransform.localScale = Vector3.Lerp(new Vector3(1.35f, 0.15f, 1f), Vector3.one, EaseOut(p));
                attacker.MotionRoot.localScale = Vector3.Lerp(originalScale, originalScale * 1.18f, EaseOut(p));
                if (attacker.Portrait != null)
                    attacker.Portrait.color = Color.Lerp(originalPortrait, skill.primaryFxColor, p * 0.7f);
                AnimateMotif(layer, p, skill);
                yield return null;
            }

            if (skill.target == SkillTarget.Self) yield return Pulse(attacker, skill, 0.24f / speed, speed);
            else yield return Lunge(attacker, defender, skill, hit, 0.24f / speed, skill.cameraShake + 0.5f, speed);

            t = 0f;
            float burst = 0.22f / speed;
            Vector2 stagePosition = stageRoot != null ? stageRoot.anchoredPosition : Vector2.zero;
            Vector3 cameraPosition = battleCamera != null ? battleCamera.transform.localPosition : Vector3.zero;
            while (t < burst)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / burst);
                flash.color = WithAlpha(skill.secondaryFxColor, Mathf.Sin(p * Mathf.PI) * 0.88f);
                title.color = Color.Lerp(skill.primaryFxColor, Color.white, Mathf.Sin(p * Mathf.PI));
                AnimateMotif(layer, 1f + p, skill);
                if (stageRoot != null)
                    stageRoot.anchoredPosition = stagePosition + Random.insideUnitCircle * (skill.cameraShake * 8f * (1f - p));
                if (battleCamera != null)
                    battleCamera.transform.localPosition = cameraPosition + (Vector3)(Random.insideUnitCircle * (skill.cameraShake * 0.1f * (1f - p)));
                yield return null;
            }

            attacker.MotionRoot.localScale = originalScale;
            attacker.MotionRoot.localPosition = originalPosition;
            if (attacker.Portrait != null) attacker.Portrait.color = originalPortrait;
            if (stageRoot != null) stageRoot.anchoredPosition = stagePosition;
            if (battleCamera != null) battleCamera.transform.localPosition = cameraPosition;
            Object.Destroy(layer.gameObject);
        }

        private static IEnumerator HitStop(float duration)
        {
            if (duration <= 0f) yield break;
            float previousScale = Time.timeScale;
            if (previousScale <= 0f)
            {
                yield return new WaitForSecondsRealtime(duration);
                yield break;
            }
            Time.timeScale = 0f;
            try
            {
                yield return new WaitForSecondsRealtime(duration);
            }
            finally
            {
                if (Mathf.Approximately(Time.timeScale, 0f)) Time.timeScale = previousScale;
            }
        }

        private IEnumerator Pulse(BattleUnitPanel panel, SkillData skill, float duration, float speed)
        {
            Vector3 start = panel.MotionRoot.localScale;
            float t = 0f;
            bool impacted = false;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);
                panel.MotionRoot.localScale = start * (1f + Mathf.Sin(p * Mathf.PI) * 0.16f);
                if (!impacted && p >= 0.5f)
                {
                    impacted = true;
                    if (sfxPlayer != null) sfxPlayer.PlayImpact(skill);
                    yield return HitStop(skill.hitStopDuration / Mathf.Sqrt(speed));
                }
                yield return null;
            }
            panel.MotionRoot.localScale = start;
        }

        private IEnumerator Lunge(BattleUnitPanel attacker, BattleUnitPanel defender, SkillData skill, HitResult hit,
                                  float duration, float shakePower, float speed)
        {
            RectTransform a = attacker.MotionRoot;
            Vector3 start = a.localPosition;
            Vector3 cameraPosition = battleCamera != null ? battleCamera.transform.localPosition : Vector3.zero;
            Vector3 target = defender != null
                ? start + (defender.MotionRoot.position - a.position) * 0.14f
                : start + Vector3.right * 70f;
            float half = Mathf.Max(0.03f, duration * 0.5f);
            float t = 0f;
            bool impacted = false;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / half);
                a.localPosition = t < half ? Vector3.Lerp(start, target, EaseOut(p)) : Vector3.Lerp(target, start, EaseOut((t - half) / half));
                if (t >= half && !hit.missed && defender != null)
                {
                    defender.MotionRoot.localPosition += (Vector3)Random.insideUnitCircle * (shakePower * 4f);
                    if (battleCamera != null)
                        battleCamera.transform.localPosition = cameraPosition + (Vector3)(Random.insideUnitCircle * shakePower * 0.035f);
                    if (!impacted)
                    {
                        impacted = true;
                        if (sfxPlayer != null) sfxPlayer.PlayImpact(skill);
                        yield return HitStop(skill.hitStopDuration / Mathf.Sqrt(speed));
                    }
                }
                yield return null;
            }
            a.localPosition = start;
            if (defender != null) defender.ResetMotion();
            if (battleCamera != null) battleCamera.transform.localPosition = cameraPosition;
        }

        private void BuildMotif(RectTransform layer, SkillData skill)
        {
            int count = Mathf.Clamp(skill.ultimateBurstCount, 6, 36);
            for (int i = 0; i < count; i++)
            {
                float angle = 360f * i / count;
                Image shard = AddImage($"Shard_{i:00}", layer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    WithAlpha(i % 2 == 0 ? skill.primaryFxColor : skill.secondaryFxColor, 0.85f));
                RectTransform r = shard.rectTransform;
                r.sizeDelta = MotifSize(skill.ultimateFxStyle, i);
                r.anchoredPosition = MotifPosition(skill.ultimateFxStyle, angle, i);
                r.localRotation = Quaternion.Euler(0f, 0f, MotifRotation(skill.ultimateFxStyle, angle));
                shard.raycastTarget = false;
            }
        }

        private static void AnimateMotif(RectTransform layer, float progress, SkillData skill)
        {
            for (int i = 3; i < layer.childCount; i++)
            {
                RectTransform r = layer.GetChild(i) as RectTransform;
                if (r == null) continue;
                float phase = i * 0.37f;
                r.localScale = Vector3.one * (0.25f + EaseOut(Mathf.Min(1f, progress)) * (0.8f + 0.25f * Mathf.Sin(phase)));
                r.localRotation *= Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 1f : -1f) * 0.8f);
            }
        }

        private RectTransform CreateLayer(string name)
        {
            Transform parent = fxRoot != null ? fxRoot : transform;
            if (fxRoot != null) fxRoot.SetAsLastSibling();
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Vector2 MotifSize(UltimateFxStyle style, int index)
        {
            switch (style)
            {
                case UltimateFxStyle.PuppetThreads: return new Vector2(5f, 620f);
                case UltimateFxStyle.LivingScript: return new Vector2(420f, 9f + index % 3 * 5f);
                case UltimateFxStyle.FrozenArchive:
                case UltimateFxStyle.FrozenMask: return new Vector2(18f, 150f + index % 4 * 34f);
                case UltimateFxStyle.CosmicAudience: return new Vector2(42f + index % 3 * 18f, 16f);
                default: return new Vector2(12f + index % 4 * 7f, 210f + index % 5 * 28f);
            }
        }

        private static Vector2 MotifPosition(UltimateFxStyle style, float angle, int index)
        {
            float rad = angle * Mathf.Deg2Rad;
            switch (style)
            {
                case UltimateFxStyle.PuppetThreads: return new Vector2(-430f + index * 860f / 18f, 40f);
                case UltimateFxStyle.LivingScript: return new Vector2(Mathf.Sin(index * 1.7f) * 230f, -330f + index * 42f);
                case UltimateFxStyle.CosmicAudience: return new Vector2(-390f + index % 7 * 130f, -290f + index / 7 * 150f);
                case UltimateFxStyle.AshBird: return new Vector2(Mathf.Cos(rad) * 260f, Mathf.Abs(Mathf.Sin(rad)) * 300f - 80f);
                case UltimateFxStyle.FlameCrown: return new Vector2(Mathf.Cos(rad) * 330f, Mathf.Abs(Mathf.Sin(rad)) * 250f - 260f);
                case UltimateFxStyle.MoonBeast: return new Vector2(Mathf.Cos(rad) * 340f, Mathf.Sin(rad) * 210f + 30f);
                case UltimateFxStyle.MoonPetals: return new Vector2(Mathf.Cos(rad) * (120f + index * 9f), Mathf.Sin(rad) * (210f + index * 7f));
                case UltimateFxStyle.FrozenMask: return new Vector2((index % 2 == 0 ? -1f : 1f) * (100f + index * 13f), -280f + index % 7 * 95f);
                default: return new Vector2(Mathf.Cos(rad) * (180f + index % 4 * 32f), Mathf.Sin(rad) * (330f + index % 3 * 35f));
            }
        }

        private static float MotifRotation(UltimateFxStyle style, float angle)
        {
            if (style == UltimateFxStyle.PuppetThreads || style == UltimateFxStyle.CosmicAudience) return 0f;
            if (style == UltimateFxStyle.LivingScript) return angle % 18f - 9f;
            return angle - 90f;
        }

        private static Image AddImage(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            return image;
        }

        private static Text AddText(string name, Transform parent, Vector2 min, Vector2 max, string value, Color color, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value; text.color = color; text.fontSize = size; text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            return text;
        }

        private static Color WithAlpha(Color color, float alpha) { color.a = alpha; return color; }
        private static float EaseOut(float value) { value = Mathf.Clamp01(value); return 1f - (1f - value) * (1f - value); }
    }
}
