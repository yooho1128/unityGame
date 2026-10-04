#if UNITY_EDITOR
using System;
using System.IO;
using ShadowTheater.UI;
using UnityEditor;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    /// <summary>타이틀·막·전투·엔딩용 무봉제 음악 WAV를 생성한다.</summary>
    public static class MusicAssetGenerator
    {
        private const string Folder = "Assets/Audio/Generated/Music";

        [MenuItem("Tools/Shadow Theater/Generate Adaptive Music")]
        public static void Generate()
        {
            EnsureFolders();
            int count = 0;
            foreach (MusicCue cue in Enum.GetValues(typeof(MusicCue)))
            {
                string path = PathFor(cue);
                WritePcm16(path, AdaptiveMusicDirector.SynthesizeSamples(cue), AdaptiveMusicDirector.SampleRate);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer != null)
                {
                    importer.forceToMono = true;
                    importer.loadInBackground = true;
                    var settings = new AudioImporterSampleSettings
                    {
                        loadType = AudioClipLoadType.Streaming,
                        compressionFormat = AudioCompressionFormat.Vorbis,
                        quality = .68f,
                        sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate,
                        preloadAudioData = false
                    };
                    importer.defaultSampleSettings = settings;
                    importer.SaveAndReimport();
                }
                count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Music] 적응형 음악 {count}개 생성 완료");
        }

        public static AudioClip[] LoadAll()
        {
            Array values = Enum.GetValues(typeof(MusicCue));
            var result = new AudioClip[values.Length];
            foreach (MusicCue cue in values) result[(int)cue] = AssetDatabase.LoadAssetAtPath<AudioClip>(PathFor(cue));
            return result;
        }

        private static string PathFor(MusicCue cue) => $"{Folder}/{cue}.wav";

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
            Ensure("Assets", "Audio"); Ensure("Assets/Audio", "Generated"); Ensure("Assets/Audio/Generated", "Music");
        }

        private static void Ensure(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
