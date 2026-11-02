using System.Collections;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>생성된 진명 FX 프리팹을 우선 재생하고, 미지정 시 절차적 테마 문양으로 대체하는 UI 연출기.</summary>
    public class BattleFxDirector : MonoBehaviour
    {
        [SerializeField] private RectTransform fxRoot;
        [SerializeField] private RectTransform stageRoot;
        [SerializeField] private BattleSfxPlayer sfxPlayer;
        [SerializeField] private Camera battleCamera;

        private void Awake() => SkillPartLibrary.Preload();

        public IEnumerator Play(BattleUnitPanel attacker, BattleUnitPanel defender, SkillData skill,
                                HitResult hit, float playbackSpeed, System.Action onImpact = null)
        {
            if (attacker == null || skill == null) yield break;
            float speed = Mathf.Max(0.1f, playbackSpeed);
            var profile=SkillPresentationProfile.For(skill);
            if (sfxPlayer == null) sfxPlayer = GetComponent<BattleSfxPlayer>();
            if (battleCamera == null || !battleCamera.isActiveAndEnabled) battleCamera = Camera.main;
            if (sfxPlayer != null) sfxPlayer.PlaySkill(skill);
            if (!skill.isUltimate)
            {
                yield return SignatureAttack(attacker, defender, skill, hit, speed, onImpact);
                yield break;
            }

            RectTransform layer = CreateLayer($"Ultimate_{skill.skillId}");
            Image dim = AddImage("Dim", layer, Vector2.zero, Vector2.one,
                new Color(0.01f, 0.005f, 0.03f, 0f));
            Image flash = AddImage("Flash", layer, Vector2.zero, Vector2.one,
                WithAlpha(skill.primaryFxColor, 0f));
            Text title = AddText("TrueNameCutIn", layer, new Vector2(0.05f, 0.63f), new Vector2(0.95f, 0.79f),
                L10n.Format("battle.true_name_ultimate", "진명 필살기\n{0}", L10n.Text(skill.displayName)), skill.primaryFxColor, 46);

            Vector3 originalScale = attacker.MotionRoot.localScale;
            Vector3 originalPosition = attacker.MotionRoot.localPosition;
            Color originalPortrait = attacker.Portrait != null ? attacker.Portrait.color : Color.black;
            bool painted=profile.choreography.art.IsComplete;
            UltimateFxAssetPlayer assetFx = painted?null:CreateAssetFx(layer, skill);
            if (!painted && assetFx == null) BuildMotif(layer, skill);

            float t = 0f;
            float intro=(profile.IsRecovery? .48f:.4f)/speed;
            Vector2 tableauSource=EffectCenter(layer,attacker.Portrait!=null?attacker.Portrait.rectTransform:attacker.MotionRoot);
            Vector2 tableauTarget=skill.target==SkillTarget.Self || defender==null?tableauSource:
                EffectCenter(layer,defender.Portrait!=null?defender.Portrait.rectTransform:defender.MotionRoot);
            using(var tableau=new SkillSignatureFx(layer,profile,tableauSource,tableauTarget,hit.missed,skill.target==SkillTarget.Self))
            while (t < intro)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / intro);
                tableau.SamplePrelude(p);
                dim.color = new Color(0.01f, 0.005f, 0.03f, Mathf.Lerp(0f, 0.82f, p));
                title.rectTransform.localScale = Vector3.Lerp(new Vector3(1.35f, 0.15f, 1f), Vector3.one, EaseOut(p));
                attacker.MotionRoot.localScale = Vector3.Lerp(originalScale, originalScale * 1.18f, EaseOut(p));
                if (attacker.Portrait != null)
                    attacker.Portrait.color = Color.Lerp(originalPortrait, skill.primaryFxColor, p * 0.7f);
                if (assetFx != null) assetFx.SetProgress(p); else if(!painted) AnimateMotif(layer, p, skill);
                yield return null;
            }

            yield return SignatureAttack(attacker, defender, skill, hit, speed, onImpact);

            t = 0f;
            float burst = 0.22f / speed;
            Vector2 stagePosition = stageRoot != null ? stageRoot.anchoredPosition : Vector2.zero;
            Vector3 cameraPosition = battleCamera != null ? battleCamera.transform.localPosition : Vector3.zero;
            while (t < burst)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / burst);
                flash.color = WithAlpha(skill.secondaryFxColor, !hit.missed ? Mathf.Sin(p * Mathf.PI) * .24f : 0f);
                title.color = Color.Lerp(skill.primaryFxColor, Color.white, Mathf.Sin(p * Mathf.PI));
                if (assetFx != null) assetFx.SetProgress(1f - p * .18f); else if(!painted) AnimateMotif(layer, 1f + p, skill);
                if (stageRoot != null && skill.DealsDamage && !hit.missed)
                    stageRoot.anchoredPosition = stagePosition + Random.insideUnitCircle * (skill.cameraShake * 8f * (1f - p));
                if (battleCamera != null && skill.DealsDamage && !hit.missed)
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

        private IEnumerator SignatureAttack(BattleUnitPanel attacker, BattleUnitPanel defender, SkillData skill,
            HitResult hit, float speed, System.Action onImpact)
        {
            bool impactPlayed = false;
            System.Action present = () =>
            {
                onImpact?.Invoke();
                if (hit.missed && skill.target != SkillTarget.Self) return;
                impactPlayed = true;
                if (sfxPlayer != null) sfxPlayer.PlayImpact(skill);
                if (skill.DealsDamage) GameSettings.TryVibrate();
            };
            using (var attack = new BattleAttackPresentation(CreateLayer("AttackSequence"), attacker, defender, skill, hit.missed, present))
            {
                float duration = SkillPresentationProfile.For(skill).duration / speed;
                for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    attack.Sample(elapsed / duration);
                    if (impactPlayed)
                    {
                        impactPlayed = false;
                        if (skill.DealsDamage) yield return HitStop(skill.hitStopDuration / Mathf.Sqrt(speed));
                    }
                    yield return null;
                }
                attack.Sample(1f);
            }
        }

        public IEnumerator PlayBossPhase(BattleUnitPanel bossPanel, BattleUnit boss,
            BossPhaseDefinition phase, float playbackSpeed)
        {
            float speed = Mathf.Max(.1f, playbackSpeed);
            Color accent = boss?.Data != null ? boss.Data.accentColor : new Color(.8f, .18f, .35f);
            RectTransform layer = CreateLayer("BossPhaseShift");
            Image dim = AddImage("WarningDim", layer, Vector2.zero, Vector2.one, new Color(.02f, 0f, .03f, 0f));
            Image flash = AddImage("PhaseFlash", layer, Vector2.zero, Vector2.one, WithAlpha(accent, 0f));
            Text warning = AddText("Warning", layer, new Vector2(.05f, .72f), new Vector2(.95f, .84f),
                "비극의 제2막", new Color(1f, .32f, .42f), 34);
            Text title = AddText("BossName", layer, new Vector2(.05f, .48f), new Vector2(.95f, .70f),
                boss == null ? "보스" : L10n.Text(boss.Name), Color.white, 54);
            Text phaseTitle = AddText("PhaseName", layer, new Vector2(.08f, .34f), new Vector2(.92f, .48f),
                L10n.Text(phase?.PhaseName ?? "숨겨진 장면"), accent, 38);
            Vector3 stageHome = stageRoot != null ? stageRoot.localPosition : Vector3.zero;
            Vector3 bossHome = bossPanel?.MotionRoot != null ? bossPanel.MotionRoot.localScale : Vector3.one;
            float duration = 1.15f / speed;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float p = Mathf.Clamp01(elapsed / duration);
                float appear = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, p * 3f));
                float vanish = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, (p - .72f) / .28f));
                float alpha = appear * vanish;
                dim.color = new Color(.02f, 0f, .03f, .88f * alpha);
                flash.color = WithAlpha(accent, Mathf.Sin(p * Mathf.PI) * .22f);
                warning.color = WithAlpha(new Color(1f, .32f, .42f), alpha);
                title.color = WithAlpha(Color.white, alpha);
                phaseTitle.color = WithAlpha(accent, alpha);
                if (stageRoot != null)
                    stageRoot.localPosition = stageHome + (Vector3)UnityEngine.Random.insideUnitCircle *
                                              (p < .28f ? 10f : 3f) * alpha;
                if (bossPanel?.MotionRoot != null)
                    bossPanel.MotionRoot.localScale = bossHome * (1f + Mathf.Sin(p * Mathf.PI) * .16f);
                yield return null;
            }
            if (stageRoot != null) stageRoot.localPosition = stageHome;
            if (bossPanel?.MotionRoot != null) bossPanel.MotionRoot.localScale = bossHome;
            Destroy(layer.gameObject);
        }

        public IEnumerator PlayJointSkill(BattleUnitPanel attacker, BattleUnitPanel defender, BattleUnit companion,
            CompanionJointProfile profile, HitResult hit, float playbackSpeed, System.Action onImpact = null)
        {
            float speed=Mathf.Max(.1f,playbackSpeed);Color accent=companion?.Data!=null?companion.Data.accentColor:Color.magenta;
            Color element = ElementChart.ColorFor(profile.element);
            RectTransform layer=CreateLayer("CompanionJointSkill");Image dim=AddImage("Dim",layer,Vector2.zero,Vector2.one,new Color(.01f,.005f,.03f,0));
            Text title=AddText("JointTitle",layer,new Vector2(.06f,.62f),new Vector2(.94f,.79f),profile.displayName,accent,42);
            for(int i=0;i<18;i++){float angle=i*Mathf.PI*2f/18f;Image shard=AddImage("BondShard"+i,layer,new Vector2(.5f,.5f),new Vector2(.5f,.5f),WithAlpha(i%2==0?accent:element,.8f));shard.rectTransform.sizeDelta=new Vector2(10,180);shard.rectTransform.anchoredPosition=new Vector2(Mathf.Cos(angle)*260,Mathf.Sin(angle)*310);shard.rectTransform.localRotation=Quaternion.Euler(0,0,-angle*Mathf.Rad2Deg);}
            Vector3 home=attacker?.MotionRoot!=null?attacker.MotionRoot.localScale:Vector3.one;float duration=.72f/speed;
            for(float t=0;t<duration;t+=Time.unscaledDeltaTime){float p=Mathf.Clamp01(t/duration),pulse=Mathf.Sin(p*Mathf.PI);dim.color=new Color(.01f,.005f,.03f,.72f*pulse);title.color=Color.Lerp(accent,Color.white,pulse);title.rectTransform.localScale=Vector3.one*(.7f+.45f*pulse);for(int i=2;i<layer.childCount;i++)layer.GetChild(i).Rotate(0,0,(i%2==0?1:-1)*2.5f);if(attacker?.MotionRoot!=null)attacker.MotionRoot.localScale=home*(1f+.15f*pulse);yield return null;}
            if(attacker?.MotionRoot!=null)attacker.MotionRoot.localScale=home;
            onImpact?.Invoke();
            Destroy(layer.gameObject);
        }

        private static UltimateFxAssetPlayer CreateAssetFx(RectTransform layer, SkillData skill)
        {
            if (skill.fxPrefab == null) return null;
            GameObject instance = Object.Instantiate(skill.fxPrefab, layer, false);
            instance.name = skill.fxPrefab.name;
            var rect = instance.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            var player = instance.GetComponent<UltimateFxAssetPlayer>();
            if (player == null)
            {
                instance.transform.SetParent(null, false);
                Object.Destroy(instance);
                return null;
            }
            player.Initialize(skill.primaryFxColor, skill.secondaryFxColor, skill.ultimateBurstCount);
            return player;
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

        private void BuildMotif(RectTransform layer, SkillData skill)
        {
            int count = Mathf.Clamp(skill.ultimateBurstCount, 6, 36);
            for (int i = 0; i < count; i++)
            {
                float angle = 360f * i / count;
                Image shard = AddImage($"Shard_{i:00}", layer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    WithAlpha(i % 2 == 0 ? skill.primaryFxColor : skill.secondaryFxColor, 0.85f));
                RectTransform r = shard.rectTransform;
                shard.sprite = BattleFxSpriteLibrary.ForStyle(skill.ultimateFxStyle);
                r.sizeDelta = MotifSize(skill.ultimateFxStyle, i);
                r.anchoredPosition = MotifPosition(skill.ultimateFxStyle, angle, i);
                r.localRotation = Quaternion.Euler(0f, 0f, MotifRotation(skill.ultimateFxStyle, angle));
                shard.raycastTarget = false;
            }
        }

        private static Vector2 EffectCenter(RectTransform layer, RectTransform target)
        {
            if (layer == null || target == null) return Vector2.zero;
            Canvas canvas = layer.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, target.position);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, camera, out Vector2 local)
                ? local : Vector2.zero;
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
