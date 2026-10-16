#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using ShadowTheater.Data;
using UnityEditor;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    /// <summary>일반 기술의 속성별 캐스트/타격음을 생성하고 모든 비필살기 SkillData에 연결한다.</summary>
    public static class StandardSkillAudioAssetGenerator
    {
        private const string AudioFolder = "Assets/Audio/Generated/Skills";
        private const string SkillFolder = "Assets/Data/Generated/Skills";

        [MenuItem("Tools/Shadow Theater/Release/Generate Standard Skill Audio")]
        public static void Generate()
        {
            EnsureFolders();
            var casts = new Dictionary<ShadowElement, AudioClip>();
            var impacts = new Dictionary<ShadowElement, AudioClip>();
            foreach (ShadowElement element in Enum.GetValues(typeof(ShadowElement)))
            {
                casts[element] = GenerateWave(element, false);
                impacts[element] = GenerateWave(element, true);
            }

            int assigned = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:SkillData", new[] { SkillFolder }))
            {
                var skill = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
                if (skill == null || skill.isUltimate) continue;
                ShadowElement element = casts.ContainsKey(skill.element) ? skill.element : ShadowElement.None;
                skill.sfxClip = ReleaseAssetOverrides.SkillAudio(skill, false, casts[element]);
                skill.impactSfxClip = ReleaseAssetOverrides.SkillAudio(skill, true, impacts[element]);
                skill.sfxPitch = Pitch(skill);
                skill.sfxVolume = skill.target == SkillTarget.Self ? .72f : .82f;
                EditorUtility.SetDirty(skill);
                assigned++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SkillAudio] 속성 4종 WAV 8개, 일반 기술 {assigned}개 연결 완료");
        }

        private static float Pitch(SkillData skill)
        {
            float pitch = skill.damageType == DamageType.Magical ? 1.08f : .96f;
            if (skill.target == SkillTarget.Self) pitch += .12f;
            if (skill.statusEffect != StatusEffectType.None) pitch += .04f;
            return Mathf.Clamp(pitch, .82f, 1.28f);
        }

        private static AudioClip GenerateWave(ShadowElement element, bool impact)
        {
            const int rate = 44100;
            float duration = impact ? .19f : .31f;
            int count = Mathf.CeilToInt(rate * duration);
            var samples = new float[count];
            uint noise = unchecked((uint)((int)element + 1) * 2654435761u + (impact ? 113u : 47u));
            float peak = .001f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                float p = i / (float)(count - 1);
                noise = noise * 1664525u + 1013904223u;
                float grit = ((noise >> 9) / 8388607f * 2f - 1f);
                float frequency = BaseFrequency(element) * (impact
                    ? Mathf.Lerp(1.9f, .58f, p)
                    : Mathf.Lerp(.72f, 1.42f, p));
                float phase = t * frequency * Mathf.PI * 2f;
                float tone = Wave(element, phase, t);
                float attack = Mathf.Clamp01(p * (impact ? 55f : 18f));
                float release = impact ? Mathf.Exp(-p * 11f) : Mathf.Pow(1f - p, 1.7f);
                float value = (tone * .68f + grit * (impact ? .2f : .035f)) * attack * release;
                samples[i] = value;
                peak = Mathf.Max(peak, Mathf.Abs(value));
            }
            float gain = .86f / peak;
            for (int i = 0; i < count; i++) samples[i] = Mathf.Clamp(samples[i] * gain, -.9f, .9f);

            string suffix = impact ? "Impact" : "Cast";
            string path = $"{AudioFolder}/{element}_{suffix}.wav";
            WritePcm16(path, samples, rate);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer != null)
            {
                importer.forceToMono = true;
                importer.loadInBackground = false;
                var settings = new AudioImporterSampleSettings
                {
                    loadType = AudioClipLoadType.DecompressOnLoad,
                    compressionFormat = AudioCompressionFormat.PCM,
                    quality = 1f,
                    sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate,
                    preloadAudioData = true
                };
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static float Wave(ShadowElement element, float phase, float time)
        {
            switch (element)
            {
                case ShadowElement.Flame:
                    return Mathf.Sin(phase) + .28f * Mathf.Sin(phase * .51f);
                case ShadowElement.Frost:
                    return .62f * Mathf.Sin(phase) + .38f * Mathf.Sin(phase * 2.01f);
                case ShadowElement.Shade:
                    return Mathf.Sin(phase + Mathf.Sin(time * 31f) * 1.25f);
                default:
                    return .72f * Mathf.Sin(phase) + .28f * Mathf.Sin(phase * 1.5f);
            }
        }

        private static float BaseFrequency(ShadowElement element)
        {
            switch (element)
            {
                case ShadowElement.Flame: return 196f;
                case ShadowElement.Frost: return 523.25f;
                case ShadowElement.Shade: return 146.83f;
                default: return 261.63f;
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
                foreach (float sample in samples)
                    writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            }
        }

        private static void EnsureFolders()
        {
            Ensure("Assets", "Audio");
            Ensure("Assets/Audio", "Generated");
            Ensure("Assets/Audio/Generated", "Skills");
        }

        private static void Ensure(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
