#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ShadowTheater.Data;
using UnityEditor;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    /// <summary>180종 능력치와 기술 효율의 극단값을 찾고, 선택적으로 보수적인 보정을 적용한다.</summary>
    public static class CombatBalanceAnalyzer
    {
        private const string DatabasePath = "Assets/Resources/ShadowDatabase.asset";
        private const string ReportPath = "Docs/Generated/COMBAT_BALANCE_REPORT.tsv";
        private const float MaxCorrectionPerRun = .08f;

        [MenuItem("Tools/Shadow Theater/Balance/Analyze 180 Shadows")]
        public static void Analyze() => Run(false);

        [MenuItem("Tools/Shadow Theater/Balance/Analyze and Apply Conservative Fixes")]
        public static void AnalyzeAndFix()
        {
            if (!EditorUtility.DisplayDialog("전투 밸런스 자동 보정",
                    "생성된 그림자와 기술의 극단값을 한 번에 최대 8% 보정합니다. 계속할까요?", "보정", "취소")) return;
            Run(true);
        }

        private static void Run(bool apply)
        {
            ShadowDatabase database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>(DatabasePath);
            if (database == null)
            {
                Debug.LogError("[Balance] 먼저 Generate and Validate Full Game을 실행해 ShadowDatabase를 생성하세요.");
                return;
            }
            List<ShadowData> shadows = database.shadows.Where(x => x != null).ToList();
            List<SkillData> skills = database.skills.Where(x => x != null).ToList();
            var medians = shadows.GroupBy(x => x.growthTier).ToDictionary(x => x.Key,
                x => Median(x.Select(y => StatBudget(y, 50))));
            int changed = 0;
            var lines = new List<string>
            {
                "구분\tID\t이름\t등급/FP\t측정값\t기준값\t판정\t자동보정"
            };

            foreach (ShadowData shadow in shadows.OrderBy(x => x.shadowId, StringComparer.Ordinal))
            {
                float value = StatBudget(shadow, 50);
                float median = medians.TryGetValue(shadow.growthTier, out float found) ? found : value;
                BalanceVerdict verdict = EvaluateRatio(value, median, .72f, 1.32f);
                bool fixedValue = apply && verdict != BalanceVerdict.Normal && TuneShadow(shadow, value, median);
                if (fixedValue) changed++;
                lines.Add(Row("그림자", shadow.shadowId, shadow.displayName, shadow.growthTier.ToString(),
                    value, median, verdict, fixedValue));
            }

            foreach (SkillData skill in skills.OrderBy(x => x.skillId, StringComparer.Ordinal))
            {
                float value = SkillEfficiency(skill);
                GetSkillEfficiencyBand(skill, out float low, out float high);
                float reference = (low + high) * .5f;
                BalanceVerdict verdict = value < low ? BalanceVerdict.Low
                    : value > high ? BalanceVerdict.High : BalanceVerdict.Normal;
                bool fixedValue = apply && verdict != BalanceVerdict.Normal && TuneSkill(skill, value, low, high);
                if (fixedValue) changed++;
                lines.Add(Row("기술", skill.skillId, skill.displayName, skill.fpCost.ToString(),
                    value, reference, verdict, fixedValue));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllLines(ReportPath, lines, new UTF8Encoding(true));
            if (apply)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            Debug.Log($"[Balance] 분석 완료: 그림자 {shadows.Count}, 기술 {skills.Count}, " +
                      $"{(apply ? $"보정 {changed}" : "분석 전용")} · {ReportPath}");
        }

        public static float StatBudget(ShadowData shadow, int level)
        {
            if (shadow == null) return 0f;
            return shadow.GetHp(level) * .1f + shadow.GetAtk(level) * 2f +
                   shadow.GetDef(level) * 1.5f + shadow.GetSpd(level);
        }

        public static float SkillEfficiency(SkillData skill)
        {
            if (skill == null) return 0f;
            float damage = skill.DealsDamage ? skill.damageMultiplier * Mathf.Clamp01(skill.accuracy) : 0f;
            float healing = skill.target == SkillTarget.Self ? skill.healRatio * 2.4f : 0f;
            float status = skill.statusEffect != StatusEffectType.None ? skill.statusChance * .35f : 0f;
            return (damage + healing + status) / (1f + skill.fpCost * .42f);
        }

        public static BalanceVerdict EvaluateRatio(float value, float reference, float lowRatio, float highRatio)
        {
            if (reference <= 0f || value < reference * lowRatio) return BalanceVerdict.Low;
            return value > reference * highRatio ? BalanceVerdict.High : BalanceVerdict.Normal;
        }

        public static void ValidatePolicyRegression(List<string> errors)
        {
            if (EvaluateRatio(100f, 100f, .72f, 1.32f) != BalanceVerdict.Normal ||
                EvaluateRatio(60f, 100f, .72f, 1.32f) != BalanceVerdict.Low ||
                EvaluateRatio(140f, 100f, .72f, 1.32f) != BalanceVerdict.High)
                errors.Add("전투 밸런스 분석: 극단값 판정 오류");
            var sample = ScriptableObject.CreateInstance<SkillData>();
            sample.fpCost = 2; sample.damageMultiplier = 2f; sample.accuracy = 1f;
            float efficient = SkillEfficiency(sample);
            sample.damageMultiplier = 1f;
            if (efficient <= SkillEfficiency(sample)) errors.Add("전투 밸런스 분석: 기술 효율 계산 오류");
            UnityEngine.Object.DestroyImmediate(sample);
        }

        private static bool TuneShadow(ShadowData shadow, float value, float reference)
        {
            if (shadow == null || value <= 0f || reference <= 0f) return false;
            float desired = Mathf.Clamp(reference / value, 1f - MaxCorrectionPerRun, 1f + MaxCorrectionPerRun);
            Undo.RecordObject(shadow, "그림자 밸런스 자동 보정");
            shadow.baseHp = Mathf.Max(1, Mathf.RoundToInt(shadow.baseHp * desired));
            shadow.baseAtk = Mathf.Max(1, Mathf.RoundToInt(shadow.baseAtk * desired));
            shadow.baseDef = Mathf.Max(0, Mathf.RoundToInt(shadow.baseDef * desired));
            shadow.baseSpd = Mathf.Max(1, Mathf.RoundToInt(shadow.baseSpd * desired));
            shadow.hpGrowth = Mathf.Max(.1f, shadow.hpGrowth * desired);
            shadow.atkGrowth = Mathf.Max(.05f, shadow.atkGrowth * desired);
            shadow.defGrowth = Mathf.Max(.05f, shadow.defGrowth * desired);
            shadow.spdGrowth = Mathf.Max(.01f, shadow.spdGrowth * desired);
            EditorUtility.SetDirty(shadow);
            return true;
        }

        private static bool TuneSkill(SkillData skill, float value, float low, float high)
        {
            if (skill == null || !skill.DealsDamage || value <= 0f) return false;
            float target = value < low ? low : high;
            float desired = Mathf.Clamp(target / value, 1f - MaxCorrectionPerRun, 1f + MaxCorrectionPerRun);
            Undo.RecordObject(skill, "기술 밸런스 자동 보정");
            skill.damageMultiplier = Mathf.Max(.1f, skill.damageMultiplier * desired);
            EditorUtility.SetDirty(skill);
            return true;
        }

        private static void GetSkillEfficiencyBand(SkillData skill, out float low, out float high)
        {
            if (skill.isUltimate) { low = .72f; high = 1.35f; return; }
            if (skill.target == SkillTarget.Self) { low = .12f; high = .48f; return; }
            low = skill.fpCost == 0 ? .45f : .52f;
            high = skill.fpCost == 0 ? 1.25f : 1.15f;
        }

        private static float Median(IEnumerable<float> source)
        {
            float[] values = source.OrderBy(x => x).ToArray();
            if (values.Length == 0) return 0f;
            int middle = values.Length / 2;
            return values.Length % 2 == 0 ? (values[middle - 1] + values[middle]) * .5f : values[middle];
        }

        private static string Row(string type, string id, string name, string group, float value,
            float reference, BalanceVerdict verdict, bool fixedValue) => string.Join("\t", new[]
            {
                type, Clean(id), Clean(name), group,
                value.ToString("0.000", CultureInfo.InvariantCulture),
                reference.ToString("0.000", CultureInfo.InvariantCulture),
                verdict == BalanceVerdict.Low ? "낮음" : verdict == BalanceVerdict.High ? "높음" : "정상",
                fixedValue ? "적용" : "-"
            });

        private static string Clean(string value) => (value ?? string.Empty).Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
    }

    public enum BalanceVerdict { Low, Normal, High }
}
#endif
