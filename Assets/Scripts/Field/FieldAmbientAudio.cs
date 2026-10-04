using System.Collections;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    public enum FieldAmbienceStyle
    {
        Theater, Village, Meadow, EclipseBoss,
        AshWastes, EmberCity, Catacombs, AshThrone,
        FrostPort, Archive, ForbiddenStacks, MirrorVault, BlueAbyss,
        VioletMarsh, HowlVillage, MoonfangForest, BloodmoonRidge, BeastDen,
        ThreadMarket, ClockworkAlley, MarionetteOpera, SeveredWorkshop, PuppeteerStage,
        Censor, BlackArchive, MemorySea, MoonPalace, FinalTheater, CosmicStage
    }

    /// <summary>지역별 지속 환경음과 간헐 원샷을 재생하며 씬/전투 전환 때 페이드한다.</summary>
    public class FieldAmbientAudio : MonoBehaviour
    {
        public const int SampleRate = 22050;
        public static FieldAmbientAudio Instance { get; private set; }

        [SerializeField] private FieldAmbienceStyle style;
        [SerializeField] private AudioSource bedSource;
        [SerializeField] private AudioSource detailSource;
        [SerializeField] private AudioClip ambienceClip;
        [SerializeField] private AudioClip detailClip;
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.32f;
        [SerializeField, Range(0f, 1f)] private float detailVolume = 0.28f;
        [SerializeField, Min(0.05f)] private float fadeDuration = 0.4f;
        [SerializeField] private Vector2 detailInterval = new Vector2(4.5f, 9f);

        private AudioClip _generatedBed;
        private AudioClip _generatedDetail;
        private Coroutine _fadeRoutine;
        private Coroutine _detailRoutine;
        private float _fadeTarget;

        private float EffectiveAmbienceVolume => ambienceVolume * GameSettings.AmbienceVolume;

        private void Awake()
        {
            EnsureSources();
            if (ambienceClip == null) _generatedBed = GenerateBed(style);
            if (detailClip == null) _generatedDetail = GenerateDetail(style);
            bedSource.clip = ambienceClip != null ? ambienceClip : _generatedBed;
            bedSource.loop = true;
            detailSource.loop = false;
        }

        private void OnEnable()
        {
            Instance = this;
            GameSettings.Changed += HandleSettingsChanged;
            EnsureSources();
            bedSource.volume = 0f;
            if (bedSource.clip != null && !bedSource.isPlaying) bedSource.Play();
            BeginFadeIn();
            _detailRoutine = StartCoroutine(DetailRoutine());
        }

        private void OnDisable()
        {
            GameSettings.Changed -= HandleSettingsChanged;
            if (Instance == this) Instance = null;
            StopAllCoroutines();
            _fadeRoutine = null;
            _detailRoutine = null;
            if (bedSource != null) bedSource.Stop();
            if (detailSource != null) detailSource.Stop();
        }

        private void OnDestroy()
        {
            if (_generatedBed != null) Destroy(_generatedBed);
            if (_generatedDetail != null) Destroy(_generatedDetail);
        }

        public void BeginFadeOut()
        {
            if (!isActiveAndEnabled) return;
            StartFade(0f);
        }

        public void BeginFadeIn()
        {
            if (!isActiveAndEnabled) return;
            StartFade(EffectiveAmbienceVolume);
        }

        private void StartFade(float target)
        {
            _fadeTarget = target;
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeRoutine(target));
        }

        private void HandleSettingsChanged()
        {
            if (isActiveAndEnabled && _fadeTarget > 0f) StartFade(EffectiveAmbienceVolume);
        }

        private IEnumerator FadeRoutine(float target)
        {
            float start = bedSource != null ? bedSource.volume : 0f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / fadeDuration);
                if (bedSource != null) bedSource.volume = Mathf.Lerp(start, target, p * p * (3f - 2f * p));
                yield return null;
            }
            if (bedSource != null) bedSource.volume = target;
            _fadeRoutine = null;
        }

        private IEnumerator DetailRoutine()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(Random.Range(detailInterval.x, detailInterval.y));
                if (detailSource != null && bedSource != null && bedSource.volume > 0.03f)
                {
                    AudioClip clip = detailClip != null ? detailClip : _generatedDetail;
                    if (clip != null)
                    {
                        detailSource.pitch = Random.Range(0.94f, 1.07f);
                        float categoryVolume = GameSettings.AmbienceVolume;
                        float fadeRatio = Mathf.Clamp01(bedSource.volume / Mathf.Max(.01f, EffectiveAmbienceVolume));
                        detailSource.PlayOneShot(clip, detailVolume * categoryVolume * fadeRatio);
                    }
                }
            }
        }

        private void EnsureSources()
        {
            if (bedSource == null) bedSource = CreateSource("AmbienceBed");
            if (detailSource == null) detailSource = CreateSource("AmbienceDetail");
        }

        private AudioSource CreateSource(string sourceName)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = false;
            return source;
        }

        private static AudioClip GenerateBed(FieldAmbienceStyle ambienceStyle)
        {
            float[] samples = SynthesizeBedSamples(ambienceStyle);
            var clip = AudioClip.Create($"GeneratedAmbience_{ambienceStyle}", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static float[] SynthesizeBedSamples(FieldAmbienceStyle ambienceStyle)
        {
            int count = Mathf.RoundToInt(SampleRate * 6f);
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float phase = i / (float)count;
                samples[i] = Mathf.Clamp(BedWave(ambienceStyle, phase) * 0.42f, -0.88f, 0.88f);
            }
            return samples;
        }

        private static float BedWave(FieldAmbienceStyle ambienceStyle, float phase)
        {
            float Sin(int cycles, float amplitude) => Mathf.Sin(Mathf.PI * 2f * cycles * phase) * amplitude;
            switch (ambienceStyle)
            {
                case FieldAmbienceStyle.Theater:
                    return Sin(330, .42f) + Sin(495, .18f) + Sin(13, .18f) * Sin(333, .35f);
                case FieldAmbienceStyle.Village:
                    return Sin(17, .24f) + Sin(29, .16f) + Sin(880, .06f) * (0.5f + Sin(3, .5f));
                case FieldAmbienceStyle.Meadow:
                    return Sin(11, .28f) + Sin(23, .16f) + Sin(1320, .045f) * (0.5f + Sin(5, .5f));
                case FieldAmbienceStyle.AshWastes:
                    return Sin(9, .30f) + Sin(41, .13f) + Sin(730, .04f) * (0.5f + Sin(4, .5f));
                case FieldAmbienceStyle.EmberCity:
                    return Sin(21, .25f) + Sin(63, .12f) + Sin(980, .055f) * (0.5f + Sin(7, .5f));
                case FieldAmbienceStyle.Catacombs:
                    return Sin(8, .34f) + Sin(55, .10f) + Sin(233, .08f) * Sin(3, .5f);
                case FieldAmbienceStyle.AshThrone:
                    return Sin(98, .40f) + Sin(49, .24f) + Sin(5, .22f) * Sin(101, .32f);
                case FieldAmbienceStyle.FrostPort:
                    return Sin(14, .27f) + Sin(37, .13f) + Sin(1175, .045f) * (0.5f + Sin(4, .5f));
                case FieldAmbienceStyle.Archive:
                    return Sin(31, .20f) + Sin(62, .11f) + Sin(1397, .035f) * (0.5f + Sin(6, .5f));
                case FieldAmbienceStyle.ForbiddenStacks:
                    return Sin(10, .32f) + Sin(47, .13f) + Sin(185, .09f) * Sin(4, .5f);
                case FieldAmbienceStyle.MirrorVault:
                    return Sin(73, .21f) + Sin(109, .13f) + Sin(877, .045f) * Sin(5, .5f);
                case FieldAmbienceStyle.BlueAbyss:
                    return Sin(55, .38f) + Sin(27, .22f) + Sin(6, .20f) * Sin(110, .32f);
                case FieldAmbienceStyle.VioletMarsh:
                case FieldAmbienceStyle.HowlVillage:
                    return Sin(12, .29f) + Sin(43, .15f) + Sin(620, .04f) * Sin(5, .5f);
                case FieldAmbienceStyle.MoonfangForest:
                    return Sin(16, .25f) + Sin(31, .15f) + Sin(930, .04f) * (0.5f + Sin(5, .5f));
                case FieldAmbienceStyle.BloodmoonRidge:
                case FieldAmbienceStyle.BeastDen:
                    return Sin(69, .38f) + Sin(34, .23f) + Sin(7, .21f) * Sin(138, .30f);
                case FieldAmbienceStyle.ThreadMarket:
                case FieldAmbienceStyle.ClockworkAlley:
                    return Sin(24, .23f) + Sin(73, .13f) + Sin(1160, .04f) * (0.5f + Sin(8, .5f));
                case FieldAmbienceStyle.MarionetteOpera:
                case FieldAmbienceStyle.PuppeteerStage:
                    return Sin(87, .36f) + Sin(43, .22f) + Sin(6, .2f) * Sin(174, .3f);
                case FieldAmbienceStyle.SeveredWorkshop:
                    return Sin(19, .27f) + Sin(59, .14f) + Sin(510, .05f) * Sin(5, .5f);
                case FieldAmbienceStyle.Censor:
                    return Sin(8, .31f) + Sin(37, .13f) + Sin(180, .06f) * Sin(4, .5f);
                case FieldAmbienceStyle.BlackArchive:
                    return Sin(49, .4f) + Sin(24, .25f) + Sin(5, .2f) * Sin(98, .3f);
                case FieldAmbienceStyle.MemorySea:
                    return Sin(13,.28f)+Sin(29,.16f)+Sin(1200,.035f)*(0.5f+Sin(4,.5f));
                case FieldAmbienceStyle.MoonPalace:
                    return Sin(66,.38f)+Sin(33,.22f)+Sin(6,.18f)*Sin(132,.3f);
                case FieldAmbienceStyle.FinalTheater:
                    return Sin(44,.3f)+Sin(88,.18f)+Sin(7,.2f)*Sin(352,.12f);
                case FieldAmbienceStyle.CosmicStage:
                    return Sin(33,.34f)+Sin(66,.2f)+Sin(5,.2f)*Sin(990,.08f);
                default:
                    return Sin(147, .46f) + Sin(73, .26f) + Sin(7, .20f) * Sin(151, .32f);
            }
        }

        private static AudioClip GenerateDetail(FieldAmbienceStyle ambienceStyle)
        {
            float[] samples = SynthesizeDetailSamples(ambienceStyle);
            var clip = AudioClip.Create($"GeneratedDetail_{ambienceStyle}", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static float[] SynthesizeDetailSamples(FieldAmbienceStyle ambienceStyle)
        {
            float duration = ambienceStyle == FieldAmbienceStyle.EclipseBoss ||
                             ambienceStyle == FieldAmbienceStyle.AshThrone ? 1.2f : 0.72f;
            int count = Mathf.RoundToInt(SampleRate * duration);
            var samples = new float[count];
            float frequency = DetailFrequency(ambienceStyle);
            uint noise = unchecked((uint)ambienceStyle.GetHashCode() * 2246822519u + 3266489917u);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float p = t / duration;
                float envelope = Mathf.Sin(Mathf.PI * p) * Mathf.Exp(-p * 2.2f);
                noise = noise * 1664525u + 1013904223u;
                float hiss = ((noise >> 9) / 8388607f * 2f - 1f);
                float tone = Mathf.Sin(Mathf.PI * 2f * frequency * t * (1f + p * .08f));
                float mix = ambienceStyle == FieldAmbienceStyle.Meadow ? tone * .72f + hiss * .10f
                    : ambienceStyle == FieldAmbienceStyle.EclipseBoss || ambienceStyle == FieldAmbienceStyle.AshThrone
                        ? tone * .58f + hiss * .26f
                    : tone * .82f + hiss * .06f;
                samples[i] = Mathf.Clamp(mix * envelope, -.9f, .9f);
            }
            return samples;
        }

        private static float DetailFrequency(FieldAmbienceStyle ambienceStyle)
        {
            switch (ambienceStyle)
            {
                case FieldAmbienceStyle.Theater: return 523.25f;
                case FieldAmbienceStyle.Village: return 659.25f;
                case FieldAmbienceStyle.Meadow: return 1046.5f;
                case FieldAmbienceStyle.AshWastes: return 196f;
                case FieldAmbienceStyle.EmberCity: return 783.99f;
                case FieldAmbienceStyle.Catacombs: return 146.83f;
                case FieldAmbienceStyle.AshThrone: return 82.41f;
                case FieldAmbienceStyle.FrostPort: return 880f;
                case FieldAmbienceStyle.Archive: return 1174.66f;
                case FieldAmbienceStyle.ForbiddenStacks: return 164.81f;
                case FieldAmbienceStyle.MirrorVault: return 698.46f;
                case FieldAmbienceStyle.BlueAbyss: return 73.42f;
                case FieldAmbienceStyle.VioletMarsh: return 220f;
                case FieldAmbienceStyle.HowlVillage: return 293.66f;
                case FieldAmbienceStyle.MoonfangForest: return 783.99f;
                case FieldAmbienceStyle.BloodmoonRidge: return 98f;
                case FieldAmbienceStyle.BeastDen: return 65.41f;
                case FieldAmbienceStyle.ThreadMarket: return 659.25f;
                case FieldAmbienceStyle.ClockworkAlley: return 523.25f;
                case FieldAmbienceStyle.MarionetteOpera: return 246.94f;
                case FieldAmbienceStyle.SeveredWorkshop: return 174.61f;
                case FieldAmbienceStyle.PuppeteerStage: return 82.41f;
                case FieldAmbienceStyle.Censor: return 130.81f;
                case FieldAmbienceStyle.BlackArchive: return 61.74f;
                case FieldAmbienceStyle.MemorySea: return 987.77f;
                case FieldAmbienceStyle.MoonPalace: return 110f;
                default: return 110f;
            }
        }
    }
}
