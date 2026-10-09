#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShadowTheater.EditorTools
{
    /// <summary>180종 데이터와 40개 필드의 생성 결과를 출시 전 일괄 검사한다.</summary>
    public static class GameRegressionValidator
    {
        private const string SceneFolder = "Assets/Scenes/Prologue";
        private const string RegionPath = "Assets/Resources/Data/RegionCatalog.json";
        private const string RosterPath = "Assets/Resources/Data/RegionalRosterManifest.json";
        private const string DatabasePath = "Assets/Resources/ShadowDatabase.asset";
        private const string QuestPath = "Assets/Resources/Data/QuestCatalog.json";
        private static string _declaredStoryFlags;
        private static string _worldSource;

        [MenuItem("Tools/Shadow Theater/Validate Full Game")]
        public static void ValidateFromMenu()
        {
            var report = Run(false);
            if (report.errors.Count == 0)
                Debug.Log($"[Regression] 통과: {report.regions}개 지역, {report.scenes}개 필드, " +
                          $"{report.shadows}종 그림자, {report.portals}개 포털, {report.encounters}개 인카운터");
            else
                Debug.LogError("[Regression] 실패\n- " + string.Join("\n- ", report.errors));
            foreach (string warning in report.warnings) Debug.LogWarning("[Regression] " + warning);
        }

        [MenuItem("Tools/Shadow Theater/Generate and Validate Full Game")]
        public static void GenerateAndValidate()
        {
            MobileBuildConfigurator.Apply();
            PlayablePrologueGenerator.Generate();
            ValidateFromMenu();
        }

        public static void ValidateForCi()
        {
            var report = Run(false);
            if (report.errors.Count > 0)
                throw new BuildFailedException("Shadow Theater regression failed:\n" + string.Join("\n", report.errors));
        }

        public static void ValidateReleaseForCi()
        {
            var report = Run(true);
            if (report.errors.Count > 0)
                throw new BuildFailedException("Shadow Theater release gate failed:\n" + string.Join("\n", report.errors));
        }

        [MenuItem("Tools/Shadow Theater/Mobile/Validate Release Gate")]
        public static void ValidateReleaseGate()
        {
            var report = Run(true);
            if (report.errors.Count == 0)
                Debug.Log("[ReleaseGate] 통과: 콘텐츠·현지화·모바일 설정·Android/iOS 실기기 QA");
            else
                Debug.LogError("[ReleaseGate] 출시 차단\n- " + string.Join("\n- ", report.errors));
            foreach (string warning in report.warnings) Debug.LogWarning("[ReleaseGate] " + warning);
        }

        public static bool TryValidateReleaseGate(out string errorText)
        {
            var report = Run(true);
            errorText = string.Join("\n- ", report.errors);
            return report.errors.Count == 0;
        }

        private static ValidationReport Run(bool requireDeviceReports)
        {
            var result = new ValidationReport();
            ValidateDevicePolicy(result);
            CoreContentBatchGenerator.ValidateGrowthPolicyRegression(result.errors);
            LocalizationTranslationImporter.ValidateRegression(result.errors);
            MobileBuildConfigurator.ValidateScenePolicyRegression(result.errors);
            MobileBuildConfigurator.ValidateSettings(result.errors);
            ValidateMigration(result);
            ValidateBattleAi(result);
            ValidateBattleRewards(result);
            ValidateBattleStatusDisplay(result);
            if (!File.Exists(RegionPath))
            {
                result.errors.Add("RegionCatalog.json 누락");
                return result;
            }

            var regions = JsonUtility.FromJson<RegionCatalogData>(File.ReadAllText(RegionPath))?.regions
                          ?? new List<RegionData>();
            result.regions = regions.Count;
            if (regions.Count != 40) result.errors.Add($"지역 수 불일치: {regions.Count}/40");
            ValidateRegionGraph(regions, result);
            ValidateRoster(result);
            ValidateDatabase(result);
            ValidateQuestRewards(result);
            LocalizationAuditResult localization = LocalizationCoverageValidator.Run(true);
            if (!localization.Passed)
                result.errors.Add($"현지화 커버리지 미완료: {localization.translated}/{localization.required} " +
                                  $"(키 {localization.missingKeys}, EN {localization.missingEnglish}, 포맷 {localization.formatErrors}) — " +
                                  LocalizationCoverageValidator.ReportPath);
            ValidateFinalArt(result);
            ValidateMusic(result);
            ValidateFrontEnd(result);
            ValidateScenes(regions, result);
            ValidateBuildSettings(regions, result);
            DeviceValidationReportUtility.ValidateImported(result.errors, result.warnings, requireDeviceReports);
            return result;
        }

        private static void ValidateDevicePolicy(ValidationReport result)
        {
            var sample = new DeviceValidationReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"), deviceModel = "Regression device", buildGuid = "regression-build",
                sessionSeconds = 1200f, averageFps = 25f, minimumOneSecondFps = 10f,
                sceneLoads = 5, uniqueScenes = 3,
                battlesCompleted = 3, savesCompleted = 3, pauseCount = 1, resumeCount = 1
            };
            if (DeviceValidationPolicy.Evaluate(sample).Count != 0)
                result.errors.Add("실기기 QA 정책: 최소 통과 기준 오류");
            if (!DeviceValidationReportUtility.IsFresh(sample, DateTime.UtcNow))
                result.errors.Add("실기기 QA 정책: 현재 리포트 유효기간 오류");
            sample.passed = true;
            sample.averageFps = float.NaN;
            if (DeviceValidationPolicy.Evaluate(sample).Count == 0)
                result.errors.Add("실기기 QA 정책: 잘못된 FPS가 통과함");
            sample.averageFps = 60f;
            sample.errorCount = 1;
            if (DeviceValidationPolicy.Evaluate(sample).Count == 0)
                result.errors.Add("실기기 QA 정책: 오류가 있는 PASS 리포트가 통과함");
            sample.errorCount = 0;
            sample.resumeCount = 0;
            if (DeviceValidationPolicy.Evaluate(sample).Count == 0)
                result.errors.Add("실기기 QA 정책: 복귀 미검증 리포트가 통과함");
            sample.resumeCount = 1;
            sample.uniqueScenes = 1;
            if (DeviceValidationPolicy.Evaluate(sample).Count == 0)
                result.errors.Add("실기기 QA 정책: 단일 지역 반복 리포트가 통과함");
            sample.generatedAtUtc = DateTime.UtcNow.AddDays(-15).ToString("O");
            if (DeviceValidationReportUtility.IsFresh(sample, DateTime.UtcNow))
                result.errors.Add("실기기 QA 정책: 만료 리포트가 통과함");
            sample.generatedAtUtc = DateTime.UtcNow.AddMinutes(11).ToString("O");
            if (DeviceValidationReportUtility.IsFresh(sample, DateTime.UtcNow))
                result.errors.Add("실기기 QA 정책: 미래 시각 리포트가 통과함");
        }

        private static void ValidateMigration(ValidationReport result)
        {
            var old = new SaveData
            {
                version = 1, mapId = "Prologue", checkpointMapId = "Prologue", gold = -10,
                party = new List<ShadowInstance>(), storage = new List<ShadowInstance>(),
                seenShadowIds = new List<string> { "knight", "knight", "" },
                recordedShadowIds = new List<string> { "knight" }
            };
            for (int i = 0; i < 8; i++) old.party.Add(new ShadowInstance
                { instanceId = i < 2 ? "duplicate" : "", shadowId = "test_" + i, level = 0, exp = -1 });
            SaveManager.Migrate(old, out _);
            if (old.version != SaveData.CurrentVersion || old.mapId != "PrologueTheater" ||
                old.party.Count != SaveData.MaxPartySize || old.storage.Count != 2 || old.gold != 0 ||
                old.party.Any(x => x.level < 1 || x.exp < 0) ||
                old.party.Concat(old.storage).Select(x => x.instanceId).Distinct().Count() != 8)
                result.errors.Add("v1 → 현재 세이브 마이그레이션 회귀 실패");
        }

        private static void ValidateBattleAi(ValidationReport result)
        {
            ShadowData flame = CreateAiTestShadow("ai_flame", ShadowElement.Flame);
            ShadowData frost = CreateAiTestShadow("ai_frost", ShadowElement.Frost);
            ShadowData shade = CreateAiTestShadow("ai_shade", ShadowElement.Shade);
            ShadowData neutral = CreateAiTestShadow("ai_neutral", ShadowElement.None);
            try
            {
                var flameUnit = new BattleUnit(new ShadowInstance(flame, 5), BattleSide.Enemy);
                var frostUnit = new BattleUnit(new ShadowInstance(frost, 5), BattleSide.Enemy);
                var shadeOpponent = new BattleUnit(new ShadowInstance(shade, 5), BattleSide.Player);
                var elementalParty = new List<BattleUnit> { flameUnit, frostUnit };
                if (BattleAI.ChooseSwitchIndex(elementalParty, 0, shadeOpponent) != 1)
                    result.errors.Add("전투 AI 불리 상성 교체 회귀 실패");
                if (BattleAI.ChooseSwitchIndex(elementalParty, 1, shadeOpponent) != -1)
                    result.errors.Add("전투 AI 유리 상성 유지 회귀 실패");

                var wounded = new ShadowInstance(neutral, 5) { currentHp = 1 };
                var healthy = new ShadowInstance(neutral, 5);
                var neutralOpponent = new BattleUnit(new ShadowInstance(neutral, 5), BattleSide.Player);
                var emergencyParty = new List<BattleUnit>
                {
                    new BattleUnit(wounded, BattleSide.Enemy),
                    new BattleUnit(healthy, BattleSide.Enemy)
                };
                if (BattleAI.ChooseSwitchIndex(emergencyParty, 0, neutralOpponent) != 1)
                    result.errors.Add("전투 AI 위험 HP 교체 회귀 실패");

                var statusSkill = ScriptableObject.CreateInstance<SkillData>();
                var damageSkill = ScriptableObject.CreateInstance<SkillData>();
                var healSkill = ScriptableObject.CreateInstance<SkillData>();
                try
                {
                    statusSkill.skillId = "ai_status";
                    statusSkill.damageType = DamageType.None;
                    statusSkill.damageMultiplier = 0f;
                    statusSkill.statusEffect = StatusEffectType.AttackDown;
                    statusSkill.statusChance = 1f;
                    damageSkill.skillId = "ai_damage";
                    damageSkill.damageMultiplier = .2f;
                    neutral.skills.Add(statusSkill);
                    neutral.skills.Add(damageSkill);
                    healSkill.skillId = "ai_boss_heal";
                    healSkill.damageType = DamageType.None;
                    healSkill.target = SkillTarget.Self;
                    healSkill.healRatio = .4f;
                    neutral.skills.Add(healSkill);

                    damageSkill.element = ShadowElement.Flame;
                    int strongDamage = DamageCalculator.EstimateDamage(flameUnit, frostUnit, damageSkill);
                    int weakDamage = DamageCalculator.EstimateDamage(flameUnit, shadeOpponent, damageSkill);
                    if (strongDamage <= weakDamage)
                        result.errors.Add("전투 기대 피해 속성 상성 회귀 실패");
                    damageSkill.element = ShadowElement.None;

                    BattleAction before = BattleAI.Choose(emergencyParty[1], neutralOpponent, 5);
                    neutralOpponent.ApplyStatus(StatusEffectType.AttackDown, 2);
                    BattleAction after = BattleAI.Choose(emergencyParty[1], neutralOpponent, 5);
                    if (before.skill != statusSkill || after.skill != damageSkill ||
                        !neutralOpponent.HasStatus(StatusEffectType.AttackDown))
                        result.errors.Add("전투 AI 중복 상태 이상 회피 회귀 실패");

                    var phaseInstance = new ShadowInstance(neutral, 5);
                    phaseInstance.currentHp = Mathf.RoundToInt(phaseInstance.MaxHp * .55f);
                    var phaseBoss = new BattleUnit(phaseInstance, BattleSide.Enemy);
                    BattleAction normal = BattleAI.Choose(phaseBoss, neutralOpponent, 5);
                    BattleAction desperate = BattleAI.Choose(phaseBoss, neutralOpponent, 5,
                        BattleAiProfile.BossDesperate);
                    if (normal.skill == healSkill || desperate.skill != healSkill)
                        result.errors.Add("보스 2페이즈 회복 우선순위 회귀 실패");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(statusSkill);
                    UnityEngine.Object.DestroyImmediate(damageSkill);
                    UnityEngine.Object.DestroyImmediate(healSkill);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(flame);
                UnityEngine.Object.DestroyImmediate(frost);
                UnityEngine.Object.DestroyImmediate(shade);
                UnityEngine.Object.DestroyImmediate(neutral);
            }
        }

        private static ShadowData CreateAiTestShadow(string id, ShadowElement element)
        {
            var data = ScriptableObject.CreateInstance<ShadowData>();
            data.shadowId = id;
            data.displayName = id;
            data.element = element;
            data.baseHp = 100;
            return data;
        }

        private static void ValidateBattleRewards(ValidationReport result)
        {
            if (DamageCalculator.SplitExperience(101, 3) != 33 ||
                DamageCalculator.SplitExperience(2, 6) != 1 ||
                DamageCalculator.SplitExperience(100, 0) != 0)
                result.errors.Add("참여 경험치 분배 회귀 실패");

            ShadowData data = CreateAiTestShadow("level_cap_test", ShadowElement.None);
            SkillData unlockSkill = ScriptableObject.CreateInstance<SkillData>();
            try
            {
                unlockSkill.skillId = "unlock_test";
                unlockSkill.displayName = "unlock_test";
                unlockSkill.requiredLevel = 5;
                data.skills.Add(unlockSkill);
                var learner = new ShadowInstance(data, 4);
                if (learner.GetAvailableSkills().Contains(unlockSkill))
                    result.errors.Add("기술 레벨 잠금 회귀 실패");
                learner.AddExp(learner.ExpToNext);
                if (!learner.GetAvailableSkills().Contains(unlockSkill))
                    result.errors.Add("기술 레벨 해금 회귀 실패");

                var instance = new ShadowInstance(data, ShadowInstance.MaxLevel - 1);
                int ups = instance.AddExp(instance.ExpToNext * 2);
                if (ups != 1 || instance.level != ShadowInstance.MaxLevel || instance.exp != 0 ||
                    instance.ExpToNext != 0 || !Mathf.Approximately(instance.ExpProgress, 1f) ||
                    instance.AddExp(9999) != 0)
                    result.errors.Add("그림자 최대 레벨·경험치 회귀 실패");

                var full = new BattleUnit(new ShadowInstance(data, 5), BattleSide.Enemy);
                var weakenedInstance = new ShadowInstance(data, 5) { currentHp = 1 };
                var weakened = new BattleUnit(weakenedInstance, BattleSide.Enemy);
                float fullChance = DamageCalculator.CaptureChance(full);
                float weakChance = DamageCalculator.CaptureChance(weakened);
                weakened.ApplyStatus(StatusEffectType.Freeze, 1);
                float frozenChance = DamageCalculator.CaptureChance(weakened);
                if (!(weakChance > fullChance && frozenChance > weakChance &&
                      DamageCalculator.CaptureChance(weakened, 99f) <= 1f))
                    result.errors.Add("각본 기록 성공률 계산 회귀 실패");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(unlockSkill);
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        private static void ValidateBattleStatusDisplay(ValidationReport result)
        {
            ShadowData data = CreateAiTestShadow("status_display_test", ShadowElement.None);
            try
            {
                var unit = new BattleUnit(new ShadowInstance(data, 5), BattleSide.Player);
                unit.ApplyStatus(StatusEffectType.Burn, 3);
                unit.ApplyStatus(StatusEffectType.AttackDown, 2);
                string summary = BattleStatusFormatter.Summary(unit);
                if (!summary.Contains("3T") || !summary.Contains("2T") || !unit.HasAnyStatus || unit.Debuffs.Count != 1)
                    result.errors.Add("전투 상태 이상 복합 표시 회귀 실패");
            }
            finally { UnityEngine.Object.DestroyImmediate(data); }
        }

        private static void ValidateRegionGraph(List<RegionData> regions, ValidationReport result)
        {
            var byId = new Dictionary<string, RegionData>(StringComparer.Ordinal);
            var scenes = new HashSet<string>(StringComparer.Ordinal);
            var orders = new HashSet<int>();
            foreach (var region in regions)
            {
                if (region == null || string.IsNullOrWhiteSpace(region.regionId))
                { result.errors.Add("빈 regionId"); continue; }
                if (byId.ContainsKey(region.regionId)) result.errors.Add("중복 regionId: " + region.regionId);
                else byId.Add(region.regionId, region);
                if (!scenes.Add(region.sceneName)) result.errors.Add("중복 sceneName: " + region.sceneName);
                if (!orders.Add(region.order)) result.errors.Add("중복 지역 순서: " + region.order);
                if (region.recommendedLevelMin > region.recommendedLevelMax)
                    result.errors.Add("권장 레벨 역전: " + region.regionId);
                if (!string.IsNullOrEmpty(region.requiredFlag) && !IsDeclaredStoryFlag(region.requiredFlag))
                    result.errors.Add($"{region.regionId}: 생성되지 않는 지역 해금 플래그 {region.requiredFlag}");
            }
            foreach (var region in regions.Where(x => x != null))
                foreach (string connected in region.connectedRegionIds ?? Array.Empty<string>())
                    if (!byId.ContainsKey(connected)) result.errors.Add($"{region.regionId}의 알 수 없는 연결: {connected}");

            if (regions.Count == 0) return;
            var reached = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            queue.Enqueue(regions.OrderBy(x => x.order).First().regionId);
            while (queue.Count > 0)
            {
                string id = queue.Dequeue();
                if (!reached.Add(id) || !byId.TryGetValue(id, out var region)) continue;
                foreach (string next in region.connectedRegionIds ?? Array.Empty<string>()) queue.Enqueue(next);
            }
            if (reached.Count != regions.Count) result.errors.Add($"월드맵 도달 가능 지역: {reached.Count}/{regions.Count}");
        }

        private static void ValidateRoster(ValidationReport result)
        {
            if (!File.Exists(RosterPath))
            { result.errors.Add("RegionalRosterManifest.json 누락 — 먼저 전체 생성을 실행하세요"); return; }
            var roster = JsonUtility.FromJson<RosterManifest>(File.ReadAllText(RosterPath));
            int count = roster?.entries?.Count ?? 0;
            if (count != 97) result.errors.Add($"지역 확장 로스터 수 불일치: {count}/97");
            if (roster?.entries != null && roster.entries.Select(x => x.shadowId).Distinct().Count() != count)
                result.errors.Add("지역 확장 로스터 shadowId 중복");
        }

        private static void ValidateDatabase(ValidationReport result)
        {
            var database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>(DatabasePath);
            if (database == null) { result.errors.Add("ShadowDatabase.asset 누락"); return; }
            if (database.items.Count(x => x != null) != 8)
                result.errors.Add($"도구 데이터 수 불일치: {database.items.Count(x => x != null)}/8");
            result.shadows = database.shadows.Count(x => x != null);
            if (result.shadows != 180) result.errors.Add($"그림자 데이터 수 불일치: {result.shadows}/180");
            var ids = database.shadows.Where(x => x != null).Select(x => x.shadowId).ToList();
            if (ids.Distinct().Count() != ids.Count) result.errors.Add("ShadowDatabase shadowId 중복");
            var ultimates = database.skills.Where(x => x != null && x.isUltimate).ToList();
            if (ultimates.Count == 0) result.errors.Add("진명 필살기 데이터 없음");
            foreach (SkillData skill in database.skills.Where(x => x != null))
            {
                if (skill.requiredLevel < 1 || skill.requiredLevel > ShadowInstance.MaxLevel)
                    result.errors.Add($"{skill.skillId}: 기술 해금 레벨 범위 오류 ({skill.requiredLevel})");
                if (skill.fpCost < 0 || skill.fpCost > 5)
                    result.errors.Add($"{skill.skillId}: FP 비용 범위 오류 ({skill.fpCost})");
                if (skill.accuracy < .65f || skill.accuracy > 1f)
                    result.errors.Add($"{skill.skillId}: 명중률 범위 오류 ({skill.accuracy})");
                if (skill.DealsDamage && (skill.damageMultiplier < .25f || skill.damageMultiplier > 4.25f))
                    result.errors.Add($"{skill.skillId}: 피해 계수 범위 오류 ({skill.damageMultiplier})");
                if (skill.target == SkillTarget.Self && (skill.healRatio <= 0f || skill.healRatio > .8f))
                    result.errors.Add($"{skill.skillId}: 회복 비율 범위 오류 ({skill.healRatio})");
            }
            foreach (SkillData skill in ultimates)
            {
                if (skill.ultimateFxStyle == UltimateFxStyle.None) result.errors.Add($"{skill.skillId}: 필살기 테마 누락");
                if (skill.fxPrefab == null) result.errors.Add($"{skill.skillId}: FX 프리팹 누락");
                if (skill.sfxClip == null) result.errors.Add($"{skill.skillId}: 캐스트 SFX 누락");
                if (skill.impactSfxClip == null) result.errors.Add($"{skill.skillId}: 타격 SFX 누락");
            }
            foreach (ItemData item in database.items.Where(x => x != null))
            {
                if (item.buyPrice <= 0) result.errors.Add($"{item.itemId}: 상점 구매 가격 누락");
                if (item.sellPrice < 0 || item.sellPrice > item.buyPrice)
                    result.errors.Add($"{item.itemId}: 판매 가격 범위 오류 ({item.sellPrice}/{item.buyPrice})");
                if (item.shopUnlockAct < 1 || item.shopUnlockAct > 8)
                    result.errors.Add($"{item.itemId}: 상점 해금 막 오류 ({item.shopUnlockAct})");
                if (item.icon == null) result.errors.Add($"{item.itemId}: 도구 아이콘 누락");
                if (item.usableInField && item.effectType != ItemEffectType.HealFlat && item.effectType != ItemEffectType.HealRatio)
                    result.warnings.Add($"{item.itemId}: 필드 사용 효과가 아직 지원되지 않음 ({item.effectType})");
            }
            var itemIds = new HashSet<string>(database.items.Where(x => x != null).Select(x => x.itemId));
            foreach (ShadowData shadow in database.shadows.Where(x => x != null))
            {
                if (string.IsNullOrEmpty(shadow.dropItemId) || !itemIds.Contains(shadow.dropItemId))
                    result.errors.Add($"{shadow.shadowId}: 전리품 도구 ID 누락 또는 오류 ({shadow.dropItemId})");
                if (shadow.dropChance <= 0f || shadow.dropChance > 1f)
                    result.errors.Add($"{shadow.shadowId}: 전리품 확률 오류 ({shadow.dropChance})");
                if (shadow.dropMinCount < 1 || shadow.dropMaxCount < shadow.dropMinCount)
                    result.errors.Add($"{shadow.shadowId}: 전리품 수량 범위 오류");
                if (shadow.baseHp < 50 || shadow.baseHp > 450 || shadow.baseAtk < 5 || shadow.baseAtk > 90 ||
                    shadow.baseDef < 0 || shadow.baseDef > 70 || shadow.baseSpd < 1 || shadow.baseSpd > 55)
                    result.errors.Add($"{shadow.shadowId}: 기본 능력치 밸런스 범위 오류");
                float minimumCaptureRate = shadow.growthTier == GrowthTier.Legendary ? .01f : .03f;
                if (shadow.IsCapturable && (shadow.baseCaptureRate < minimumCaptureRate || shadow.baseCaptureRate > .6f))
                    result.errors.Add($"{shadow.shadowId}: 기록 확률 범위 오류 ({shadow.baseCaptureRate})");

                bool rare = shadow.growthTier == GrowthTier.Rare;
                bool fullGrowth = shadow.growthTier == GrowthTier.RegionalBoss || shadow.growthTier == GrowthTier.Legendary;
                if ((rare || fullGrowth) && shadow.restoredForm?.enabled != true)
                    result.errors.Add($"{shadow.shadowId}: 등급 필수 기억 복원 데이터 누락");
                if (fullGrowth)
                {
                    ValidateTrueNameForm(shadow, shadow.salvationForm, "구원", result);
                    ValidateTrueNameForm(shadow, shadow.grudgeForm, "원한", result);
                }
                ValidateGrowthRequirement(shadow, shadow.restoredForm, "기억 복원", result);
                ValidateGrowthRequirement(shadow, shadow.salvationForm, "구원", result);
                ValidateGrowthRequirement(shadow, shadow.grudgeForm, "원한", result);
            }
        }

        private static void ValidateGrowthRequirement(ShadowData shadow, MemoryFormData form, string stage,
            ValidationReport result)
        {
            if (form?.enabled != true || string.IsNullOrEmpty(form.requiredFlag)) return;
            if (MemoryAwakeningService.HasStoryMilestoneAlias(form.requiredFlag)) return;
            if (!DeclaredStoryFlags.Contains(form.requiredFlag))
                result.errors.Add($"{shadow.shadowId}: {stage} 해금 플래그를 설정하는 스토리 없음 ({form.requiredFlag})");
        }

        private static string DeclaredStoryFlags => _declaredStoryFlags ??=
            File.ReadAllText("Assets/Resources/Data/DialogueCatalog.json") + "\n" +
            File.ReadAllText("Assets/Resources/Data/QuestCatalog.json") + "\n" +
            File.ReadAllText("Assets/Editor/PlayablePrologueGenerator.cs");

        private static string WorldSource => _worldSource ??=
            File.ReadAllText("Assets/Editor/PlayablePrologueGenerator.cs") + "\n" +
            File.ReadAllText("Assets/Scripts/Story/BossEncounterTrigger.cs");

        private static bool IsDeclaredStoryFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag) || DeclaredStoryFlags.Contains(flag)) return true;
            const string prefix = "quest_";
            const string suffix = "_complete";
            if (!flag.StartsWith(prefix, StringComparison.Ordinal) || !flag.EndsWith(suffix, StringComparison.Ordinal))
                return false;
            string questId = flag.Substring(prefix.Length, flag.Length - prefix.Length - suffix.Length);
            return File.ReadAllText(QuestPath).Contains($"\"questId\": \"{questId}\"");
        }

        private static void ValidateTrueNameForm(ShadowData shadow, MemoryFormData form, string path,
            ValidationReport result)
        {
            if (form?.enabled != true)
            {
                result.errors.Add($"{shadow.shadowId}: {path} 진명 데이터 누락");
                return;
            }
            if (form.bonusSkills == null || !form.bonusSkills.Any(x => x != null && x.isUltimate))
                result.errors.Add($"{shadow.shadowId}: {path} 진명 필살기 누락");
        }

        private static void ValidateQuestRewards(ValidationReport result)
        {
            var database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>(DatabasePath);
            if (database == null || !File.Exists(QuestPath)) return;
            var itemIds = new HashSet<string>(database.items.Where(x => x != null).Select(x => x.itemId));
            var catalog = JsonUtility.FromJson<QuestCatalogData>(File.ReadAllText(QuestPath));
            var quests = catalog?.quests ?? new List<QuestDefinition>();
            if (quests.Count != 47) result.errors.Add($"퀘스트 데이터 수 불일치: {quests.Count}/47");
            var mainQuests = quests.Where(x => x != null && !x.isSideQuest).ToList();
            var sideQuests = quests.Where(x => x != null && x.isSideQuest).ToList();
            if (mainQuests.Count != 41 || sideQuests.Count != 6)
                result.errors.Add($"메인/개인 기억 퀘스트 수 불일치: {mainQuests.Count}/41, {sideQuests.Count}/6");
            var ids = quests.Where(x => x != null && !string.IsNullOrEmpty(x.questId)).Select(x => x.questId).ToList();
            if (ids.Distinct().Count() != ids.Count) result.errors.Add("퀘스트 ID 중복");
            var byId = quests.Where(x => x != null && !string.IsNullOrEmpty(x.questId))
                .GroupBy(x => x.questId).ToDictionary(x => x.Key, x => x.First());
            foreach (QuestDefinition quest in quests.Where(x => x != null))
            {
                if (quest.rewardGold < 0) result.errors.Add($"{quest.questId}: 음수 금화 보상");
                if (quest.rewardItemCount < 0) result.errors.Add($"{quest.questId}: 음수 도구 보상");
                if (!string.IsNullOrEmpty(quest.rewardItemId) && !itemIds.Contains(quest.rewardItemId))
                    result.errors.Add($"{quest.questId}: 존재하지 않는 보상 도구 {quest.rewardItemId}");
                if (!string.IsNullOrEmpty(quest.prerequisiteQuestId) && !byId.ContainsKey(quest.prerequisiteQuestId))
                    result.errors.Add($"{quest.questId}: 선행 퀘스트 누락 {quest.prerequisiteQuestId}");
                if (!string.IsNullOrEmpty(quest.nextQuestId) && !byId.ContainsKey(quest.nextQuestId))
                    result.errors.Add($"{quest.questId}: 다음 퀘스트 누락 {quest.nextQuestId}");
                if (quest.isSideQuest && !MemoryAwakeningService.IsRegisteredMemoryQuest(quest.questId, quest.completionFlag))
                    result.errors.Add($"{quest.questId}: 개인 기억 성장 플래그 연결 오류 ({quest.completionFlag})");
                var objectiveIds = new HashSet<string>();
                if (quest.objectives == null || quest.objectives.Count == 0)
                    result.errors.Add($"{quest.questId}: 목표가 없습니다");
                foreach (QuestObjectiveDefinition objective in quest.objectives ?? new List<QuestObjectiveDefinition>())
                {
                    if (string.IsNullOrEmpty(objective.objectiveId) || !objectiveIds.Add(objective.objectiveId))
                        result.errors.Add($"{quest.questId}: 목표 ID 누락 또는 중복");
                    if (!objective.TryGetType(out _)) result.errors.Add($"{quest.questId}: 목표 타입 오류 {objective.type}");
                    if (string.IsNullOrEmpty(objective.targetId)) result.errors.Add($"{quest.questId}: 목표 대상 누락 ({objective.objectiveId})");
                    if (objective.TryGetType(out QuestObjectiveType type) && objective.targetId != "*")
                    {
                        if ((type == QuestObjectiveType.Talk || type == QuestObjectiveType.Reach ||
                             type == QuestObjectiveType.Defeat) && !WorldSource.Contains($"\"{objective.targetId}\""))
                            result.errors.Add($"{quest.questId}: 필드에 배치되지 않은 {type} 대상 {objective.targetId}");
                    }
                }
            }

            QuestDefinition start = mainQuests.FirstOrDefault(x => x.autoStart);
            if (mainQuests.Count(x => x.autoStart) != 1 || sideQuests.Any(x => x.autoStart))
                result.errors.Add("자동 시작 퀘스트는 정확히 1개여야 합니다");
            if (start == null) { result.errors.Add("자동 시작 퀘스트 누락"); return; }
            var reached = new HashSet<string>();
            QuestDefinition current = start;
            while (current != null && reached.Add(current.questId))
            {
                if (string.IsNullOrEmpty(current.nextQuestId)) { current = null; break; }
                if (!byId.TryGetValue(current.nextQuestId, out QuestDefinition next)) { current = null; break; }
                if (next.prerequisiteQuestId != current.questId)
                    result.errors.Add($"퀘스트 연결 불일치: {current.questId} → {next.questId}");
                current = next;
            }
            if (current != null) result.errors.Add($"퀘스트 순환 발견: {current.questId}");
            if (reached.Count != mainQuests.Count)
                result.errors.Add($"시작부터 도달 가능한 메인 퀘스트: {reached.Count}/{mainQuests.Count}");
        }

        private static void ValidateFinalArt(ValidationReport result)
        {
            if (!File.Exists(FinalPixelArtPipeline.ReportPath))
            {
                result.errors.Add("최종 픽셀 아트 커버리지 보고서 누락");
                return;
            }
            FinalArtCoverageReport report = JsonUtility.FromJson<FinalArtCoverageReport>(
                File.ReadAllText(FinalPixelArtPipeline.ReportPath));
            if (report == null) { result.errors.Add("최종 픽셀 아트 보고서 파싱 실패"); return; }
            if (report.shadowCount != 180) result.errors.Add($"픽셀 아트 대상 그림자 수 불일치: {report.shadowCount}/180");
            if (report.tileCount != 89) result.errors.Add($"최종 타일 대상 수 불일치: {report.tileCount}/89");
            foreach (string issue in report.invalid ?? new List<string>()) result.errors.Add("최종 아트 규격: " + issue);
            ValidatePixelArtImporters("Assets/Art/Final/Player", result);
            ValidatePixelArtImporters("Assets/Art/Final/Field", result);
            result.warnings.Add($"최종 아트 교체율 — 초상 {report.portraitsReady}/{report.shadowCount}, " +
                                $"필드 그림자 {report.fieldActorsReady}/{report.shadowCount}, " +
                                $"플레이어 {report.playerFramesReady}/6, 타일 {report.tilesReady}/{report.tileCount}");
        }

        private static void ValidatePixelArtImporters(string folder, ValidationReport result)
        {
            if (!Directory.Exists(folder)) return;
            foreach (string file in Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly))
            {
                string path = file.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    result.errors.Add("픽셀 아트 임포터 누락: " + path);
                    continue;
                }

                if (!Mathf.Approximately(importer.spritePixelsPerUnit, 32f))
                    result.errors.Add($"픽셀 아트 PPU 불일치: {path} ({importer.spritePixelsPerUnit}/32)");
                if (importer.filterMode != FilterMode.Point)
                    result.errors.Add("픽셀 아트 Point 필터 누락: " + path);
                if (importer.mipmapEnabled)
                    result.errors.Add("픽셀 아트 밉맵 활성화: " + path);
            }
        }

        private static void ValidateMusic(ValidationReport result)
        {
            GameObject core = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Systems/CoreSystems.prefab");
            AdaptiveMusicDirector director = core != null ? core.GetComponent<AdaptiveMusicDirector>() : null;
            if (director == null) { result.errors.Add("CoreSystems 적응형 음악 감독 누락"); return; }
            if (core.GetComponent<DeviceValidationRecorder>() == null)
                result.errors.Add("CoreSystems 실기기 QA 기록기 누락");
            SerializedProperty clips = new SerializedObject(director).FindProperty("clips");
            int expected = Enum.GetValues(typeof(MusicCue)).Length;
            if (clips == null || clips.arraySize != expected)
            { result.errors.Add($"음악 큐 수 불일치: {clips?.arraySize ?? 0}/{expected}"); return; }
            for (int i = 0; i < clips.arraySize; i++)
                if (clips.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    result.errors.Add($"음악 클립 누락: {(MusicCue)i}");
        }

        private static void ValidateFrontEnd(ValidationReport result)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/TitleCanvas.prefab");
            TitleScreenController title = prefab != null ? prefab.GetComponent<TitleScreenController>() : null;
            if (title == null)
            {
                result.errors.Add("TitleCanvas 또는 TitleScreenController 누락");
                return;
            }
            var so = new SerializedObject(title);
            foreach (string property in new[]
                     {
                         "titleRoot", "continueButton", "newCycleButton", "newGameConfirmRoot",
                         "saveSummaryText", "feedbackText", "starterSelection", "endingGallery", "settingsPanel"
                     })
                if (so.FindProperty(property)?.objectReferenceValue == null)
                    result.errors.Add($"타이틀 UI {property} 참조 누락");
        }

        private static void ValidateScenes(List<RegionData> regions, ValidationReport result)
        {
            var portalLinks = new List<PortalValidationLink>();
            foreach (var region in regions)
            {
                string path = $"{SceneFolder}/{region.sceneName}.unity";
                if (!File.Exists(path)) { result.errors.Add("씬 누락: " + path); continue; }
                Scene scene = SceneManager.GetSceneByPath(path);
                bool closeAfter = !scene.IsValid() || !scene.isLoaded;
                if (closeAfter) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                result.scenes++;
                try
                {
                    var roots = scene.GetRootGameObjects();
                    FieldGrid fieldGrid = roots.SelectMany(x => x.GetComponentsInChildren<FieldGrid>(true)).FirstOrDefault();
                    if (fieldGrid == null) result.errors.Add($"{region.sceneName}: FieldGrid 누락");
                    else
                    {
                        var gridSo = new SerializedObject(fieldGrid);
                        if (gridSo.FindProperty("groundTilemap")?.objectReferenceValue == null ||
                            gridSo.FindProperty("collisionTilemap")?.objectReferenceValue == null)
                            result.errors.Add($"{region.sceneName}: 바닥/충돌 Tilemap 참조 누락");
                        if (!fieldGrid.TryFindNearestWalkable(region.ArrivalCell, out Vector2Int walkableArrival,
                                null, 12, false) ||
                            walkableArrival != region.ArrivalCell)
                            result.errors.Add($"{region.sceneName}: 월드맵 도착 좌표가 막힘 {region.ArrivalCell}");
                    }
                    if (!roots.Any(x => x.GetComponentInChildren<PlayerController>(true) != null)) result.errors.Add($"{region.sceneName}: Player 누락");
                    if (!roots.Any(x => x.GetComponentInChildren<GameFlowController>(true) != null)) result.errors.Add($"{region.sceneName}: GameFlow 누락");
                    GameObject fieldRoot = roots.FirstOrDefault(x => x.GetComponentInChildren<FieldGrid>(true) != null);
                    BattleUIController battleUi = roots.SelectMany(x => x.GetComponentsInChildren<BattleUIController>(true)).FirstOrDefault();
                    if (battleUi == null) result.errors.Add($"{region.sceneName}: 전투 UI 누락");
                    else
                    {
                        CanvasScaler battleScaler = battleUi.GetComponent<CanvasScaler>();
                        if (battleScaler == null || battleScaler.referenceResolution != new Vector2(1920f, 1080f))
                            result.errors.Add($"{region.sceneName}: 전투 UI 가로 기준 해상도 불일치");
                        if (battleUi.GetComponentInChildren<MobileSafeArea>(true) == null)
                            result.errors.Add($"{region.sceneName}: 전투 UI 안전영역 보정 누락");
                        var battleSo = new SerializedObject(battleUi);
                        if (battleSo.FindProperty("playerPartyStrip")?.objectReferenceValue == null ||
                            battleSo.FindProperty("enemyPartyStrip")?.objectReferenceValue == null ||
                            battleSo.FindProperty("optionContent")?.objectReferenceValue == null ||
                            battleSo.FindProperty("messageRoot")?.objectReferenceValue == null ||
                            battleSo.FindProperty("optionPromptText")?.objectReferenceValue == null ||
                            battleSo.FindProperty("resultRoot")?.objectReferenceValue == null ||
                            battleSo.FindProperty("resultGroup")?.objectReferenceValue == null ||
                            battleSo.FindProperty("resultTitleText")?.objectReferenceValue == null ||
                            battleSo.FindProperty("resultSummaryText")?.objectReferenceValue == null ||
                            battleSo.FindProperty("resultDetailsText")?.objectReferenceValue == null ||
                            battleSo.FindProperty("tutorialRoot")?.objectReferenceValue == null ||
                            battleSo.FindProperty("tutorialStepText")?.objectReferenceValue == null ||
                            battleSo.FindProperty("tutorialTitleText")?.objectReferenceValue == null ||
                            battleSo.FindProperty("tutorialBodyText")?.objectReferenceValue == null ||
                            battleSo.FindProperty("recordText")?.objectReferenceValue == null)
                            result.errors.Add($"{region.sceneName}: 전투 파티 스트립·선택 목록·메시지·결과 카드·튜토리얼·기록 확률 참조 누락");
                        foreach (BattlePartyStrip strip in battleUi.GetComponentsInChildren<BattlePartyStrip>(true))
                        {
                            SerializedProperty slots = new SerializedObject(strip).FindProperty("slots");
                            if (slots == null || slots.arraySize != SaveData.MaxPartySize)
                                result.errors.Add($"{region.sceneName}: 전투 파티 스트립 슬롯 수 불일치");
                        }
                        foreach (BattleUnitPanel panel in battleUi.GetComponentsInChildren<BattleUnitPanel>(true))
                        {
                            var panelSo = new SerializedObject(panel);
                            if (panelSo.FindProperty("elementText")?.objectReferenceValue == null ||
                                panelSo.FindProperty("statusText")?.objectReferenceValue == null)
                                result.errors.Add($"{region.sceneName}: 전투 유닛 속성·상태 UI 참조 누락");
                        }
                    }
                    DialogueController dialogue = roots.SelectMany(x => x.GetComponentsInChildren<DialogueController>(true)).FirstOrDefault();
                    if (dialogue == null) result.errors.Add($"{region.sceneName}: 대화 UI 누락");
                    else
                    {
                        var dialogueSo = new SerializedObject(dialogue);
                        if (dialogueSo.FindProperty("dialogueBox")?.objectReferenceValue == null ||
                            dialogueSo.FindProperty("dialogueBoxImage")?.objectReferenceValue == null ||
                            dialogueSo.FindProperty("accentBar")?.objectReferenceValue == null)
                            result.errors.Add($"{region.sceneName}: 감정 대화 연출 참조 누락");
                    }
                    if (fieldRoot != null)
                    {
                        foreach (Canvas fieldCanvas in roots.SelectMany(x => x.GetComponentsInChildren<Canvas>(true))
                                     .Where(x => x.GetComponent<BattleUIController>() == null &&
                                                 x.GetComponent<ScreenFader>() == null))
                        {
                            if (!fieldCanvas.transform.IsChildOf(fieldRoot.transform))
                                result.errors.Add($"{region.sceneName}: 전투 중 남을 수 있는 필드 Canvas: {fieldCanvas.name}");
                        }
                    }
                    RegionArrivalBanner arrival = roots.SelectMany(x => x.GetComponentsInChildren<RegionArrivalBanner>(true)).FirstOrDefault();
                    if (arrival == null) result.errors.Add($"{region.sceneName}: 지역 진입 타이틀 누락");
                    else
                    {
                        var arrivalSo = new SerializedObject(arrival);
                        foreach (string property in new[] { "root", "card", "accent", "actText", "regionText", "environmentText" })
                            if (arrivalSo.FindProperty(property)?.objectReferenceValue == null)
                                result.errors.Add($"{region.sceneName}: 지역 진입 타이틀 {property} 참조 누락");
                    }
                    PartyStorageController partyStorage = roots.SelectMany(x => x.GetComponentsInChildren<PartyStorageController>(true)).FirstOrDefault();
                    if (partyStorage == null) result.errors.Add($"{region.sceneName}: 파티·각본 서고 UI 누락");
                    else
                    {
                        var partySo = new SerializedObject(partyStorage);
                        if (partySo.FindProperty("root")?.objectReferenceValue == null ||
                            partySo.FindProperty("partyContent")?.objectReferenceValue == null ||
                            partySo.FindProperty("storageContent")?.objectReferenceValue == null ||
                            partySo.FindProperty("healButton")?.objectReferenceValue == null)
                            result.errors.Add($"{region.sceneName}: 파티·각본 서고 UI 참조 누락");
                        foreach (string property in new[] { "partyTemplate", "storageTemplate" })
                        {
                            var entry = partySo.FindProperty(property)?.objectReferenceValue as PartyStorageEntryView;
                            if (entry == null) continue;
                            var entrySo = new SerializedObject(entry);
                            if (entrySo.FindProperty("expFill")?.objectReferenceValue == null ||
                                entrySo.FindProperty("expText")?.objectReferenceValue == null)
                                result.errors.Add($"{region.sceneName}: 파티·각본 서고 경험치 UI 참조 누락");
                        }
                    }
                    SettlementShopController shop = roots.SelectMany(x => x.GetComponentsInChildren<SettlementShopController>(true)).FirstOrDefault();
                    if (shop == null) result.errors.Add($"{region.sceneName}: 정착지 상점 UI 누락");
                    else
                    {
                        var shopSo = new SerializedObject(shop);
                        if (shopSo.FindProperty("root")?.objectReferenceValue == null ||
                            shopSo.FindProperty("openButton")?.objectReferenceValue == null ||
                            shopSo.FindProperty("contentRoot")?.objectReferenceValue == null ||
                            shopSo.FindProperty("itemTemplate")?.objectReferenceValue == null ||
                            shopSo.FindProperty("restButton")?.objectReferenceValue == null)
                            result.errors.Add($"{region.sceneName}: 정착지 상점 UI 참조 누락");
                    }
                    FieldInventoryController inventory = roots.SelectMany(x => x.GetComponentsInChildren<FieldInventoryController>(true)).FirstOrDefault();
                    if (inventory == null) result.errors.Add($"{region.sceneName}: 필드 도구 가방 UI 누락");
                    else
                    {
                        var inventorySo = new SerializedObject(inventory);
                        foreach (string property in new[] { "root", "itemContent", "targetContent", "itemTemplate", "targetTemplate", "useButton" })
                            if (inventorySo.FindProperty(property)?.objectReferenceValue == null)
                                result.errors.Add($"{region.sceneName}: 도구 가방 {property} 참조 누락");
                    }
                    QuestLogController questLog = roots.SelectMany(x => x.GetComponentsInChildren<QuestLogController>(true)).FirstOrDefault();
                    if (questLog == null) result.errors.Add($"{region.sceneName}: 퀘스트 기록장 UI 누락");
                    else
                    {
                        var questLogSo = new SerializedObject(questLog);
                        foreach (string property in new[] { "root", "contentRoot", "entryTemplate", "titleText", "objectivesText", "rewardText" })
                            if (questLogSo.FindProperty(property)?.objectReferenceValue == null)
                                result.errors.Add($"{region.sceneName}: 퀘스트 기록장 {property} 참조 누락");
                    }
                    FieldPauseMenuController pauseMenu = roots.SelectMany(x => x.GetComponentsInChildren<FieldPauseMenuController>(true)).FirstOrDefault();
                    if (pauseMenu == null) result.errors.Add($"{region.sceneName}: 필드 메뉴 누락");
                    else
                    {
                        var pauseSo = new SerializedObject(pauseMenu);
                        if (pauseSo.FindProperty("root")?.objectReferenceValue == null ||
                            pauseSo.FindProperty("masterSlider")?.objectReferenceValue == null ||
                            pauseSo.FindProperty("recoverPositionButton")?.objectReferenceValue == null ||
                            pauseSo.FindProperty("titleConfirmRoot")?.objectReferenceValue == null)
                            result.errors.Add($"{region.sceneName}: 필드 메뉴 참조 누락");
                    }
                    SaveFeedbackController saveFeedback = roots.SelectMany(x => x.GetComponentsInChildren<SaveFeedbackController>(true)).FirstOrDefault();
                    if (saveFeedback == null) result.errors.Add($"{region.sceneName}: 저장 완료 HUD 누락");
                    else
                    {
                        var feedbackSo = new SerializedObject(saveFeedback);
                        if (feedbackSo.FindProperty("root")?.objectReferenceValue == null ||
                            feedbackSo.FindProperty("label")?.objectReferenceValue == null)
                            result.errors.Add($"{region.sceneName}: 저장 완료 HUD 참조 누락");
                    }
                    QuestCompletionToast questToast = roots.SelectMany(x => x.GetComponentsInChildren<QuestCompletionToast>(true)).FirstOrDefault();
                    if (questToast == null) result.errors.Add($"{region.sceneName}: 퀘스트 완료 알림 누락");
                    else
                    {
                        var toastSo = new SerializedObject(questToast);
                        if (toastSo.FindProperty("root")?.objectReferenceValue == null ||
                            toastSo.FindProperty("titleText")?.objectReferenceValue == null ||
                            toastSo.FindProperty("rewardText")?.objectReferenceValue == null)
                            result.errors.Add($"{region.sceneName}: 퀘스트 완료 알림 참조 누락");
                    }
                    FieldAmbientAudio ambience = roots.SelectMany(x => x.GetComponentsInChildren<FieldAmbientAudio>(true)).FirstOrDefault();
                    if (ambience == null) result.errors.Add($"{region.sceneName}: 환경음 컨트롤러 누락");
                    else
                    {
                        var ambienceSo = new SerializedObject(ambience);
                        if (ambienceSo.FindProperty("ambienceClip")?.objectReferenceValue == null)
                            result.errors.Add($"{region.sceneName}: 지속 환경음 누락");
                        if (ambienceSo.FindProperty("detailClip")?.objectReferenceValue == null)
                            result.errors.Add($"{region.sceneName}: 간헐 환경음 누락");
                    }
                    FieldFootstepAudio footsteps = roots.SelectMany(x => x.GetComponentsInChildren<FieldFootstepAudio>(true)).FirstOrDefault();
                    if (footsteps == null) result.errors.Add($"{region.sceneName}: 발걸음 오디오 누락");
                    else
                    {
                        SerializedProperty clips = new SerializedObject(footsteps).FindProperty("clips");
                        if (clips == null || clips.arraySize < 4 || Enumerable.Range(0, clips.arraySize)
                            .Any(i => clips.GetArrayElementAtIndex(i).objectReferenceValue == null))
                            result.errors.Add($"{region.sceneName}: 발걸음 클립 누락");
                    }
                    foreach (GameObject root in roots)
                    foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject) > 0)
                            result.errors.Add($"{region.sceneName}: Missing Script ({GetPath(tr)})");

                    var portals = roots.SelectMany(x => x.GetComponentsInChildren<MapPortal>(true)).ToArray();
                    var encounters = roots.SelectMany(x => x.GetComponentsInChildren<EncounterSymbol>(true)).ToArray();
                    result.portals += portals.Length;
                    result.encounters += encounters.Length;
                    if (portals.Length == 0) result.errors.Add($"{region.sceneName}: 포털 없음");
                    foreach (MapPortal portal in portals)
                    {
                        var so = new SerializedObject(portal);
                        string target = so.FindProperty("targetScene")?.stringValue;
                        if (string.IsNullOrEmpty(target) || !regions.Any(x => x.sceneName == target))
                            result.errors.Add($"{region.sceneName}: 잘못된 포털 대상 {target}");
                        else
                        {
                            SerializedProperty arrivalProperty = so.FindProperty("arrivalCell");
                            if (arrivalProperty != null)
                                portalLinks.Add(new PortalValidationLink
                                {
                                    sourceScene = region.sceneName,
                                    portalName = portal.name,
                                    targetScene = target,
                                    arrivalCell = arrivalProperty.vector2IntValue
                                });
                        }
                    }
                    foreach (EncounterSymbol encounter in encounters)
                    {
                        var so = new SerializedObject(encounter);
                        if (so.FindProperty("leadShadow")?.objectReferenceValue == null)
                            result.errors.Add($"{region.sceneName}: {encounter.name} 적 데이터 누락");
                    }
                }
                finally { if (closeAfter) EditorSceneManager.CloseScene(scene, true); }
            }
            ValidatePortalArrivals(portalLinks, result);
        }

        private static void ValidatePortalArrivals(List<PortalValidationLink> links, ValidationReport result)
        {
            foreach (var group in links.GroupBy(x => x.targetScene))
            {
                string path = $"{SceneFolder}/{group.Key}.unity";
                if (!File.Exists(path)) continue;
                Scene scene = SceneManager.GetSceneByPath(path);
                bool closeAfter = !scene.IsValid() || !scene.isLoaded;
                if (closeAfter) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    FieldGrid grid = scene.GetRootGameObjects()
                        .SelectMany(x => x.GetComponentsInChildren<FieldGrid>(true)).FirstOrDefault();
                    if (grid == null) continue;
                    foreach (PortalValidationLink link in group)
                    {
                        if (grid.TryFindNearestWalkable(link.arrivalCell, out Vector2Int resolved,
                                null, 12, false) && resolved == link.arrivalCell) continue;
                        result.errors.Add($"{link.sourceScene}/{link.portalName}: {link.targetScene} 도착 좌표가 막힘 {link.arrivalCell}");
                    }
                }
                finally { if (closeAfter) EditorSceneManager.CloseScene(scene, true); }
            }
        }

        private static void ValidateBuildSettings(List<RegionData> regions, ValidationReport result)
        {
            var enabled = new HashSet<string>(EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path));
            string title = $"{SceneFolder}/Title.unity";
            if (!enabled.Contains(title)) result.errors.Add("Build Settings: Title 씬 누락");
            foreach (var region in regions)
            {
                string path = $"{SceneFolder}/{region.sceneName}.unity";
                if (!enabled.Contains(path)) result.errors.Add("Build Settings 씬 누락: " + region.sceneName);
            }
        }

        private static string GetPath(Transform target)
        {
            string path = target.name;
            while (target.parent != null) { target = target.parent; path = target.name + "/" + path; }
            return path;
        }

        [Serializable] private class RosterManifest { public List<RosterEntry> entries; }
        [Serializable] private class RosterEntry { public string shadowId, regionId, sceneName; public int minLevel, maxLevel; }
        private class PortalValidationLink
        {
            public string sourceScene, portalName, targetScene;
            public Vector2Int arrivalCell;
        }
        private class ValidationReport
        {
            public int regions, scenes, shadows, portals, encounters;
            public readonly List<string> errors = new List<string>();
            public readonly List<string> warnings = new List<string>();
        }
    }
}
#endif
