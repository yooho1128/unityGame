#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadowTheater.Data;
using ShadowTheater.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.EditorTools
{
    public static class ReleasePresentationValidator
    {
        public static readonly string[] Backgrounds = { "Theater", "Ember", "Archive", "Ocean", "Cosmos" };
        public static readonly string[] StoryPortraits = { "ash_king", "infinite_archive_dragon", "night_devouring_beast",
            "last_puppeteer", "high_censor_nox", "drowned_painter", "rival_director", "boss_moonlit_widow" };

        public static void Validate(List<string> errors)
        {
            foreach (string name in Backgrounds)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/Art/BattleBackgrounds/{name}.png");
                if (sprite == null || sprite.texture.width < 1024 || sprite.texture.width <= sprite.texture.height)
                    errors.Add("출시 전투 배경 규격 또는 연결 오류: " + name);
            }
            foreach (string id in StoryPortraits)
                if (AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Final/Portraits/{id}.png") == null)
                    errors.Add("주요 스토리 보스 최종 초상 누락: " + id);
            foreach (BattleFxGlyph glyph in Enum.GetValues(typeof(BattleFxGlyph)))
                if (AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/Art/FX/{glyph}.png") == null)
                    errors.Add("출시 FX 마스크 누락: " + glyph);
            foreach (UltimateFxStyle style in Enum.GetValues(typeof(UltimateFxStyle)))
            {
                if (style == UltimateFxStyle.None) continue;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/FX/Ultimate/{style}.prefab");
                if (prefab == null || prefab.GetComponentsInChildren<Image>(true).Any(x => x.sprite == null || !AssetDatabase.Contains(x.sprite)))
                    errors.Add("필살기 FX의 영구 스프라이트 연결 누락: " + style);
            }
            GameObject systems = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Systems/CoreSystems.prefab");
            GameObject battleUi = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/BattleCanvas.prefab");
            if (battleUi != null)
                foreach (MonoBehaviour panel in battleUi.GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(x => x is BattleUnitPanel || x is BattlePartySlot))
                {
                    var hp = new SerializedObject(panel).FindProperty("hpFill")?.objectReferenceValue as Image;
                    if (hp == null || hp.sprite == null || hp.type != Image.Type.Filled)
                        errors.Add("전투/파티 HP 바 채움 스프라이트 연결 누락: " + panel.name);
                }
            AdaptiveMusicDirector music = systems != null ? systems.GetComponent<AdaptiveMusicDirector>() : null;
            SerializedProperty clips = music != null ? new SerializedObject(music).FindProperty("clips") : null;
            foreach (MusicCue cue in Enum.GetValues(typeof(MusicCue)))
            {
                string path = $"Assets/Audio/Final/Music/{cue}.wav";
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null || clip.channels != 1 || clip.length < 24)
                { errors.Add("출시 음악 길이/채널 오류: " + cue); continue; }
                if (clips == null || clips.arraySize <= (int)cue || clips.GetArrayElementAtIndex((int)cue).objectReferenceValue != clip)
                    errors.Add("CoreSystems 최종 음악 연결 누락: " + cue);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null || importer.defaultSampleSettings.loadType != AudioClipLoadType.Streaming)
                    errors.Add("출시 음악 스트리밍 설정 오류: " + cue);
                ValidateWave(path, errors);
            }
        }

        private static void ValidateWave(string path, List<string> errors)
        {
            try
            {
                using (var reader = new BinaryReader(File.OpenRead(path)))
                {
                    if (reader.ReadUInt32() != 0x46464952) throw new InvalidDataException("RIFF");
                    reader.ReadUInt32();
                    if (reader.ReadUInt32() != 0x45564157) throw new InvalidDataException("WAVE");
                    int format = 0, channels = 0, bits = 0, dataSize = 0; long dataOffset = 0;
                    while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                    {
                        uint kind = reader.ReadUInt32(); int size = reader.ReadInt32(); long start = reader.BaseStream.Position;
                        if (size < 0 || start + size > reader.BaseStream.Length) throw new InvalidDataException("chunk");
                        if (kind == 0x20746d66 && size >= 16)
                        {
                            format = reader.ReadInt16(); channels = reader.ReadInt16();
                            reader.ReadInt32(); reader.ReadInt32(); reader.ReadInt16(); bits = reader.ReadInt16();
                        }
                        if (kind == 0x61746164) { dataOffset = start; dataSize = size; }
                        reader.BaseStream.Position = start + size + size % 2;
                    }
                    if (format != 1 || channels != 1 || bits != 16 || dataSize < 2) throw new InvalidDataException("mono PCM16");
                    reader.BaseStream.Position = dataOffset;
                    double energy = 0, mean = 0, peak = 0, first = 0, last = 0; int count = dataSize / 2;
                    for (int i = 0; i < count; i++)
                    {
                        double value = reader.ReadInt16() / 32768.0;
                        if (i == 0) first = value; last = value;
                        energy += value * value; mean += value; peak = Math.Max(peak, Math.Abs(value));
                    }
                    if (peak > .98 || Math.Sqrt(energy / count) < .002 || Math.Abs(mean / count) > .02 || Math.Abs(first - last) > .025)
                        errors.Add("출시 음악 클리핑/무음/DC/반복 경계 오류: " + path);
                }
            }
            catch (Exception exception) { errors.Add("출시 음악 PCM 검사 실패: " + path + " · " + exception.Message); }
        }
    }
}
#endif
