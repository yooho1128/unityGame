using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    public enum FootstepSurface
    {
        Wood, Stone, Grass, Ash, Snow, Water, Metal, Void
    }

    /// <summary>타일 이동 완료 시 지형별 발걸음을 재생하고, 에셋이 없으면 짧은 합성음을 사용한다.</summary>
    public class FieldFootstepAudio : MonoBehaviour
    {
        public const int SampleRate = 22050;

        [SerializeField] private PlayerController controller;
        [SerializeField] private AudioSource source;
        [SerializeField] private FootstepSurface surface;
        [SerializeField] private AudioClip[] clips;
        [SerializeField, Range(0f, 1f)] private float volume = .32f;
        [SerializeField, Range(0f, .2f)] private float pitchVariation = .055f;

        private AudioClip[] _generated;
        private int _stepIndex;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
            if (!HasCompleteClipSet())
            {
                _generated = new AudioClip[4];
                for (int i = 0; i < _generated.Length; i++)
                {
                    float[] samples = SynthesizeSamples(surface, i);
                    var clip = AudioClip.Create($"GeneratedStep_{surface}_{i + 1}", samples.Length, 1,
                        SampleRate, false);
                    clip.SetData(samples, 0);
                    _generated[i] = clip;
                }
            }
        }

        private void OnEnable()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            if (controller != null) controller.OnStepFinished += PlayStep;
        }

        private void OnDisable()
        {
            if (controller != null) controller.OnStepFinished -= PlayStep;
        }

        private void OnDestroy()
        {
            if (_generated == null) return;
            foreach (AudioClip clip in _generated) if (clip != null) Destroy(clip);
        }

        private void PlayStep(Vector2Int _)
        {
            AudioClip[] available = HasCompleteClipSet() ? clips : _generated;
            if (source == null || available == null || available.Length == 0) return;
            AudioClip clip = available[_stepIndex++ % available.Length];
            if (clip == null) return;
            float offset = ((_stepIndex * 37) % 101) / 100f * 2f - 1f;
            source.pitch = 1f + offset * pitchVariation;
            source.PlayOneShot(clip, volume * GameSettings.SfxVolume);
        }

        private bool HasCompleteClipSet()
        {
            if (clips == null || clips.Length == 0) return false;
            foreach (AudioClip clip in clips) if (clip == null) return false;
            return true;
        }

        public static float[] SynthesizeSamples(FootstepSurface surface, int variant)
        {
            float duration = surface == FootstepSurface.Water ? .16f : .105f + variant * .006f;
            int count = Mathf.RoundToInt(SampleRate * duration);
            var samples = new float[count];
            uint noise = unchecked((uint)surface * 2654435761u + (uint)(variant + 1) * 2246822519u);
            float filtered = 0f;
            float frequency = BodyFrequency(surface) * (1f + variant * .035f);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float p = i / (float)(count - 1);
                noise = noise * 1664525u + 1013904223u;
                float raw = (noise >> 9) / 8388607f * 2f - 1f;
                float smoothing = surface == FootstepSurface.Grass || surface == FootstepSurface.Ash ? .16f : .38f;
                filtered = Mathf.Lerp(filtered, raw, smoothing);
                float body = Mathf.Sin(Mathf.PI * 2f * frequency * t * Mathf.Lerp(1.15f, .74f, p));
                float texture = TextureMix(surface, raw, filtered, body);
                float attack = Mathf.Clamp01(p * 28f);
                float release = Mathf.Pow(1f - p, surface == FootstepSurface.Water ? 2.2f : 4.2f);
                samples[i] = Mathf.Clamp(texture * attack * release * .72f, -.9f, .9f);
            }
            return samples;
        }

        private static float TextureMix(FootstepSurface surface, float raw, float filtered, float body)
        {
            switch (surface)
            {
                case FootstepSurface.Wood: return body * .7f + filtered * .3f;
                case FootstepSurface.Stone: return body * .48f + raw * .52f;
                case FootstepSurface.Grass: return filtered * .72f + raw * .18f + body * .1f;
                case FootstepSurface.Ash: return filtered * .82f + body * .18f;
                case FootstepSurface.Snow: return filtered * .62f + raw * .3f + body * .08f;
                case FootstepSurface.Water: return filtered * .45f + Mathf.Sin(body * 2.2f) * .55f;
                case FootstepSurface.Metal: return body * .75f + raw * .25f;
                default: return body * .58f + filtered * .42f;
            }
        }

        private static float BodyFrequency(FootstepSurface surface)
        {
            switch (surface)
            {
                case FootstepSurface.Wood: return 132f;
                case FootstepSurface.Stone: return 92f;
                case FootstepSurface.Grass: return 68f;
                case FootstepSurface.Ash: return 54f;
                case FootstepSurface.Snow: return 76f;
                case FootstepSurface.Water: return 118f;
                case FootstepSurface.Metal: return 246f;
                default: return 44f;
            }
        }
    }
}
