#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using ShadowTheater.Data;
using ShadowTheater.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.EditorTools
{
    /// <summary>9개 진명 테마의 Canvas 파티클 프리팹과 캐스트/타격 WAV를 생성해 SkillData에 연결한다.</summary>
    public static class UltimateFxAssetGenerator
    {
        private const string FxFolder = "Assets/Prefabs/FX/Ultimate";
        private const string AudioFolder = "Assets/Audio/Generated/Ultimate";
        private const string SkillFolder = "Assets/Data/Generated/Skills";
        private const int ParticleCount = 48;

        [MenuItem("Tools/Shadow Theater/Generate Ultimate FX and SFX")]
        public static void Generate()
        {
            EnsureFolders();
            var prefabs = new Dictionary<UltimateFxStyle, GameObject>();
            var casts = new Dictionary<UltimateFxStyle, AudioClip>();
            var impacts = new Dictionary<UltimateFxStyle, AudioClip>();
            foreach (UltimateFxStyle style in Enum.GetValues(typeof(UltimateFxStyle)))
            {
                if (style == UltimateFxStyle.None) continue;
                prefabs[style] = GeneratePrefab(style);
                casts[style] = GenerateWave(style, false);
                impacts[style] = GenerateWave(style, true);
            }

            int assigned = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:SkillData", new[] { SkillFolder }))
            {
                var skill = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
                if (skill == null || !skill.isUltimate || skill.ultimateFxStyle == UltimateFxStyle.None) continue;
                skill.fxPrefab = prefabs[skill.ultimateFxStyle];
                skill.sfxClip = casts[skill.ultimateFxStyle];
                skill.impactSfxClip = impacts[skill.ultimateFxStyle];
                EditorUtility.SetDirty(skill);
                assigned++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[UltimateAssets] 테마 {prefabs.Count}종, WAV {casts.Count + impacts.Count}개, 필살기 {assigned}개 연결 완료");
        }

        private static GameObject GeneratePrefab(UltimateFxStyle style)
        {
            var root = new GameObject(style + "Fx", typeof(RectTransform), typeof(UltimateFxAssetPlayer));
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero; rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero; rootRect.offsetMax = Vector2.zero;
            var particles = new RectTransform[ParticleCount];
            for (int i = 0; i < particles.Length; i++)
            {
                var child = new GameObject($"Particle_{i:00}", typeof(RectTransform), typeof(Image));
                child.transform.SetParent(root.transform, false);
                var rect = (RectTransform)child.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.sizeDelta = Size(style, i);
                rect.anchoredPosition = Position(style, i);
                rect.localRotation = Quaternion.Euler(0f, 0f, Rotation(style, i));
                var image = child.GetComponent<Image>();
                image.color = Color.white;
                image.raycastTarget = false;
                particles[i] = rect;
            }
            root.GetComponent<UltimateFxAssetPlayer>().Setup(style, particles);
            string path = $"{FxFolder}/{style}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static AudioClip GenerateWave(UltimateFxStyle style, bool impact)
        {
            const int rate = 44100;
            float duration = impact ? .28f : .72f;
            int sampleCount = Mathf.CeilToInt(rate * duration);
            var samples = new float[sampleCount];
            uint noise = unchecked((uint)style * 2654435761u + (impact ? 17u : 71u));
            float peak = .001f;
            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)rate;
                float p = i / (float)(sampleCount - 1);
                noise = noise * 1664525u + 1013904223u;
                float grit = ((noise >> 9) / 8388607f * 2f - 1f);
                float value = Sample(style, impact, time, p, grit);
                samples[i] = value;
                peak = Mathf.Max(peak, Mathf.Abs(value));
            }
            float gain = .88f / peak;
            for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Clamp(samples[i] * gain, -.92f, .92f);

            string suffix = impact ? "Impact" : "Cast";
            string path = $"{AudioFolder}/{style}_{suffix}.wav";
            WritePcm16(path, samples, rate);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer != null)
            {
                importer.forceToMono = true;
                importer.loadInBackground = false;
                var sampleSettings = new AudioImporterSampleSettings
                {
                    loadType = AudioClipLoadType.DecompressOnLoad,
                    compressionFormat = AudioCompressionFormat.PCM,
                    quality = 1f,
                    sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate
                };
#if UNITY_6000_0_OR_NEWER
                sampleSettings.preloadAudioData = true;
#else
                importer.preloadAudioData = true;
#endif
                importer.defaultSampleSettings = sampleSettings;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static float Sample(UltimateFxStyle style, bool impact, float t, float p, float noise)
        {
            float baseHz = BaseFrequency(style);
            float sweep = impact ? Mathf.Lerp(2.1f, .48f, p) : Mathf.Lerp(.62f, 1.85f, p);
            float phase = t * baseHz * sweep * Mathf.PI * 2f;
            float attack = Mathf.Clamp01(p * (impact ? 45f : 10f));
            float release = impact ? Mathf.Exp(-p * 10f) : Mathf.Pow(1f - p, 1.4f);
            float tonal;
            switch (style)
            {
                case UltimateFxStyle.FlameCrown: tonal = Mathf.Sin(phase) + .34f * Mathf.Sin(phase * .51f); break;
                case UltimateFxStyle.FrozenArchive: tonal = .55f * Mathf.Sin(phase) + .45f * Mathf.Sin(phase * 2.013f); break;
                case UltimateFxStyle.MoonBeast: tonal = Mathf.Sin(phase + Mathf.Sin(t * 29f) * 1.7f); break;
                case UltimateFxStyle.PuppetThreads: tonal = .58f * Mathf.Sign(Mathf.Sin(phase)) + .42f * Mathf.Sin(phase * 2f); break;
                case UltimateFxStyle.AshBird: tonal = Mathf.Sin(phase + p * p * 13f) + .28f * Mathf.Sin(phase * 3f); break;
                case UltimateFxStyle.FrozenMask: tonal = .5f * (Mathf.Sin(phase) + Mathf.Sin(phase * 1.009f)); break;
                case UltimateFxStyle.MoonPetals: tonal = .68f * Mathf.Sin(phase) + .32f * Mathf.Sin(phase * 1.5f); break;
                case UltimateFxStyle.LivingScript: tonal = Mathf.Sin(phase) * (.65f + .35f * Mathf.Sign(Mathf.Sin(t * 41f))); break;
                case UltimateFxStyle.CosmicAudience: tonal = .5f * Mathf.Sin(phase) + .3f * Mathf.Sin(phase * .502f) + .2f * Mathf.Sin(phase * 2.99f); break;
                default: tonal = Mathf.Sin(phase); break;
            }
            float gritAmount = impact ? .28f : style == UltimateFxStyle.AshBird ? .12f : .045f;
            return (tonal * .7f + noise * gritAmount) * attack * release;
        }

        private static Vector2 Position(UltimateFxStyle style, int i)
        {
            float a = i / (float)ParticleCount * Mathf.PI * 2f;
            switch (style)
            {
                case UltimateFxStyle.PuppetThreads: return new Vector2(-500f + i * 1000f / (ParticleCount - 1), 20f);
                case UltimateFxStyle.LivingScript: return new Vector2(Mathf.Sin(i * 1.7f) * 250f, -350f + i % 16 * 47f);
                case UltimateFxStyle.CosmicAudience: return new Vector2(-420f + i % 8 * 120f, -290f + i / 8 * 115f);
                case UltimateFxStyle.AshBird: return new Vector2(Mathf.Cos(a) * 330f, Mathf.Abs(Mathf.Sin(a)) * 330f - 100f);
                case UltimateFxStyle.FlameCrown: return new Vector2(Mathf.Cos(a) * 350f, Mathf.Abs(Mathf.Sin(a)) * 260f - 270f);
                case UltimateFxStyle.FrozenMask: return new Vector2((i % 2 == 0 ? -1f : 1f) * (90f + i % 12 * 23f), -280f + i % 8 * 82f);
                case UltimateFxStyle.MoonBeast: return new Vector2(Mathf.Cos(a) * 355f, Mathf.Sin(a) * 225f + 25f);
                default: return new Vector2(Mathf.Cos(a) * (150f + i % 5 * 34f), Mathf.Sin(a) * (260f + i % 4 * 31f));
            }
        }

        private static Vector2 Size(UltimateFxStyle style, int i)
        {
            switch (style)
            {
                case UltimateFxStyle.PuppetThreads: return new Vector2(5f, 680f);
                case UltimateFxStyle.LivingScript: return new Vector2(470f, 7f + i % 4 * 4f);
                case UltimateFxStyle.CosmicAudience: return new Vector2(36f + i % 3 * 14f, 13f + i % 2 * 8f);
                case UltimateFxStyle.FrozenArchive:
                case UltimateFxStyle.FrozenMask: return new Vector2(14f + i % 3 * 6f, 120f + i % 5 * 27f);
                case UltimateFxStyle.MoonPetals: return new Vector2(18f + i % 3 * 8f, 48f + i % 4 * 13f);
                default: return new Vector2(10f + i % 4 * 8f, 170f + i % 5 * 29f);
            }
        }

        private static float Rotation(UltimateFxStyle style, int i)
        {
            if (style == UltimateFxStyle.PuppetThreads || style == UltimateFxStyle.CosmicAudience) return 0f;
            if (style == UltimateFxStyle.LivingScript) return i % 5 * 4f - 8f;
            return i / (float)ParticleCount * 360f - 90f;
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

        private static void WritePcm16(string path, float[] samples, int sampleRate)
        {
            using (var writer = new BinaryWriter(File.Open(path, FileMode.Create)))
            {
                int dataSize = samples.Length * 2;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + dataSize);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE")); writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate);
                writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(dataSize);
                foreach (float sample in samples) writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            }
        }

        private static void EnsureFolders()
        {
            Ensure("Assets", "Prefabs"); Ensure("Assets/Prefabs", "FX"); Ensure("Assets/Prefabs/FX", "Ultimate");
            Ensure("Assets", "Audio"); Ensure("Assets/Audio", "Generated"); Ensure("Assets/Audio/Generated", "Ultimate");
        }

        private static void Ensure(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
