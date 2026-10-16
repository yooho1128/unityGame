#if UNITY_EDITOR
using System;
using System.IO;
using ShadowTheater.UI;
using UnityEditor;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    /// <summary>기존 최종 음원을 보존하고 비어 있는 큐에만 16마디 오리지널 음악을 생성한다.</summary>
    public static class ReleaseMusicComposer
    {
        public const string Folder = "Assets/Audio/Final/Music";
        public const int Rate = 22050;
        private static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 };
        private static readonly int[] Dorian = { 0, 2, 3, 5, 7, 9, 10 };
        private static readonly int[] Melody = { 0, 2, 4, 2, 6, 4, 3, 1, 0, 2, 5, 4, 3, 2, 1, 4 };
        private static readonly int[] Progression = { 0, 5, 2, 6, 0, 3, 5, 4 };

        [MenuItem("Tools/Shadow Theater/Release/Compose Missing Music")]
        public static void GenerateMissing()
        {
            Directory.CreateDirectory(Folder);
            foreach (MusicCue cue in Enum.GetValues(typeof(MusicCue)))
            {
                string path = $"{Folder}/{cue}.wav";
                if (File.Exists(path)) continue;
                float[] samples = Compose(cue);
                Write(path, samples);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Configure(path);
            }
            Debug.Log("[ReleaseMusic] 13개 큐의 최종 음악 준비 완료 · 기존 음원 보존");
        }

        public static void Configure(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;
            importer.forceToMono = true;
            importer.loadInBackground = true;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .78f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        public static float[] Compose(MusicCue cue)
        {
            bool battle = cue == MusicCue.WildBattle || cue == MusicCue.RivalBattle || cue == MusicCue.BossBattle;
            int bpm = battle ? (cue == MusicCue.BossBattle ? 112 : 124) : cue == MusicCue.Ending ? 72 : 84 + (int)cue % 4 * 4;
            double beat = 60.0 / bpm;
            int length = (int)Math.Round(64 * beat * Rate);
            var output = new float[length];
            int root = 48 + ((int)cue * 5) % 12;
            int[] scale = cue == MusicCue.Ending || cue == MusicCue.Act7 ? Dorian : Minor;
            for (int bar = 0; bar < 16; bar++)
            {
                int chord = Progression[(bar + (int)cue % 3) % Progression.Length];
                double start = bar * 4 * beat;
                // Gentle bowed pad, picked ostinato, low strings and a call/response bell melody.
                for (int voice = 0; voice < 3; voice++)
                    Note(output, start, 4.7 * beat, root + Scale(scale, chord + voice * 2), .055f, 0);
                Note(output, start, 3.8 * beat, root - 12 + Scale(scale, chord), .12f, 1);
                int steps = battle ? 8 : 4;
                for (int step = 0; step < steps; step++)
                {
                    int degree = chord + new[] { 0, 2, 4, 2 }[step % 4];
                    Note(output, start + step * 4.0 / steps * beat, 1.35 * beat,
                        root + 12 + Scale(scale, degree), battle ? .085f : .065f, 2);
                }
                for (int step = 0; step < 4; step++)
                {
                    if ((bar + step) % 7 == 6) continue;
                    int phrase = Melody[(bar * 4 + step + (int)cue * 2) % Melody.Length];
                    int octave = bar >= 8 && bar < 12 ? 12 : 0;
                    Note(output, start + (step + .08) * beat, (step == 3 ? 1.7 : .9) * beat,
                        root + 12 + octave + Scale(scale, phrase), battle ? .075f : .09f, 3);
                }
                if (battle)
                {
                    for (int step = 0; step < 4; step++)
                    {
                        Drum(output, start + step * beat, step % 2 == 0, .14f, bar * 4 + step);
                        Drum(output, start + (step + .5) * beat, false, .035f, bar * 17 + step);
                    }
                }
                else if (cue == MusicCue.Act5 || cue == MusicCue.Act2)
                    Drum(output, start, true, .055f, bar);
            }
            // Circular delays retain the reverb tail across the exact loop boundary.
            var dry = (float[])output.Clone();
            foreach (double delay in new[] { .23, .41, .67 })
            {
                int offset = (int)(delay * Rate);
                for (int i = 0; i < length; i++) output[(i + offset) % length] += dry[i] * .1f;
            }
            double mean = 0;
            foreach (float sample in output) mean += sample;
            mean /= output.Length;
            float peak = .001f;
            for (int i = 0; i < length; i++) { output[i] -= (float)mean; peak = Mathf.Max(peak, Mathf.Abs(output[i])); }
            float gain = .78f / peak;
            for (int i = 0; i < length; i++) output[i] = Mathf.Clamp(output[i] * gain, -.8f, .8f);
            // A 4 ms equal blend removes any residual discontinuity without an audible gap.
            int seam = 88;
            for (int i = 0; i < seam; i++)
            {
                float midpoint = (output[i] + output[length - seam + i]) * .5f;
                float p = i / (float)(seam - 1);
                output[i] = Mathf.Lerp(midpoint, output[i], p);
                output[length - seam + i] = Mathf.Lerp(output[length - seam + i], midpoint, p);
            }
            float boundary = (output[0] + output[length - 1]) * .5f;
            output[0] = output[length - 1] = boundary;
            return output;
        }

        private static int Scale(int[] scale, int degree) => scale[degree % 7] + degree / 7 * 12;
        private static void Note(float[] output, double start, double duration, int midi, float gain, int voice)
        {
            int first = (int)Math.Round(start * Rate), count = (int)Math.Round(duration * Rate);
            double frequency = 440 * Math.Pow(2, (midi - 69) / 12.0), phase = 0;
            for (int j = 0; j < count; j++)
            {
                double t = j / (double)Rate, p = j / (double)count;
                phase += frequency * 2 * Math.PI / Rate;
                double attack = Math.Min(1, t / (voice == 0 ? .18 : .008));
                double release = Math.Min(1, (duration - t) / (voice == 0 ? .38 : .08));
                double envelope = attack * release;
                double value;
                if (voice == 0)
                    value = (Math.Sin(phase) + .28 * Math.Sin(phase * 2 + .08 * Math.Sin(t * 3)) + .09 * Math.Sin(phase * 3)) * envelope;
                else if (voice == 1)
                    value = (Math.Sin(phase) + .18 * Math.Sin(phase * 2)) * envelope * (.55 + .45 * Math.Exp(-t * 2));
                else if (voice == 2)
                    value = (Math.Sin(phase) + .22 * Math.Sin(phase * 2) + .1 * Math.Sin(phase * 3)) * envelope * Math.Exp(-p * 6);
                else
                    value = (Math.Sin(phase) + .35 * Math.Sin(phase * 2) * Math.Exp(-t * 3) + .12 * Math.Sin(phase * 4)) * envelope * Math.Exp(-p * 3);
                output[(first + j) % output.Length] += (float)value * gain;
            }
        }

        private static void Drum(float[] output, double start, bool kick, float gain, int seed)
        {
            int first = (int)(start * Rate), count = (int)(Rate * (kick ? .22 : .11));
            uint noise = unchecked((uint)(seed + 1) * 2654435761u); double phase = 0;
            for (int j = 0; j < count; j++)
            {
                double t = j / (double)Rate;
                noise = noise * 1664525u + 1013904223u;
                double grit = ((noise >> 9) / 8388607.0 * 2 - 1);
                phase += (kick ? 45 + 105 * Math.Exp(-t * 38) : 185) * 2 * Math.PI / Rate;
                double sound = kick ? Math.Sin(phase) : grit * .7 + Math.Sin(phase) * .3;
                output[(first + j) % output.Length] += (float)(sound * Math.Exp(-t * (kick ? 22 : 48))) * gain;
            }
        }

        private static void Write(string path, float[] samples)
        {
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1);
                writer.Write((short)1); writer.Write(Rate); writer.Write(Rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
                foreach (float value in samples) writer.Write((short)Mathf.RoundToInt(value * 32767));
            }
        }
    }
}
#endif
