#if UNITY_EDITOR
using System;
using System.IO;
using ShadowTheater.Field;
using UnityEditor;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    /// <summary>지역 환경음과 지형별 발걸음을 WAV로 생성해 씬 생성기가 재사용하도록 한다.</summary>
    public static class FieldAudioAssetGenerator
    {
        private const string Root = "Assets/Audio/Generated/Field";
        private const string AmbienceFolder = Root + "/Ambience";
        private const string FootstepFolder = Root + "/Footsteps";

        [MenuItem("Tools/Shadow Theater/Generate Field Audio Assets")]
        public static void Generate()
        {
            EnsureFolders();
            int ambienceCount = 0;
            foreach (FieldAmbienceStyle style in Enum.GetValues(typeof(FieldAmbienceStyle)))
            {
                WriteAndImport(BedPath(style), FieldAmbientAudio.SynthesizeBedSamples(style),
                    FieldAmbientAudio.SampleRate, true);
                WriteAndImport(DetailPath(style), FieldAmbientAudio.SynthesizeDetailSamples(style),
                    FieldAmbientAudio.SampleRate, false);
                ambienceCount += 2;
            }

            int footstepCount = 0;
            foreach (FootstepSurface surface in Enum.GetValues(typeof(FootstepSurface)))
            for (int variant = 0; variant < 4; variant++)
            {
                WriteAndImport(FootstepPath(surface, variant),
                    FieldFootstepAudio.SynthesizeSamples(surface, variant), FieldFootstepAudio.SampleRate, false);
                footstepCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FieldAudio] 환경음 {ambienceCount}개, 발걸음 {footstepCount}개 생성 완료");
        }

        public static AudioClip LoadBed(FieldAmbienceStyle style) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>(BedPath(style));

        public static AudioClip LoadDetail(FieldAmbienceStyle style) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>(DetailPath(style));

        public static AudioClip[] LoadFootsteps(FootstepSurface surface)
        {
            var result = new AudioClip[4];
            for (int i = 0; i < result.Length; i++)
                result[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(FootstepPath(surface, i));
            return result;
        }

        private static string BedPath(FieldAmbienceStyle style) => $"{AmbienceFolder}/{style}_Bed.wav";
        private static string DetailPath(FieldAmbienceStyle style) => $"{AmbienceFolder}/{style}_Detail.wav";
        private static string FootstepPath(FootstepSurface surface, int variant) =>
            $"{FootstepFolder}/{surface}_{variant + 1:00}.wav";

        private static void WriteAndImport(string path, float[] samples, int sampleRate, bool streaming)
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

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;
            importer.forceToMono = true;
            importer.loadInBackground = streaming;
            importer.preloadAudioData = !streaming;
            importer.defaultSampleSettings = new AudioImporterSampleSettings
            {
                loadType = streaming ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory,
                compressionFormat = AudioCompressionFormat.Vorbis,
                quality = streaming ? .62f : .76f,
                sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate
            };
            importer.SaveAndReimport();
        }

        private static void EnsureFolders()
        {
            Ensure("Assets", "Audio");
            Ensure("Assets/Audio", "Generated");
            Ensure("Assets/Audio/Generated", "Field");
            Ensure(Root, "Ambience");
            Ensure(Root, "Footsteps");
        }

        private static void Ensure(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
