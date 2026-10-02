using System.Collections;
using UnityEngine;

namespace ShadowTheater.Field
{
    public enum FieldAmbienceStyle { Theater, Village, Meadow, EclipseBoss }

    /// <summary>지역별 지속 환경음과 간헐 원샷을 재생하며 씬/전투 전환 때 페이드한다.</summary>
    public class FieldAmbientAudio : MonoBehaviour
    {
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
            EnsureSources();
            bedSource.volume = 0f;
            if (bedSource.clip != null && !bedSource.isPlaying) bedSource.Play();
            BeginFadeIn();
            _detailRoutine = StartCoroutine(DetailRoutine());
        }

        private void OnDisable()
        {
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
            StartFade(ambienceVolume);
        }

        private void StartFade(float target)
        {
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeRoutine(target));
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
                        detailSource.PlayOneShot(clip, detailVolume * Mathf.Clamp01(bedSource.volume / Mathf.Max(.01f, ambienceVolume)));
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
            const int rate = 22050;
            const float duration = 6f;
            int count = Mathf.RoundToInt(rate * duration);
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float p = i / (float)count;
                float value = BedWave(ambienceStyle, p);
                samples[i] = Mathf.Clamp(value * 0.42f, -0.88f, 0.88f);
            }
            var clip = AudioClip.Create($"GeneratedAmbience_{ambienceStyle}", count, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
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
                default:
                    return Sin(147, .46f) + Sin(73, .26f) + Sin(7, .20f) * Sin(151, .32f);
            }
        }

        private static AudioClip GenerateDetail(FieldAmbienceStyle ambienceStyle)
        {
            const int rate = 22050;
            float duration = ambienceStyle == FieldAmbienceStyle.EclipseBoss ? 1.2f : 0.72f;
            int count = Mathf.RoundToInt(rate * duration);
            var samples = new float[count];
            float frequency = DetailFrequency(ambienceStyle);
            uint noise = unchecked((uint)ambienceStyle.GetHashCode() * 2246822519u + 3266489917u);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                float p = t / duration;
                float envelope = Mathf.Sin(Mathf.PI * p) * Mathf.Exp(-p * 2.2f);
                noise = noise * 1664525u + 1013904223u;
                float hiss = ((noise >> 9) / 8388607f * 2f - 1f);
                float tone = Mathf.Sin(Mathf.PI * 2f * frequency * t * (1f + p * .08f));
                float mix = ambienceStyle == FieldAmbienceStyle.Meadow ? tone * .72f + hiss * .10f
                    : ambienceStyle == FieldAmbienceStyle.EclipseBoss ? tone * .58f + hiss * .26f
                    : tone * .82f + hiss * .06f;
                samples[i] = Mathf.Clamp(mix * envelope, -.9f, .9f);
            }
            var clip = AudioClip.Create($"GeneratedDetail_{ambienceStyle}", count, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float DetailFrequency(FieldAmbienceStyle ambienceStyle)
        {
            switch (ambienceStyle)
            {
                case FieldAmbienceStyle.Theater: return 523.25f;
                case FieldAmbienceStyle.Village: return 659.25f;
                case FieldAmbienceStyle.Meadow: return 1046.5f;
                default: return 110f;
            }
        }
    }
}
