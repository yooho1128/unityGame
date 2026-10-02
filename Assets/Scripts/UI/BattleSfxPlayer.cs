using System.Collections.Generic;
using ShadowTheater.Data;
using UnityEngine;

namespace ShadowTheater.UI
{
    /// <summary>실제 SFX가 없을 때 진명 테마별 합성음을 생성하는 전투 오디오 플레이어.</summary>
    public class BattleSfxPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource skillSource;
        [SerializeField] private AudioSource impactSource;
        private readonly Dictionary<string, AudioClip> _generated = new Dictionary<string, AudioClip>();

        public void PlaySkill(SkillData skill)
        {
            if (skill == null) return;
            AudioSource source = EnsureSource(ref skillSource, "SkillAudio");
            source.pitch = skill.sfxPitch;
            source.PlayOneShot(skill.sfxClip != null ? skill.sfxClip : Clip(skill.ultimateFxStyle, false), skill.sfxVolume);
        }

        public void PlayImpact(SkillData skill)
        {
            if (skill == null) return;
            AudioSource source = EnsureSource(ref impactSource, "ImpactAudio");
            source.pitch = Mathf.Lerp(skill.sfxPitch, 0.85f, 0.35f);
            source.PlayOneShot(Clip(skill.ultimateFxStyle, true), skill.sfxVolume);
        }

        private AudioClip Clip(UltimateFxStyle style, bool impact)
        {
            string key = $"{style}_{(impact ? "Impact" : "Cast")}";
            if (_generated.TryGetValue(key, out AudioClip cached)) return cached;
            const int rate = 44100;
            float duration = impact ? 0.18f : 0.42f;
            int count = Mathf.CeilToInt(rate * duration);
            var samples = new float[count];
            float baseFrequency = BaseFrequency(style);
            uint noise = unchecked((uint)style.GetHashCode() * 747796405u + (impact ? 2891336453u : 277803737u));
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                float p = t / duration;
                float envelope = impact ? Mathf.Exp(-p * 9f) : Mathf.Sin(Mathf.PI * p) * Mathf.Exp(-p * 1.5f);
                float frequency = baseFrequency * (impact ? Mathf.Lerp(1.7f, 0.55f, p) : Mathf.Lerp(0.75f, 1.65f, p));
                float wave = ThemeWave(style, t, frequency, p);
                noise = noise * 1664525u + 1013904223u;
                float grit = ((noise >> 9) / 8388607f * 2f - 1f) * (impact ? 0.32f : 0.06f);
                samples[i] = Mathf.Clamp((wave * 0.72f + grit) * envelope, -0.95f, 0.95f);
            }
            var clip = AudioClip.Create($"Generated_{key}", count, 1, rate, false);
            clip.SetData(samples, 0);
            _generated[key] = clip;
            return clip;
        }

        private static float ThemeWave(UltimateFxStyle style, float time, float frequency, float progress)
        {
            float phase = time * frequency * Mathf.PI * 2f;
            switch (style)
            {
                case UltimateFxStyle.FlameCrown:
                    return Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 0.5f);
                case UltimateFxStyle.FrozenArchive:
                    return Mathf.Sin(phase) * 0.65f + Mathf.Sin(phase * 2.01f) * 0.35f;
                case UltimateFxStyle.MoonBeast:
                    return Mathf.Sin(phase + Mathf.Sin(time * 24f) * 1.4f);
                case UltimateFxStyle.PuppetThreads:
                    return Mathf.Sign(Mathf.Sin(phase)) * (1f - progress) + Mathf.Sin(phase * 2f) * progress;
                case UltimateFxStyle.AshBird:
                    return Mathf.Sin(phase + progress * progress * 10f) + 0.25f * Mathf.Sin(phase * 3f);
                case UltimateFxStyle.FrozenMask:
                    return (Mathf.Sin(phase) + Mathf.Sin(phase * 1.012f)) * 0.5f;
                case UltimateFxStyle.MoonPetals:
                    return Mathf.Sin(phase) * 0.7f + Mathf.Sin(phase * 1.5f) * 0.3f;
                case UltimateFxStyle.LivingScript:
                    return Mathf.Sin(phase) * (0.6f + 0.4f * Mathf.Sign(Mathf.Sin(time * 37f)));
                case UltimateFxStyle.CosmicAudience:
                    return Mathf.Sin(phase) * 0.55f + Mathf.Sin(phase * 0.503f) * 0.45f;
                default:
                    return Mathf.Sin(phase);
            }
        }

        private static float BaseFrequency(UltimateFxStyle style)
        {
            switch (style)
            {
                case UltimateFxStyle.FlameCrown: return 196f;
                case UltimateFxStyle.FrozenArchive: return 659.25f;
                case UltimateFxStyle.MoonBeast: return 146.83f;
                case UltimateFxStyle.PuppetThreads: return 440f;
                case UltimateFxStyle.AshBird: return 293.66f;
                case UltimateFxStyle.FrozenMask: return 523.25f;
                case UltimateFxStyle.MoonPetals: return 783.99f;
                case UltimateFxStyle.LivingScript: return 246.94f;
                case UltimateFxStyle.CosmicAudience: return 98f;
                default: return 220f;
            }
        }

        private AudioSource EnsureSource(ref AudioSource source, string sourceName)
        {
            if (source != null) return source;
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            return source;
        }

        private void OnDestroy()
        {
            foreach (AudioClip clip in _generated.Values)
                if (clip != null) Destroy(clip);
            _generated.Clear();
        }
    }
}
