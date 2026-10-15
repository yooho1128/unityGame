#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Story;
using UnityEditor;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    public static class RegionDifficultySimulator
    {
        private const string DatabasePath = "Assets/Resources/ShadowDatabase.asset";
        private const string RegionPath = "Assets/Resources/Data/RegionCatalog.json";
        private const string RosterPath = "Assets/Resources/Data/RegionalRosterManifest.json";
        private const string PhasePath = "Assets/Resources/Data/BossPhaseCatalog.json";
        private const string ReportPath = "Docs/Generated/REGION_DIFFICULTY_SIMULATION.tsv";
        private const int Trials = 5000;

        [MenuItem("Tools/Shadow Theater/Balance/Simulate Region Difficulty")]
        public static void Simulate()
        {
            ShadowDatabase database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>(DatabasePath);
            if (database == null || !File.Exists(RegionPath))
            {
                Debug.LogError("[RegionSimulation] 먼저 Generate and Validate Full Game을 실행하세요.");
                return;
            }
            RegionCatalogData catalog = JsonUtility.FromJson<RegionCatalogData>(File.ReadAllText(RegionPath));
            RegionalRosterManifest roster = File.Exists(RosterPath)
                ? JsonUtility.FromJson<RegionalRosterManifest>(File.ReadAllText(RosterPath)) : new RegionalRosterManifest();
            BossPhaseCatalogData phases = File.Exists(PhasePath)
                ? JsonUtility.FromJson<BossPhaseCatalogData>(File.ReadAllText(PhasePath)) : new BossPhaseCatalogData();
            var phaseByBoss = phases.phases.Where(x => x != null && !string.IsNullOrEmpty(x.bossShadowId))
                .GroupBy(x => x.bossShadowId).ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
            var lines = new List<string>
            {
                "순서\t지역ID\t지역명\t권장레벨\t조우후보\t희귀조우율\t평균조우걸음\t보스\t보스전투지수\t페이즈부담\t위험도\t진단"
            };
            int warningCount = 0;
            foreach (RegionData region in (catalog?.regions ?? new List<RegionData>()).OrderBy(x => x.order))
            {
                List<WeightedShadow> candidates = BuildCandidates(region, database, roster);
                float rareRate = WeightedRareRate(candidates);
                float averageSteps = region.AllowsRandomEncounters
                    ? SimulateAverageSteps(region, Trials, StableHash(region.regionId)) : 0f;
                ShadowData boss = FindByDisplayName(database, region.bossShadowName);
                float bossIndex = boss == null ? 0f : BossIndex(boss, region.recommendedLevelMax, database.shadows);
                BossPhaseDefinition phase = boss != null && phaseByBoss.TryGetValue(boss.shadowId, out BossPhaseDefinition found)
                    ? found : null;
                float phaseLoad = PhaseLoad(phase);
                string diagnosis = Diagnose(region, candidates.Count, rareRate, boss, bossIndex, phaseLoad);
                if (diagnosis != "정상") warningCount++;
                lines.Add(string.Join("\t", new[]
                {
                    region.order.ToString(), Clean(region.regionId), Clean(region.displayName),
                    $"{region.recommendedLevelMin}-{region.recommendedLevelMax}", candidates.Count.ToString(),
                    Percent(rareRate), Number(averageSteps), Clean(region.bossShadowName), Number(bossIndex),
                    Number(phaseLoad), RiskLabel(bossIndex, phaseLoad, candidates.Count, region), diagnosis
                }));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllLines(ReportPath, lines, new UTF8Encoding(true));
            AssetDatabase.Refresh();
            Debug.Log($"[RegionSimulation] {lines.Count - 1}개 지역 분석 완료 · 경고 {warningCount}개 · {ReportPath}");
        }

        public static float SimulateAverageSteps(RegionData region, int trials, int seed)
        {
            if (region == null || trials <= 0 || !region.AllowsRandomEncounters) return 0f;
            RegionalEncounterPolicy.GetCadence(region, out int safeMin, out int safeMax,
                out float chance, out int pity);
            var random = new System.Random(seed);
            long total = 0;
            for (int trial = 0; trial < trials; trial++)
            {
                int steps = random.Next(safeMin, safeMax + 1);
                for (int eligible = 1; eligible <= pity; eligible++)
                {
                    steps++;
                    if (eligible >= pity || random.NextDouble() <= chance) break;
                }
                total += steps;
            }
            return total / (float)trials;
        }

        public static float PhaseLoad(BossPhaseDefinition phase)
        {
            if (phase == null) return 0f;
            float value = phase.enemyFpGain * .12f + phase.playerFpDrain * .16f + phase.healRatio * 2f;
            if (phase.cleanseBoss) value += .18f;
            if (phase.TryGetPlayerStatus(out StatusEffectType status))
                value += BattleUnit.IsMajor(status) ? .34f : .2f;
            return value;
        }

        public static void ValidatePolicyRegression(List<string> errors)
        {
            var normal = new RegionData { regionId = "normal", recommendedLevelMin = 1, recommendedLevelMax = 5 };
            float average = SimulateAverageSteps(normal, 5000, 17);
            if (average < 12f || average > 20f)
                errors.Add($"지역 난이도 시뮬레이터: 일반 필드 조우 간격 오류 ({average:0.0})");
            var phase = new BossPhaseDefinition { enemyFpGain = 2, playerFpDrain = 1, healRatio = .1f,
                cleanseBoss = true, playerStatus = "Burn" };
            if (PhaseLoad(phase) <= PhaseLoad(new BossPhaseDefinition { enemyFpGain = 1 }))
                errors.Add("지역 난이도 시뮬레이터: 보스 페이즈 부담 계산 오류");
        }

        private static List<WeightedShadow> BuildCandidates(RegionData region, ShadowDatabase database,
            RegionalRosterManifest roster)
        {
            var values = new Dictionary<string, WeightedShadow>(StringComparer.Ordinal);
            foreach (RegionalRosterEntry entry in roster.entries ?? new List<RegionalRosterEntry>())
            {
                if (entry.regionId != region.regionId) continue;
                ShadowData shadow = database.GetShadow(entry.shadowId);
                Add(values, region, shadow, entry.featured ? 2f : 1f);
            }
            string[] featured = region.featuredShadows ?? Array.Empty<string>();
            for (int i = 0; i < featured.Length; i++)
                Add(values, region, FindByDisplayName(database, featured[i]), RegionalEncounterPolicy.FeaturedWeight(i));
            return values.Values.ToList();
        }

        private static void Add(Dictionary<string, WeightedShadow> values, RegionData region,
            ShadowData shadow, float weight)
        {
            if (!RegionalEncounterPolicy.IsEligible(region, shadow)) return;
            weight *= RegionalEncounterPolicy.RarityMultiplier(shadow.growthTier);
            if (weight <= 0f) return;
            if (values.TryGetValue(shadow.shadowId, out WeightedShadow old)) old.weight = Mathf.Max(old.weight, weight);
            else values.Add(shadow.shadowId, new WeightedShadow { shadow = shadow, weight = weight });
        }

        private static float WeightedRareRate(List<WeightedShadow> candidates)
        {
            float total = candidates.Sum(x => x.weight);
            return total <= 0f ? 0f : candidates.Where(x => x.shadow.growthTier == GrowthTier.Rare).Sum(x => x.weight) / total;
        }

        private static float BossIndex(ShadowData boss, int level, IEnumerable<ShadowData> all)
        {
            float bossPower = CombatPower(boss, level);
            float[] baseline = all.Where(x => x != null && x.growthTier < GrowthTier.RegionalBoss)
                .Select(x => CombatPower(x, level)).OrderBy(x => x).ToArray();
            if (baseline.Length == 0) return 0f;
            float median = baseline.Length % 2 == 0
                ? (baseline[baseline.Length / 2 - 1] + baseline[baseline.Length / 2]) * .5f
                : baseline[baseline.Length / 2];
            return median <= 0f ? 0f : bossPower / median;
        }

        private static float CombatPower(ShadowData shadow, int level) => shadow == null ? 0f :
            shadow.GetHp(level) * (shadow.GetAtk(level) * 1.8f + shadow.GetDef(level) * 1.15f +
                                   shadow.GetSpd(level) * .55f) / 100f;

        private static string Diagnose(RegionData region, int candidates, float rareRate, ShadowData boss,
            float bossIndex, float phaseLoad)
        {
            var issues = new List<string>();
            if (region.AllowsRandomEncounters && candidates == 0) issues.Add("조우 후보 없음");
            if (rareRate > .35f) issues.Add("희귀 출현 과다");
            if (!string.IsNullOrEmpty(region.bossShadowName) && boss == null) issues.Add("보스 데이터 미연결");
            if (bossIndex > 2.35f) issues.Add("보스 능력치 급등");
            if (phaseLoad > 1.35f) issues.Add("페이즈 부담 과다");
            return issues.Count == 0 ? "정상" : string.Join(", ", issues);
        }

        private static string RiskLabel(float bossIndex, float phaseLoad, int candidates, RegionData region)
        {
            if (region.AllowsRandomEncounters && candidates == 0 || bossIndex + phaseLoad > 3.2f) return "위험";
            if (bossIndex + phaseLoad > 2.35f) return "주의";
            return "안정";
        }

        private static ShadowData FindByDisplayName(ShadowDatabase database, string name)
        {
            if (database == null || string.IsNullOrWhiteSpace(name)) return null;
            string key = Normalize(name);
            return database.shadows.FirstOrDefault(x => x != null && Normalize(x.displayName) == key);
        }

        private static string Normalize(string value) => new string((value ?? string.Empty)
            .Where(c => !char.IsWhiteSpace(c) && c != '·').ToArray());
        private static int StableHash(string value) { unchecked { int h = 17; foreach (char c in value ?? "") h = h * 31 + c; return h; } }
        private static string Percent(float value) => (value * 100f).ToString("0.0", CultureInfo.InvariantCulture) + "%";
        private static string Number(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);
        private static string Clean(string value) => (value ?? string.Empty).Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");

        private class WeightedShadow { public ShadowData shadow; public float weight; }
        [Serializable] private class RegionalRosterManifest { public List<RegionalRosterEntry> entries = new List<RegionalRosterEntry>(); }
        [Serializable] private class RegionalRosterEntry { public string shadowId; public string regionId; public bool featured; }
    }
}
#endif
