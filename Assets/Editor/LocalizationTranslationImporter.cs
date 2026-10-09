#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ShadowTheater.UI;
using UnityEditor;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    public static class LocalizationTranslationImporter
    {
        private const string CatalogPath = "Assets/Resources/Data/LocalizationCatalog.json";

        [MenuItem("Tools/Shadow Theater/Localization/Import Completed Translation TSV")]
        public static void Import()
        {
            string source = EditorUtility.OpenFilePanel("완료한 현지화 TSV 선택", "Docs/Generated", "tsv");
            if (string.IsNullOrEmpty(source)) return;
            try
            {
                var catalog = JsonUtility.FromJson<LocalizationCatalogData>(File.ReadAllText(CatalogPath));
                if (catalog?.strings == null) throw new InvalidDataException("현지화 카탈로그 형식 오류");
                var byKey = catalog.strings.Where(x => x != null).ToDictionary(x => x.key, StringComparer.Ordinal);
                var updates = Parse(File.ReadAllText(source));
                if (updates.Count == 0) { Debug.LogWarning("[Localization] 반영할 완료 번역이 없습니다."); return; }
                string backup = "Docs/Generated/LocalizationBackup_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".json";
                Directory.CreateDirectory("Docs/Generated");
                File.Copy(CatalogPath, backup, false);
                foreach (var entry in updates)
                {
                    if (byKey.TryGetValue(entry.key, out var existing)) { existing.ko = entry.ko; existing.en = entry.en; }
                    else { catalog.strings.Add(entry); byKey.Add(entry.key, entry); }
                }
                File.WriteAllText(CatalogPath, JsonUtility.ToJson(catalog, true));
                AssetDatabase.ImportAsset(CatalogPath, ImportAssetOptions.ForceSynchronousImport);
                ShadowTheater.UI.L10n.Reload();
                LocalizationCoverageValidator.ValidateFromMenu();
                Debug.Log($"[Localization] 번역 {updates.Count}개 반영 · 원본 백업: {backup}");
            }
            catch (Exception e) { Debug.LogError("[Localization] 가져오기 실패: " + e.Message); }
        }

        public static List<LocalizedStringEntry> Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) throw new InvalidDataException("빈 TSV");
            var rows = new List<LocalizedStringEntry>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            if (lines.Length == 0) throw new InvalidDataException("빈 TSV");
            string[] header = lines[0].TrimStart('\uFEFF').Split('\t');
            int key = Array.IndexOf(header, "key"), ko = Array.IndexOf(header, "ko"), en = Array.IndexOf(header, "en");
            if (key < 0 || ko < 0 || en < 0) throw new InvalidDataException("key/ko/en 열이 필요합니다");
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] cells = lines[i].Split('\t');
                if (cells.Length != header.Length) throw new InvalidDataException($"{i + 1}행: 열 수 불일치");
                string id = cells[key].Trim();
                if (id.Length == 0 || !keys.Add(id)) throw new InvalidDataException($"{i + 1}행: 빈 키 또는 중복 키");
                // 아직 번역하지 않은 행은 기존 카탈로그를 지우지 않는다.
                if (string.IsNullOrWhiteSpace(cells[ko]) || string.IsNullOrWhiteSpace(cells[en])) continue;
                string korean = cells[ko].Replace(" ↵ ", "\n"), english = cells[en].Replace(" ↵ ", "\n");
                if (!Indices(korean).SetEquals(Indices(english)))
                    throw new InvalidDataException($"{i + 1}행: 한영 서식 변수 불일치 ({id})");
                rows.Add(new LocalizedStringEntry { key = id, ko = korean, en = english });
            }
            return rows;
        }

        public static void ValidateRegression(List<string> errors)
        {
            const string valid = "key\tcategory\tko\ten\tissue\n" +
                                 "ui.test\tui\t첫 줄 ↵ 둘째 줄 {0}\tFirst line ↵ Second line {0}\tMISSING_KEY\n" +
                                 "ui.empty\tui\t미완성\t\tMISSING_KEY\n";
            try
            {
                List<LocalizedStringEntry> rows = Parse(valid);
                if (rows.Count != 1 || rows[0].key != "ui.test" ||
                    rows[0].ko != "첫 줄\n둘째 줄 {0}" || rows[0].en != "First line\nSecond line {0}")
                    errors.Add("현지화 TSV: 완료 행 선별 또는 줄바꿈 복원 오류");
            }
            catch (Exception e) { errors.Add("현지화 TSV: 정상 샘플 파싱 실패 (" + e.Message + ")"); }

            ExpectInvalid(string.Empty, "빈 파일", errors);
            ExpectInvalid("key\tko\nui.test\t문장\n", "필수 헤더", errors);
            ExpectInvalid("key\tko\ten\na\t가\tA\na\t나\tB\n", "중복 키", errors);
            ExpectInvalid("key\tko\ten\na\t가\n", "열 수", errors);
            ExpectInvalid("key\tko\ten\na\t수치 {0}\tValue {1}\n", "서식 변수", errors);
        }

        private static void ExpectInvalid(string value, string label, List<string> errors)
        {
            try
            {
                Parse(value);
                errors.Add("현지화 TSV: " + label + " 오류가 통과함");
            }
            catch (InvalidDataException) { }
            catch (Exception e) { errors.Add($"현지화 TSV: {label} 검사에서 잘못된 예외 ({e.GetType().Name})"); }
        }

        private static HashSet<string> Indices(string text) => new HashSet<string>(
            Regex.Matches(text, "\\{(\\d+)(?:[^}]*)?\\}").Cast<Match>().Select(x => x.Groups[1].Value));
    }
}
#endif
