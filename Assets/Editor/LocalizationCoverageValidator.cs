#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ShadowTheater.Data;
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEditor;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    public class LocalizationAuditResult
    {
        public int required, translated, missingKeys, missingKorean, missingEnglish, formatErrors;
        public readonly List<string> issues = new List<string>();
        public readonly List<LocalizationTemplateRow> incomplete = new List<LocalizationTemplateRow>();
        public bool Passed => missingKeys == 0 && missingKorean == 0 && missingEnglish == 0 && formatErrors == 0;
    }

    public class LocalizationTemplateRow
    {
        public string key, category, ko, en, issue;
    }

    /// <summary>코드와 전체 콘텐츠 카탈로그에서 실제 런타임 번역 요구 항목을 역산한다.</summary>
    public static class LocalizationCoverageValidator
    {
        private const string CatalogPath = "Assets/Resources/Data/LocalizationCatalog.json";
        private const string DialoguePath = "Assets/Resources/Data/DialogueCatalog.json";
        private const string QuestPath = "Assets/Resources/Data/QuestCatalog.json";
        private const string RegionPath = "Assets/Resources/Data/RegionCatalog.json";
        private const string EndingPath = "Assets/Resources/Data/EndingCatalog.json";
        public const string ReportPath = "Docs/Generated/LOCALIZATION_COVERAGE.md";
        public const string MissingTemplatePath = "Docs/Generated/LOCALIZATION_MISSING.tsv";
        private static readonly Regex StaticKeyRegex = new Regex(
            "L10n\\.(?:Get|Format)\\(\\s*\"([^\"]+)\"\\s*,\\s*\"((?:\\\\.|[^\"])*)\"",
            RegexOptions.Compiled);
        private static readonly Regex StaticTextRegex = new Regex(
            "L10n\\.Text\\(\\s*\"((?:\\\\.|[^\"])*)\"\\s*\\)", RegexOptions.Compiled);
        private static readonly Regex PlaceholderRegex = new Regex("\\{(\\d+)(?:[^}]*)?\\}", RegexOptions.Compiled);

        [MenuItem("Tools/Shadow Theater/Localization/Validate Full Coverage")]
        public static void ValidateFromMenu()
        {
            LocalizationAuditResult report = Run(true);
            if (report.Passed)
                Debug.Log($"[Localization] 통과: {report.translated}/{report.required} 항목");
            else
                Debug.LogError($"[Localization] 미완료: {report.translated}/{report.required} · " +
                               $"키 {report.missingKeys}, KO {report.missingKorean}, EN {report.missingEnglish}, 포맷 {report.formatErrors}\n" +
                               $"전체 목록: {ReportPath}");
        }

        public static LocalizationAuditResult Run(bool writeReport)
        {
            var result = new LocalizationAuditResult();
            var required = BuildRequirements();
            result.required = required.Count;
            LocalizationCatalogData catalog = Load<LocalizationCatalogData>(CatalogPath) ?? new LocalizationCatalogData();
            var entries = catalog.strings ?? new List<LocalizedStringEntry>();
            var byKey = entries.Where(x => x != null && !string.IsNullOrWhiteSpace(x.key))
                .GroupBy(x => x.key).ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
            var byKorean = entries.Where(x => x != null && !string.IsNullOrWhiteSpace(x.ko))
                .GroupBy(x => x.ko).ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

            foreach (Requirement item in required.Values.OrderBy(x => x.category).ThenBy(x => x.key))
            {
                LocalizedStringEntry entry = null;
                if (!byKey.TryGetValue(item.key, out entry) && !item.exactKey && !string.IsNullOrEmpty(item.korean))
                    byKorean.TryGetValue(item.korean, out entry);
                if (entry == null)
                {
                    result.missingKeys++;
                    result.issues.Add($"[{item.category}] MISSING KEY · {item.key} · KO: {OneLine(item.korean)}");
                    AddIncomplete(result, item, item.korean, string.Empty, "MISSING_KEY");
                    continue;
                }
                bool valid = true;
                if (string.IsNullOrWhiteSpace(entry.ko))
                {
                    result.missingKorean++; result.issues.Add($"[{item.category}] EMPTY KO · {item.key}");
                    AddIncomplete(result, item, entry.ko, entry.en, "EMPTY_KO"); valid = false;
                }
                if (string.IsNullOrWhiteSpace(entry.en))
                {
                    result.missingEnglish++; result.issues.Add($"[{item.category}] EMPTY EN · {item.key}");
                    AddIncomplete(result, item, entry.ko, entry.en, "EMPTY_EN"); valid = false;
                }
                if (!string.IsNullOrEmpty(entry.ko) && !string.IsNullOrEmpty(entry.en) &&
                    !Placeholders(entry.ko).SetEquals(Placeholders(entry.en)))
                {
                    result.formatErrors++; result.issues.Add($"[{item.category}] FORMAT MISMATCH · {item.key}");
                    AddIncomplete(result, item, entry.ko, entry.en, "FORMAT_MISMATCH"); valid = false;
                }
                if ((!string.IsNullOrEmpty(entry.ko) && !ValidCompositeFormat(entry.ko)) ||
                    (!string.IsNullOrEmpty(entry.en) && !ValidCompositeFormat(entry.en)))
                {
                    result.formatErrors++; result.issues.Add($"[{item.category}] MALFORMED FORMAT · {item.key}");
                    AddIncomplete(result, item, entry.ko, entry.en, "MALFORMED_FORMAT"); valid = false;
                }
                if (valid) result.translated++;
            }

            var duplicateKeys = entries.Where(x => x != null && !string.IsNullOrWhiteSpace(x.key))
                .GroupBy(x => x.key).Where(x => x.Count() > 1).Select(x => x.Key).ToList();
            foreach (string duplicate in duplicateKeys)
            { result.formatErrors++; result.issues.Add("[catalog] DUPLICATE KEY · " + duplicate); }
            int invalidEntries = entries.Count(x => x == null || string.IsNullOrWhiteSpace(x.key));
            if (invalidEntries > 0)
            { result.formatErrors += invalidEntries; result.issues.Add($"[catalog] NULL OR EMPTY KEY · {invalidEntries}개"); }
            foreach (var group in entries.Where(x => x != null && !string.IsNullOrWhiteSpace(x.ko))
                         .GroupBy(x => x.ko).Where(x => x.Select(y => y.en).Distinct().Count() > 1))
            { result.formatErrors++; result.issues.Add("[catalog] AMBIGUOUS KO · " + OneLine(group.Key)); }
            foreach (var group in entries.Where(x => x != null && !string.IsNullOrWhiteSpace(x.en))
                         .GroupBy(x => x.en).Where(x => x.Select(y => y.ko).Distinct().Count() > 1))
            { result.formatErrors++; result.issues.Add("[catalog] AMBIGUOUS EN · " + OneLine(group.Key)); }
            if (writeReport) WriteReport(result, required.Values);
            return result;
        }

        private static Dictionary<string, Requirement> BuildRequirements()
        {
            var required = new Dictionary<string, Requirement>(StringComparer.Ordinal);
            void Add(string key, string korean, string category, bool exact = true)
            {
                if (string.IsNullOrWhiteSpace(key) || required.ContainsKey(key)) return;
                required.Add(key, new Requirement { key = key, korean = korean ?? string.Empty, category = category, exactKey = exact });
            }

            foreach (string file in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(file);
                foreach (Match match in StaticKeyRegex.Matches(source))
                {
                    string key = match.Groups[1].Value;
                    if (key.EndsWith("_", StringComparison.Ordinal)) continue;
                    Add(key, Regex.Unescape(match.Groups[2].Value), "ui");
                }
                foreach (Match match in StaticTextRegex.Matches(source))
                {
                    string korean = Regex.Unescape(match.Groups[1].Value);
                    Add("literal." + StableHash(korean), korean, "ui-literal", false);
                }
            }
            for (int i = 0; i < 3; i++)
            {
                Add("battle.tutorial_title_" + i, i == 0 ? "공연 열기" : i == 1 ? "기술과 교체" : "각본 기록", "ui");
                Add("battle.tutorial_body_" + i, "전투 안내", "ui");
            }

            var dialogues = Load<DialogueCatalogData>(DialoguePath)?.dialogues ?? new List<DialogueSequence>();
            foreach (DialogueSequence dialogue in dialogues.Where(x => x != null))
            {
                for (int i = 0; i < (dialogue.lines?.Count ?? 0); i++)
                {
                    DialogueLine line = dialogue.lines[i];
                    Add($"dialogue.{dialogue.dialogueId}.{i}.speaker", line?.speaker, "dialogue");
                    Add($"dialogue.{dialogue.dialogueId}.{i}.text", line?.text, "dialogue");
                }
                for (int i = 0; i < (dialogue.choices?.Count ?? 0); i++)
                    Add($"dialogue.{dialogue.dialogueId}.choice.{i}", dialogue.choices[i]?.text, "dialogue");
            }

            var quests = Load<QuestCatalogData>(QuestPath)?.quests ?? new List<QuestDefinition>();
            foreach (QuestDefinition quest in quests.Where(x => x != null))
            {
                Add($"quest.{quest.questId}.title", quest.title, "quest");
                Add($"quest.{quest.questId}.summary", quest.summary, "quest");
                foreach (QuestObjectiveDefinition objective in quest.objectives ?? new List<QuestObjectiveDefinition>())
                    Add($"quest.{quest.questId}.{objective.objectiveId}", objective.description, "quest");
            }

            var regions = Load<RegionCatalogData>(RegionPath)?.regions ?? new List<RegionData>();
            foreach (RegionData region in regions.Where(x => x != null))
            {
                Add($"region.{region.regionId}.name", region.displayName, "region");
                Add($"region.{region.regionId}.act", region.actTitle, "region");
                Add($"region.{region.regionId}.environment", region.environment, "region");
                Add($"region.{region.regionId}.summary", region.summary, "region");
            }

            var endings = Load<EndingCatalogData>(EndingPath)?.endings ?? new List<EndingDefinition>();
            foreach (EndingDefinition ending in endings.Where(x => x != null))
            {
                Add($"ending.{ending.endingId}.title", ending.title, "ending");
                Add($"ending.{ending.endingId}.subtitle", ending.subtitle, "ending");
                Add($"ending.{ending.endingId}.archive", ending.archiveText, "ending");
            }

            ShadowDatabase database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>("Assets/Resources/ShadowDatabase.asset");
            if (database != null)
            {
                foreach (ShadowData shadow in database.shadows.Where(x => x != null))
                {
                    Add($"shadow.{shadow.shadowId}.name", shadow.displayName, "shadow", false);
                    Add($"shadow.{shadow.shadowId}.title", shadow.title, "shadow", false);
                    Add($"shadow.{shadow.shadowId}.lore_locked", shadow.loreLocked, "shadow", false);
                    Add($"shadow.{shadow.shadowId}.lore", shadow.loreUnlocked, "shadow", false);
                    AddForm(shadow.shadowId, "restored", shadow.restoredForm, Add);
                    AddForm(shadow.shadowId, "salvation", shadow.salvationForm, Add);
                    AddForm(shadow.shadowId, "grudge", shadow.grudgeForm, Add);
                }
                foreach (SkillData skill in database.skills.Where(x => x != null))
                {
                    Add($"skill.{skill.skillId}.name", skill.displayName, "skill", false);
                    Add($"skill.{skill.skillId}.description", skill.description, "skill", false);
                }
                foreach (ItemData item in database.items.Where(x => x != null))
                {
                    Add($"item.{item.itemId}.name", item.displayName, "item", false);
                    Add($"item.{item.itemId}.description", item.description, "item", false);
                }
            }
            else
            {
                // 180종 자동 확장 로스터는 생성된 데이터베이스에만 존재하므로 이를 빼고 PASS하면 안 된다.
                Add("validation.shadow_database", "ShadowDatabase 생성 필요", "catalog");
            }
            return required;
        }

        private static void AddForm(string shadowId, string stage, MemoryFormData form,
                                    Action<string, string, string, bool> add)
        {
            if (form?.enabled != true) return;
            add($"shadow.{shadowId}.{stage}.name", form.formName, "shadow", false);
            add($"shadow.{shadowId}.{stage}.lore", form.loreAppend, "shadow", false);
        }

        private static T Load<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<T>(File.ReadAllText(path)); }
            catch (Exception e) { Debug.LogError($"[Localization] {path} 파싱 실패: {e.Message}"); return null; }
        }

        private static HashSet<string> Placeholders(string value) => new HashSet<string>(
            PlaceholderRegex.Matches(value ?? string.Empty).Cast<Match>().Select(x => x.Groups[1].Value));

        private static bool ValidCompositeFormat(string value)
        {
            try
            {
                int max = -1;
                foreach (Match match in PlaceholderRegex.Matches(value ?? string.Empty))
                {
                    if (!int.TryParse(match.Groups[1].Value, out int index) || index > 255) return false;
                    max = Mathf.Max(max, index);
                }
                string.Format(value ?? string.Empty, Enumerable.Repeat<object>(string.Empty, max + 1).ToArray());
                return true;
            }
            catch (FormatException) { return false; }
        }

        private static string OneLine(string value) => (value ?? string.Empty).Replace("\n", " ↵ ");

        private static string StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in value ?? string.Empty) { hash ^= c; hash *= 16777619; }
                return hash.ToString("x8");
            }
        }

        private static void AddIncomplete(LocalizationAuditResult result, Requirement item,
                                          string korean, string english, string issue)
        {
            if (result.incomplete.Any(x => x.key == item.key)) return;
            result.incomplete.Add(new LocalizationTemplateRow
            {
                key = item.key, category = item.category, ko = korean ?? string.Empty,
                en = english ?? string.Empty, issue = issue
            });
        }

        private static void WriteReport(LocalizationAuditResult result, IEnumerable<Requirement> requirements)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            var byCategory = requirements.GroupBy(x => x.category).OrderBy(x => x.Key);
            var text = new StringBuilder();
            text.AppendLine("# 현지화 전체 커버리지").AppendLine();
            text.AppendLine($"- 결과: {(result.Passed ? "PASS" : "FAIL")}");
            text.AppendLine($"- 완료: {result.translated} / {result.required}");
            text.AppendLine($"- 누락 키: {result.missingKeys}");
            text.AppendLine($"- 빈 한국어/영문: {result.missingKorean} / {result.missingEnglish}");
            text.AppendLine($"- 포맷·중복 오류: {result.formatErrors}").AppendLine();
            text.AppendLine("## 요구 항목").AppendLine();
            foreach (var group in byCategory) text.AppendLine($"- {group.Key}: {group.Count()}");
            text.AppendLine().AppendLine("## 수정 필요 목록").AppendLine();
            if (result.issues.Count == 0) text.AppendLine("- 없음");
            else foreach (string issue in result.issues) text.AppendLine("- " + issue.Replace("\n", " "));
            File.WriteAllText(ReportPath, text.ToString());

            var template = new StringBuilder("key\tcategory\tko\ten\tissue\n");
            foreach (LocalizationTemplateRow row in result.incomplete.OrderBy(x => x.category).ThenBy(x => x.key))
                template.Append(Tsv(row.key)).Append('\t').Append(Tsv(row.category)).Append('\t')
                    .Append(Tsv(row.ko)).Append('\t').Append(Tsv(row.en)).Append('\t')
                    .Append(Tsv(row.issue)).Append('\n');
            File.WriteAllText(MissingTemplatePath, template.ToString());
            AssetDatabase.Refresh();
        }

        private static string Tsv(string value) => (value ?? string.Empty)
            .Replace("\t", " ").Replace("\r", string.Empty).Replace("\n", " ↵ ");

        private class Requirement
        {
            public string key, korean, category;
            public bool exactKey;
        }
    }
}
#endif
