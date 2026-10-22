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
    /// <summary>180종 데이터와 63개 필드의 생성 결과를 출시 전 일괄 검사한다.</summary>
    public static class GameRegressionValidator
    {
        private const string SceneFolder = "Assets/Scenes/Prologue";
        private const string RegionPath = "Assets/Resources/Data/RegionCatalog.json";
        private const string RosterPath = "Assets/Resources/Data/RegionalRosterManifest.json";
        private const string DatabasePath = "Assets/Resources/ShadowDatabase.asset";
        private const string QuestPath = "Assets/Resources/Data/QuestCatalog.json";
        private const string CompanionEventPath = "Assets/Resources/Data/CompanionEventCatalog.json";
        private const string DialoguePath = "Assets/Resources/Data/DialogueCatalog.json";
        private const string BossPhasePath = "Assets/Resources/Data/BossPhaseCatalog.json";
        private const string EndingPath = "Assets/Resources/Data/EndingCatalog.json";
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
            foreach (string warning in report.warnings) Debug.LogWarning("[Regression] " + warning);
            if (report.errors.Count > 0)
                throw new BuildFailedException("Shadow Theater regression failed:\n" + string.Join("\n", report.errors));
            Debug.Log($"[Regression] PASS: {report.regions} regions, {report.scenes} fields, " +
                      $"{report.shadows} shadows, {report.portals} portals, {report.encounters} encounters");
        }

        public static void GenerateAndValidateForCi()
        {
            MobileBuildConfigurator.Apply();
            PlayablePrologueGenerator.Generate();
            ValidateForCi();
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

        public static CompletionAuditSnapshot CollectCompletionAudit()
        {
            ValidationReport report=Run(false);
            return new CompletionAuditSnapshot(report.regions,report.scenes,report.shadows,report.portals,report.encounters,
                new List<string>(report.errors),new List<string>(report.warnings));
        }

        private static ValidationReport Run(bool requireDeviceReports)
        {
            var result = new ValidationReport();
            ValidateScriptBindings(result);
            ValidateDevicePolicy(result);
            CoreContentBatchGenerator.ValidateGrowthPolicyRegression(result.errors);
            CombatBalanceAnalyzer.ValidatePolicyRegression(result.errors);
            RegionDifficultySimulator.ValidatePolicyRegression(result.errors);
            LocalizationTranslationImporter.ValidateRegression(result.errors);
            MobileBuildConfigurator.ValidateScenePolicyRegression(result.errors);
            StandaloneBuildConfigurator.ValidateScenePolicyRegression(result.errors);
            MobileBuildConfigurator.ValidateSettings(result.errors);
            ValidateMigration(result);
            QuestRecordRegressionChecks.Validate(result.errors);
            ExplorationRegressionChecks.Validate(result.errors);
            AssetCompletionBatch.Validate(result.errors);
            ShadowRosterArtBatch.Validate(result.errors);
            ValidateBattleAi(result);
            ValidateBattleActions(result);
            ValidateBattleMomentum(result);
            ValidateBattlePerformance(result);
            ValidateBattleRewards(result);
            ValidateBattleStatusDisplay(result);
            ValidateHiddenBattleGimmicks(result);
            ValidateRegionalEncounterPolicy(result);
            ValidateCompanionPassives(result);
            ValidateCompanionBondsAndWorldProgress(result);
            ValidateExplorationRewards(result);
            ValidateCollectionRewards(result);
            ValidateJourneyOverviewPolicy(result);
            ValidateBossPhases(result);
            ValidateFieldWorldEffects(result);
            ValidateFieldTraversalEffects(result);
            if (!File.Exists(RegionPath))
            {
                result.errors.Add("RegionCatalog.json 누락");
                return result;
            }

            var regions = JsonUtility.FromJson<RegionCatalogData>(File.ReadAllText(RegionPath))?.regions
                          ?? new List<RegionData>();
            result.regions = regions.Count;
            if (regions.Count != 63) result.errors.Add($"지역 수 불일치: {regions.Count}/63");
            if (regions.Count(x => x.isSettlement) != 7)
                result.errors.Add($"극단 마을 수 불일치: {regions.Count(x => x.isSettlement)}/7");
            ValidateRegionGraph(regions, result);
            ValidateRoster(regions, result);
            ValidateDatabase(result);
            ValidateTownServices(regions, result);
            ValidateCompanionEvents(regions, result);
            ValidateQuestRewards(regions, result);
            ValidateStoryCompletion(result);
            LocalizationAuditResult localization = LocalizationCoverageValidator.Run(true);
            if (!localization.Passed)
                result.errors.Add($"현지화 커버리지 미완료: {localization.translated}/{localization.required} " +
                                  $"(키 {localization.missingKeys}, KO {localization.missingKorean}, 포맷 {localization.formatErrors}) — " +
                                  LocalizationCoverageValidator.ReportPath);
            ValidateFinalArt(result);
            ValidateMusic(result);
            ReleaseAssetPipeline.Validate(result.errors, result.warnings);
            ValidateFrontEnd(result);
            ValidateScenes(regions, result);
            ValidateBuildSettings(regions, result);
            DeviceValidationReportUtility.ValidateImported(result.errors, result.warnings, requireDeviceReports);
            return result;
        }

        private static void ValidateScriptBindings(ValidationReport result)
        {
            var scriptTypes = new HashSet<Type>(AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Scripts" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid))?.GetClass())
                .Where(type => type != null));
            foreach (Type type in typeof(GameFlowController).Assembly.GetTypes())
                if (!type.IsAbstract && typeof(MonoBehaviour).IsAssignableFrom(type) &&
                    type.Namespace?.StartsWith("ShadowTheater", StringComparison.Ordinal) == true && !scriptTypes.Contains(type))
                    result.errors.Add($"Unity 스크립트 연결 누락: {type.FullName} — 클래스 이름과 같은 .cs 파일이 필요합니다");
        }

        private static void ValidateCompanionPassives(ValidationReport result)
        {
            var flameDealer = ScriptableObject.CreateInstance<ShadowData>();
            flameDealer.shadowId = "passive_flame";
            flameDealer.element = ShadowElement.Flame;
            flameDealer.role = ShadowRole.PhysicalDealer;
            CompanionPassiveSnapshot offense = CompanionBondService.From(flameDealer);
            UnityEngine.Object.DestroyImmediate(flameDealer);
            if (offense.safeStepBonus != 1 || offense.attackMultiplier <= 1f || offense.criticalBonus <= 0f ||
                offense.captureMultiplier != 1f)
                result.errors.Add("동행 패시브: 불꽃/물리 역할 보정 오류");

            var frostSupport = ScriptableObject.CreateInstance<ShadowData>();
            frostSupport.shadowId = "passive_frost";
            frostSupport.element = ShadowElement.Frost;
            frostSupport.role = ShadowRole.Support;
            CompanionPassiveSnapshot support = CompanionBondService.From(frostSupport);
            if (support.fieldHealingMultiplier <= 1f || support.defenseMultiplier <= 1f ||
                support.captureMultiplier <= 1f || support.attackMultiplier != 1f)
                result.errors.Add("동행 패시브: 서리/지원 역할 보정 오류");
            if (!CompanionBondService.Describe(frostSupport).Contains("필드 회복") ||
                !CompanionBondService.Describe(frostSupport).Contains("기록률"))
                result.errors.Add("동행 패시브: UI 설명 누락");
            UnityEngine.Object.DestroyImmediate(frostSupport);
        }

        private static void ValidateCompanionBondsAndWorldProgress(ValidationReport result)
        {
            if (CompanionBondService.LevelForExperience(0) != 1 ||
                CompanionBondService.LevelForExperience(120) != 3 ||
                CompanionBondService.LevelForExperience(480) != CompanionBondService.MaxBondLevel)
                result.errors.Add("동행 유대: 경험치 레벨 경계 오류");
            if (BossInvestigationService.WeaknessOf(ShadowElement.Flame) != ShadowElement.Shade ||
                BossInvestigationService.ReducePhaseFp(2, 3) != 1 ||
                BossInvestigationService.PhaseHealMultiplier(3) >= 1f)
                result.errors.Add("보스 사전 조사: 약점·페이즈 완화 정책 오류");
            var journalSample=new BossInvestigationSnapshot("boss_test","시험 보스",ShadowElement.Frost,
                "서리 흔적","체력 절반에 전환",2,3,"boss_room","숨은 무대","첫 극단 마을");
            if(!BossInvestigationService.ClueSource(journalSample,1).Contains("첫 극단 마을")||
               !BossInvestigationService.ClueSource(journalSample,2).Contains("숨은 무대")||
               BossInvestigationService.ClueText(journalSample,3)!="아직 발견하지 못한 단서")
                result.errors.Add("보스 조사 수첩: 단서 공개·NPC/지역 출처 정책 오류");
            if (TownRestorationService.StageFor(false, 99) != 0 ||
                TownRestorationService.StageFor(true, 3) != 1 ||
                TownRestorationService.StageFor(true, 6) != 2 ||
                TownRestorationService.StageFor(true, 10) != 3)
                result.errors.Add("마을 복구: 단계 경계 오류");
            var incident = new FieldIncidentDefinition("test", "test", "test", FieldTimeOfDay.Night,
                FieldWeather.Starfall, 3, 0, string.Empty, 0, 0);
            var region = new RegionData { act = 3 };
            if (!FieldIncidentService.Matches(incident, region,
                    new FieldWorldSnapshot(FieldTimeOfDay.Night, FieldWeather.Starfall, 0)) ||
                FieldIncidentService.Matches(incident, region,
                    new FieldWorldSnapshot(FieldTimeOfDay.Day, FieldWeather.Starfall, 0)))
                result.errors.Add("필드 사건: 시간·날씨 조건 판정 오류");
            if (string.IsNullOrWhiteSpace(incident.trackChoice) || string.IsNullOrWhiteSpace(incident.observeChoice))
                result.errors.Add("필드 사건: 선택지 정의 누락");
            ShadowBattleSimulator.ValidatePolicyRegression(result.errors);
        }

        private static void ValidateBossPhases(ValidationReport result)
        {
            if (!File.Exists(BossPhasePath))
            { result.errors.Add("BossPhaseCatalog.json 누락"); return; }
            BossPhaseCatalogData catalog = JsonUtility.FromJson<BossPhaseCatalogData>(File.ReadAllText(BossPhasePath));
            List<BossPhaseDefinition> phases = catalog?.phases ?? new List<BossPhaseDefinition>();
            string[] expected =
            {
                "kneeling_captain", "headless_guard", "ash_king", "book_drake", "inverted_librarian",
                "infinite_archive_dragon", "bloodmane_stalker", "night_devouring_beast", "wire_primadonna",
                "last_puppeteer", "zero_warden", "extract_subject_m", "high_censor_nox", "drowned_painter",
                "mourning_keeper", "interlude_manager", "rival_director", "outside_script_shadow"
            };
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var phaseNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (BossPhaseDefinition phase in phases)
            {
                if (phase == null || string.IsNullOrWhiteSpace(phase.phaseId) ||
                    string.IsNullOrWhiteSpace(phase.bossShadowId) || !ids.Add(phase.bossShadowId))
                { result.errors.Add("보스 페이즈: ID 누락 또는 대상 중복"); continue; }
                if (phase.hpThreshold < .1f || phase.hpThreshold > .9f || string.IsNullOrWhiteSpace(phase.message) ||
                    phase.enemyFpGain + phase.playerFpDrain <= 0 && phase.healRatio <= 0f &&
                    !phase.cleanseBoss && !phase.TryGetPlayerStatus(out _))
                    result.errors.Add($"보스 페이즈 설정 오류: {phase.phaseId}");
                if (!string.IsNullOrWhiteSpace(phase.playerStatus) && !phase.TryGetPlayerStatus(out _))
                    result.errors.Add($"보스 페이즈 상태 이상 오류: {phase.phaseId}");
                if (string.IsNullOrWhiteSpace(phase.PhaseName) || !phaseNames.Add(phase.PhaseName))
                    result.errors.Add($"보스 페이즈 연출명 누락 또는 중복: {phase.phaseId}");
            }
            if (phases.Count != 18 || expected.Any(x => !ids.Contains(x)))
                result.errors.Add($"주요 보스 개별 페이즈 수·대상 불일치: {phases.Count}/18");
        }

        private static void ValidateFieldWorldEffects(ValidationReport result)
        {
            var clearDay = new FieldWorldSnapshot(FieldTimeOfDay.Day, FieldWeather.Clear, 0);
            var inkNight = new FieldWorldSnapshot(FieldTimeOfDay.Night, FieldWeather.InkStorm, 0);
            var dawnRain = new FieldWorldSnapshot(FieldTimeOfDay.Dawn, FieldWeather.Rain, 0);
            if (FieldWorldState.EncounterRateMultiplier(inkNight) <=
                FieldWorldState.EncounterRateMultiplier(clearDay))
                result.errors.Add("필드 효과: 밤 먹물 폭풍 조우율 보정 오류");
            if (FieldWorldState.MovementDurationMultiplier(inkNight) <= 1f ||
                FieldWorldState.FieldHealingMultiplier(dawnRain) <= 1f)
                result.errors.Add("필드 효과: 이동·회복 날씨 보정 오류");
            if (string.IsNullOrWhiteSpace(FieldWorldState.RareEncounterSignal(
                    new FieldWorldSnapshot(FieldTimeOfDay.Night, FieldWeather.Starfall, 0))))
                result.errors.Add("필드 효과: 희귀 조우 알림 누락");
            WeatherEffectProfile rain = FieldWeatherEffects.Profile(FieldWeather.Rain);
            WeatherEffectProfile clear = FieldWeatherEffects.Profile(FieldWeather.Clear);
            if (rain.rate <= 0f || rain.speed >= 0f || rain.volume <= 0f || clear.rate != 0f ||
                FieldWeatherEffects.SynthesizeSamples(FieldWeather.InkStorm).Length != FieldWeatherEffects.SampleRate * 4)
                result.errors.Add("필드 효과: 날씨 파티클·환경음 정책 오류");
        }

        private static void ValidateDevicePolicy(ValidationReport result)
        {
            var sample = new DeviceValidationReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"), deviceModel = "Regression device", buildGuid = "regression-build",
                sessionSeconds = 1200f, averageFps = 25f, minimumOneSecondFps = 10f,
                metricsRevision = 2, frameSamples = 30000, maximumFrameSeconds = 0.04f,
                sceneLoads = 5, uniqueScenes = 3,
                battlesCompleted = 3, savesCompleted = 3, pauseCount = 1, resumeCount = 1,
                resumesObservedInUpdate = 1, suspendedSeconds = 1f
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
            sample.resumesObservedInUpdate = 0;
            if (DeviceValidationPolicy.Evaluate(sample).Count == 0)
                result.errors.Add("실기기 QA 정책: 복귀 후 활성 프레임 미확인 리포트가 통과함");
            sample.resumesObservedInUpdate = 1;
            sample.pauseCount = 2;
            if (DeviceValidationPolicy.Evaluate(sample).Count == 0)
                result.errors.Add("실기기 QA 정책: 전환·복귀 횟수 불일치 리포트가 통과함");
            sample.pauseCount = 1;
            sample.maximumFrameSeconds = float.NaN;
            if (DeviceValidationPolicy.Evaluate(sample).Count == 0)
                result.errors.Add("실기기 QA 정책: 잘못된 활성 프레임 측정값이 통과함");
            sample.maximumFrameSeconds = 0.04f;
            sample.metricsRevision = 1;
            if (DeviceValidationPolicy.Evaluate(sample).Count == 0)
                result.errors.Add("실기기 QA 정책: 구버전 측정 항목 리포트가 통과함");
            sample.metricsRevision = 2;
            sample.suspendedSeconds = float.NaN;
            if (DeviceValidationPolicy.Evaluate(sample).Count == 0)
                result.errors.Add("실기기 QA 정책: 잘못된 백그라운드 체류 시간이 통과함");
            sample.suspendedSeconds = 1f;
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
                companionBonds = null, townRestorations = null, fieldIncidentRecords = null, endingHistory = null,
                seenShadowIds = new List<string> { "knight", "knight", "" },
                recordedShadowIds = new List<string> { "knight" }
            };
            for (int i = 0; i < 8; i++) old.party.Add(new ShadowInstance
                { instanceId = i < 2 ? "duplicate" : "", shadowId = "test_" + i, level = 0, exp = -1 });
            old.inventory.Add(new ItemStack { itemId = "overflow_item", count = int.MaxValue });
            old.inventory.Add(new ItemStack { itemId = "overflow_item", count = 10 });
            SaveManager.Migrate(old, out _);
            if (old.version != SaveData.CurrentVersion || old.mapId != "PrologueTheater" ||
                old.party.Count != SaveData.MaxPartySize || old.storage.Count != 2 || old.gold != 0 ||
                old.inventory.Count != 1 || old.inventory[0].count != int.MaxValue ||
                old.party.Any(x => x.level < 1 || x.exp < 0) ||
                string.IsNullOrEmpty(old.companionInstanceId) ||
                old.companionBonds == null || old.townRestorations == null || old.fieldIncidentRecords == null ||
                old.endingHistory == null || old.memoryFragments < 0 ||
                !old.party.Any(x => x.instanceId == old.companionInstanceId) ||
                old.party.Concat(old.storage).Select(x => x.instanceId).Distinct().Count() != 8)
                result.errors.Add("v1 → 현재 세이브 마이그레이션 회귀 실패");

            var v10Shadow=new ShadowInstance{instanceId="v10_companion",shadowId="knight",level=15,currentHp=30};
            var v10=new SaveData
            {
                version=10,party=new List<ShadowInstance>{v10Shadow},companionInstanceId=v10Shadow.instanceId,
                companionBonds=new List<CompanionBondProgressData>{new CompanionBondProgressData{instanceId=v10Shadow.instanceId,experience=260,jointSkillUses=4}},
                townRestorations=new List<TownRestorationProgressData>{new TownRestorationProgressData{regionId="prologue_theater",points=6,returnVisitClaimed=true},new TownRestorationProgressData{regionId="prologue_theater",points=3}},
                fieldIncidentRecords=new List<FieldIncidentRecordData>{new FieldIncidentRecordData{incidentId="rain_script",lastCompletedDay=7,completionCount=2}}
            };
            SaveManager.Migrate(v10,out _);
            FieldIncidentRecordData migratedIncident=v10.fieldIncidentRecords.SingleOrDefault(x=>x.incidentId=="rain_script");
            if(v10.version!=SaveData.CurrentVersion||v10.companionBonds.Count!=1||v10.companionBonds[0].experience!=260||
               v10.townRestorations.Count!=1||v10.townRestorations[0].points!=6||!v10.townRestorations[0].returnVisitClaimed||
               migratedIncident==null||migratedIncident.stage!=4||migratedIncident.activeDay!=7||migratedIncident.completionCount!=2)
                result.errors.Add("v10 → v12 유대·마을·필드 사건 진행 복구 회귀 실패");

            var temporary = new SaveData { savedAtUtcTicks = 20 };
            var backup = new SaveData { savedAtUtcTicks = 10 };
            var primary = new SaveData { savedAtUtcTicks = 15 };
            if (SaveRecoveryPolicy.Choose(primary, temporary, backup) != SaveRecoverySource.Temporary ||
                SaveRecoveryPolicy.Choose(null, null, backup) != SaveRecoverySource.Backup ||
                SaveRecoveryPolicy.Choose(null, temporary, null) != SaveRecoverySource.Temporary ||
                SaveRecoveryPolicy.Choose(null, null, null) != SaveRecoverySource.None)
                result.errors.Add("중단 저장·백업 복구 우선순위 회귀 실패");

            backup.savedAtUtcTicks = 30;
            if (SaveRecoveryPolicy.Choose(primary, temporary, backup) != SaveRecoverySource.Backup)
                result.errors.Add("최신 백업 선택 회귀 실패");
            primary.savedAtUtcTicks = 30;
            if (SaveRecoveryPolicy.Choose(primary, temporary, backup) != SaveRecoverySource.Primary)
                result.errors.Add("동일 시각 주 저장 우선 회귀 실패");

            if (SaveValuePolicy.SaturatingAdd(int.MaxValue - 2, 10) != int.MaxValue ||
                SaveValuePolicy.SaturatingAdd(3, -10) != 0 ||
                SaveValuePolicy.SaturatingAdd(-5, 7) != 7)
                result.errors.Add("세이브 재화·수량 포화 연산 회귀 실패");

            var quest = new QuestDefinition
            {
                questId = "recovery_test",
                objectives = new List<QuestObjectiveDefinition>
                {
                    new QuestObjectiveDefinition { objectiveId = "step", requiredCount = 3 }
                }
            };
            var questSave = new SaveData
            {
                quests = new List<QuestProgressData>(),
                flags = new List<FlagEntry> { new FlagEntry { key = "quest_recovery_test_complete", value = 1 } }
            };
            if (!QuestProgressRecovery.RestoreCompletedStates(questSave, new[] { quest }) ||
                questSave.quests.Count != 1 || !questSave.quests[0].completed ||
                !questSave.quests[0].rewardClaimed || questSave.quests[0].objectives.Count != 1 ||
                questSave.quests[0].objectives[0].current != 3 ||
                QuestProgressRecovery.RestoreCompletedStates(questSave, new[] { quest }))
                result.errors.Add("퀘스트 완료 플래그 복구 회귀 실패");

            var claimed = new QuestProgressData { questId = "claimed", rewardClaimed = true };
            questSave.quests.Add(claimed);
            if (!QuestProgressRecovery.RestoreCompletedStates(questSave, Array.Empty<QuestDefinition>()) ||
                !claimed.completed)
                result.errors.Add("퀘스트 보상 수령 상태 복구 회귀 실패");

            var talkSave = new SaveData
            {
                flags = new List<FlagEntry>
                {
                    new FlagEntry { key = "talked_npc_aria", value = 1 },
                    new FlagEntry { key = "talked_ash_scout", value = 1 },
                    new FlagEntry { key = "talked_rival_director_final", value = 1 }
                }
            };
            if (!QuestProgressRecovery.HasTalkFact(talkSave, "npc_aria") ||
                !QuestProgressRecovery.HasTalkFact(talkSave, "npc_ash_scout") ||
                !QuestProgressRecovery.HasTalkFact(talkSave, "npc_rival_director") ||
                QuestProgressRecovery.HasTalkFact(talkSave, "npc_unknown"))
                result.errors.Add("NPC 대화 사실 플래그 복구 회귀 실패");

            var cumulativeSave = new SaveData
            {
                version = 7,
                recordedShadowIds = new List<string> { "a", "b", "c", "d", "e" },
                quests = new List<QuestProgressData>
                {
                    new QuestProgressData
                    {
                        questId = "memory_mage_pages",
                        objectives = new List<QuestObjectiveProgressData>
                        {
                            new QuestObjectiveProgressData { objectiveId = "record_pages", current = 2 }
                        }
                    },
                    new QuestProgressData
                    {
                        questId = "memory_beast_trail",
                        objectives = new List<QuestObjectiveProgressData>
                        {
                            new QuestObjectiveProgressData { objectiveId = "clear_trail", current = 3 }
                        }
                    }
                }
            };
            SaveManager.Migrate(cumulativeSave, out _);
            var recordProgress = cumulativeSave.quests[0].objectives[0];
            var defeatProgress = cumulativeSave.quests[1].objectives[0];
            QuestRepository.TryGet("memory_mage_pages", out QuestDefinition recordQuest);
            QuestRepository.TryGet("memory_beast_trail", out QuestDefinition defeatQuest);
            cumulativeSave.recordedShadowIds.Add("f");
            cumulativeSave.wildVictoryCount++;
            if (cumulativeSave.version != SaveData.CurrentVersion ||
                !recordProgress.cumulativeBaselineSet || recordProgress.cumulativeBaselineCounter != 5 ||
                recordProgress.cumulativeBaselineProgress != 2 ||
                !defeatProgress.cumulativeBaselineSet || defeatProgress.cumulativeBaselineCounter != 0 ||
                defeatProgress.cumulativeBaselineProgress != 3 ||
                recordQuest == null || defeatQuest == null ||
                !QuestProgressRecovery.TryGetCumulativeProgress(cumulativeSave, recordQuest.objectives[0],
                    recordProgress, out int recoveredRecords) || recoveredRecords != 3 ||
                !QuestProgressRecovery.TryGetCumulativeProgress(cumulativeSave, defeatQuest.objectives[0],
                    defeatProgress, out int recoveredDefeats) || recoveredDefeats != 4)
                result.errors.Add("v7 누적 퀘스트 기준점 마이그레이션·복구 회귀 실패");
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
                flame.role = ShadowRole.PhysicalDealer;
                frost.role = ShadowRole.SpeedUtility;
                neutral.role = ShadowRole.Support;
                if (BattleAI.ProfileFor(flame, false) != BattleAiProfile.Aggressive ||
                    BattleAI.ProfileFor(flame, true) != BattleAiProfile.BossAggressive ||
                    BattleAI.ProfileFor(frost, false) != BattleAiProfile.Controller ||
                    BattleAI.ProfileFor(frost, true) != BattleAiProfile.BossController ||
                    BattleAI.ProfileFor(neutral, false) != BattleAiProfile.Survivor ||
                    BattleAI.ProfileFor(neutral, true) != BattleAiProfile.BossGuardian)
                    result.errors.Add("역할별 일반·보스 전투 성향 회귀 실패");
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

        private static void ValidateHiddenBattleGimmicks(ValidationReport result)
        {
            if (!BattleGimmickRules.IsBellWarningTurn(BattleGimmick.BellToll, 2) ||
                !BattleGimmickRules.IsBellTollTurn(BattleGimmick.BellToll, 3) ||
                BattleGimmickRules.IsBellTollTurn(BattleGimmick.BellToll, 2))
                result.errors.Add("숨은 보스 봉화 종 3턴 주기 회귀 실패");
            if (!BattleGimmickRules.ReversesInitiative(BattleGimmick.MoonReversal, 2) ||
                BattleGimmickRules.ReversesInitiative(BattleGimmick.MoonReversal, 3))
                result.errors.Add("숨은 보스 달빛 선후공 반전 회귀 실패");
            if (!BattleGimmickRules.IsEchoRepeat(BattleGimmick.EchoSeal, "slash", "slash") ||
                !BattleGimmickRules.IsEchoBreak(BattleGimmick.EchoSeal, "slash", "guard") ||
                BattleGimmickRules.IsEchoRepeat(BattleGimmick.None, "slash", "slash"))
                result.errors.Add("숨은 보스 메아리 기술 반복 판정 회귀 실패");
        }

        private static void ValidateBattleMomentum(ValidationReport result)
        {
            var momentum = new BattleMomentum();
            var normal = new HitResult { damage = 40, elementMultiplier = 1f };
            if (momentum.Apply(ref normal) || momentum.Value != 1 || normal.damage != 40)
                result.errors.Add("열연 일반 명중 누적 회귀 실패");

            var effective = new HitResult { damage = 40, elementMultiplier = 1.5f };
            if (!momentum.Apply(ref effective) || momentum.Value != 0 || effective.damage != 50)
                result.errors.Add("열연 앙코르 피해·초기화 회귀 실패");

            var miss = new HitResult { missed = true, damage = 40, elementMultiplier = 1f };
            normal.damage = 40;
            momentum.Apply(ref normal);
            if (momentum.Value != 1 || momentum.Apply(ref miss) || momentum.Value != 0)
                result.errors.Add("열연 빗나감 초기화 회귀 실패");

            var support = new HitResult { damage = 0, elementMultiplier = 1f };
            momentum.Apply(ref normal);
            if (momentum.Apply(ref support, false) || momentum.Value != 1)
                result.errors.Add("열연 비공격 기술 유지 회귀 실패");
        }

        private static void ValidateBattlePerformance(ValidationReport result)
        {
            BattlePerformanceResult s = BattlePerformance.Evaluate(2, 1, 3, 1, 0, 100);
            BattlePerformanceResult a = BattlePerformance.Evaluate(4, 1, 1, 1, 0, 100);
            BattlePerformanceResult b = BattlePerformance.Evaluate(2, 1, 0, 0, 0, 100);
            BattlePerformanceResult c = BattlePerformance.Evaluate(8, 1, 0, 0, 0, 100);
            BattlePerformanceResult capped = BattlePerformance.Evaluate(1, 1, 99, 99, 0, int.MaxValue);
            if (s.rank != "S" || s.score != 150 || s.bonusGold != 25 ||
                a.rank != "A" || a.score != 110 || a.bonusGold != 15 ||
                b.rank != "B" || b.score != 80 || b.bonusGold != 5 ||
                c.rank != "C" || c.score != 70 || c.bonusGold != 0 ||
                capped.bonusGold != 0)
                result.errors.Add("전투 공연 등급·보너스 회귀 실패");
            if (BattleManager.EnvironmentLabel(FieldWeather.Rain, FieldTimeOfDay.Evening) != "해질녘 · 잔향비")
                result.errors.Add("전투 환경 요약 회귀 실패");
            var growthData = ScriptableObject.CreateInstance<ShadowData>();
            growthData.shadowId = "growth_regression"; growthData.baseHp = 100; growthData.baseAtk = 100;
            growthData.baseDef = 100; growthData.baseSpd = 100; growthData.growthTier = GrowthTier.Legendary;
            var focused = new ShadowInstance(growthData, 1)
                { trainingFocus = TrainingFocus.Power, masteryPoints = CollectionGrowthService.FocusUnlockMastery };
            if (focused.Atk != 106 || focused.CritRate < .019f ||
                CollectionGrowthService.DuplicateFragments(growthData) != 12)
                result.errors.Add("숙련 성장·중복 기억 조각 회귀 실패");
            UnityEngine.Object.DestroyImmediate(growthData);
        }

        private static void ValidateExplorationRewards(ValidationReport result)
        {
            var opening = new RegionData { act = 1 };
            var dungeon = new RegionData { act = 3, isDungeon = true };
            var legendaryHidden = new RegionData { act = 8, isHidden = true, isLegendary = true };
            if (ExplorationRewardService.RegionGold(opening) != 15 ||
                ExplorationRewardService.RegionGold(dungeon) != 30 ||
                ExplorationRewardService.RegionGold(legendaryHidden) != 85 ||
                ExplorationRewardService.NextMilestone(0) != 5 ||
                ExplorationRewardService.NextMilestone(5) != 10 ||
                ExplorationRewardService.NextMilestone(62) != 63 ||
                ExplorationRewardService.NextMilestone(63) != 0 ||
                ExplorationRewardService.MilestoneGoldFor(5) != 50 ||
                ExplorationRewardService.MilestoneGoldFor(63) != 1000 ||
                ExplorationRewardService.MilestoneGoldFor(7) != 0)
                result.errors.Add("지역 발견·누적 탐험 보상 회귀 실패");
        }

        private static void ValidateCollectionRewards(ValidationReport result)
        {
            if (CollectionRewardService.NextMilestone(0, 180) != 5 ||
                CollectionRewardService.NextMilestone(5, 180) != 15 ||
                CollectionRewardService.NextMilestone(150, 180) != 180 ||
                CollectionRewardService.NextMilestone(180, 180) != 0 ||
                CollectionRewardService.NextMilestone(150, 177) != 177 ||
                CollectionRewardService.NextMilestone(177, 177) != 0 ||
                CollectionRewardService.GoldFor(5) != 50 ||
                CollectionRewardService.GoldFor(180) != 1800 ||
                CollectionRewardService.GoldFor(7) != 0)
                result.errors.Add("각본집 수집 이정표·보상 회귀 실패");
        }

        private static void ValidateJourneyOverviewPolicy(ValidationReport result)
        {
            if (JourneyOverviewService.Weighted(1, 2, 35) != 18 ||
                JourneyOverviewService.Weighted(99, 10, 35) != 35 ||
                JourneyOverviewService.Weighted(-1, 10, 35) != 0 ||
                JourneyOverviewService.Weighted(1, 0, 35) != 0 ||
                JourneyOverviewService.RankIndex(0) != 0 ||
                JourneyOverviewService.RankIndex(20) != 1 ||
                JourneyOverviewService.RankIndex(40) != 2 ||
                JourneyOverviewService.RankIndex(60) != 3 ||
                JourneyOverviewService.RankIndex(80) != 4 ||
                JourneyOverviewService.RankIndex(100) != 5)
                result.errors.Add("전체 여정 진행률·등급 정책 회귀 실패");
        }

        private static void ValidateFieldTraversalEffects(ValidationReport result)
        {
            if (FieldTraversalEffects.ResolveSurface("MeadowPixel_Grass", FootstepSurface.Stone) != FootstepSurface.Grass ||
                FieldTraversalEffects.ResolveSurface("MeadowPixel_Path", FootstepSurface.Grass) != FootstepSurface.Stone ||
                FieldTraversalEffects.ResolveSurface("Village_Ground", FootstepSurface.Stone) != FootstepSurface.Stone ||
                FieldTraversalEffects.ResolveSurface("MoonfangForest_Ground", FootstepSurface.Stone) != FootstepSurface.Stone ||
                FieldTraversalEffects.ResolveSurface("MemorySea_Ground", FootstepSurface.Stone) != FootstepSurface.Water ||
                FieldTraversalEffects.ResolveSurface("FrostPort_Ground", FootstepSurface.Stone) != FootstepSurface.Snow ||
                FieldTraversalEffects.ResolveSurface("AshWastes_Ground", FootstepSurface.Stone) != FootstepSurface.Ash ||
                FieldTraversalEffects.ResolveSurface("Unknown", FootstepSurface.Metal) != FootstepSurface.Metal)
                result.errors.Add("풀·길·물·눈·재 지형 반응 판정 회귀 실패");
            TraversalMotionProfile mud = FieldTraversalMotion.Profile("Marsh_Mud", Vector2Int.right, 0);
            TraversalMotionProfile ice = FieldTraversalMotion.Profile("Frost_Ice", Vector2Int.up, 0);
            TraversalMotionProfile ladderSide = FieldTraversalMotion.Profile("Tower_Ladder", Vector2Int.left, 0);
            TraversalMotionProfile ledgeUp = FieldTraversalMotion.Profile("Cliff_Ledge", Vector2Int.up, 0);
            TraversalMotionProfile dash = FieldTraversalMotion.Profile("Meadow_Path", Vector2Int.right, 4);
            if (mud.durationMultiplier <= 1f || ice.durationMultiplier >= 1f || ice.overshoot <= 0f ||
                ladderSide.allowed || ledgeUp.allowed || dash.durationMultiplier >= 1f)
                result.errors.Add("달리기·진흙·얼음·사다리·턱 이동 프로필 회귀 실패");
            Vector2Int keepHorizontal=PlayerController.ResolveDirection(new Vector2(.51f,.5f),true,.14f,out bool horizontal);
            Vector2Int switchVertical=PlayerController.ResolveDirection(new Vector2(.4f,.8f),true,.14f,out bool verticalResult);
            if (keepHorizontal!=Vector2Int.right || !horizontal || switchVertical!=Vector2Int.up || verticalResult)
                result.errors.Add("필드 대각선 입력 축 고정·전환 회귀 실패");
            if(PlayerController.CanRepeatHeldStep(true,.18f,.34f)||
               !PlayerController.CanRepeatHeldStep(true,.35f,.34f)||
               !PlayerController.CanRepeatHeldStep(false,0f,.34f))
                result.errors.Add("짧은 방향 입력의 한 칸 이동·길게 누르기 반복 지연 회귀 실패");
            if(FieldDepthSorter.OrderAt(2f)>=FieldDepthSorter.OrderAt(1f) ||
               FieldDepthSorter.OrderAt(1f,3)-FieldDepthSorter.OrderAt(1f)!=3)
                result.errors.Add("필드 Y축 앞뒤 깊이 정렬 회귀 실패");
        }

        private static void ValidateRegionalEncounterPolicy(ValidationReport result)
        {
            AssertEncounterCadence(new RegionData { act = 1, recommendedLevelMin = 3 }, 14, 20, .08f, 24, "초반 필드", result);
            AssertEncounterCadence(new RegionData { isHidden = true }, 9, 13, .09f, 22, "숨겨진 지역", result);
            AssertEncounterCadence(new RegionData { isDungeon = true }, 5, 8, .16f, 14, "던전", result);
            AssertEncounterCadence(new RegionData(), 7, 11, .12f, 18, "일반 필드", result);
            if (!(RegionalEncounterPolicy.FeaturedWeight(0) > RegionalEncounterPolicy.FeaturedWeight(1) &&
                  RegionalEncounterPolicy.FeaturedWeight(1) > RegionalEncounterPolicy.FeaturedWeight(2) &&
                  RegionalEncounterPolicy.FeaturedWeight(2) > RegionalEncounterPolicy.FeaturedWeight(3)))
                result.errors.Add("지역 대표 그림자 출현 가중치 회귀 실패");
            if (!(RegionalEncounterPolicy.RarityMultiplier(GrowthTier.Standard) >
                  RegionalEncounterPolicy.RarityMultiplier(GrowthTier.Rare)) ||
                RegionalEncounterPolicy.RarityMultiplier(GrowthTier.RegionalBoss) != 0f ||
                RegionalEncounterPolicy.RarityMultiplier(GrowthTier.Legendary) != 0f)
                result.errors.Add("그림자 등급별 랜덤 조우 가중치 회귀 실패");

            ShadowData candidate = CreateAiTestShadow("encounter_policy_test", ShadowElement.None);
            try
            {
                var region = new RegionData { bossShadowName = "지역 보스" };
                candidate.displayName = "필드 그림자";
                candidate.growthTier = GrowthTier.Standard;
                candidate.baseCaptureRate = .3f;
                var intro = new RegionData { act = 1, recommendedLevelMin = 3 };
                var solo = new[] { new ShadowInstance(candidate, 5) };
                if (IntroEncounterPolicy.ClampLevel(intro, 10, solo) != 4)
                    result.errors.Add("초반 솔로 조우 Lv.4 상한 회귀 실패");
                candidate.growthTier = GrowthTier.Rare;
                if (IntroEncounterPolicy.IsAvailable(intro, candidate, solo) ||
                    !IntroEncounterPolicy.IsAvailable(intro, candidate, new[] { solo[0], new ShadowInstance(candidate, 5) }))
                    result.errors.Add("초반 동료 확보 전 희귀 그림자 보호 정책 회귀 실패");
                candidate.growthTier = GrowthTier.Standard;
                if (!RegionalEncounterPolicy.IsEligible(region, candidate))
                    result.errors.Add("일반 그림자 랜덤 조우 후보 회귀 실패");
                candidate.displayName = region.bossShadowName;
                if (RegionalEncounterPolicy.IsEligible(region, candidate))
                    result.errors.Add("지역 보스 랜덤 조우 제외 회귀 실패");
                candidate.displayName = "다른 지역 보스";
                candidate.growthTier = GrowthTier.RegionalBoss;
                if (RegionalEncounterPolicy.IsEligible(region, candidate))
                    result.errors.Add("보스 등급 랜덤 조우 제외 회귀 실패");

                region.regionId = "world_state_test";
                region.act = 2;
                FieldWorldSnapshot dawn = FieldWorldState.At(region, 0f);
                FieldWorldSnapshot day = FieldWorldState.At(region, FieldWorldState.SegmentSeconds);
                FieldWorldSnapshot evening = FieldWorldState.At(region, FieldWorldState.SegmentSeconds * 2f);
                FieldWorldSnapshot night = FieldWorldState.At(region, FieldWorldState.SegmentSeconds * 3f);
                FieldWorldSnapshot nextDawn = FieldWorldState.At(region, FieldWorldState.DaySeconds);
                if (dawn.timeOfDay != FieldTimeOfDay.Dawn || day.timeOfDay != FieldTimeOfDay.Day ||
                    evening.timeOfDay != FieldTimeOfDay.Evening || night.timeOfDay != FieldTimeOfDay.Night ||
                    nextDawn.timeOfDay != FieldTimeOfDay.Dawn || nextDawn.dayIndex != 1)
                    result.errors.Add("게임 내부 시간대 순환 회귀 실패");
                if (FieldWorldState.At(region, 0f).weather != dawn.weather)
                    result.errors.Add("지역 날씨 결정성 회귀 실패");
                candidate.growthTier = GrowthTier.Standard;
                candidate.element = ShadowElement.Shade;
                if (FieldWorldState.EncounterWeightMultiplier(candidate, night) <= 1f)
                    result.errors.Add("밤 그림자 속성 출현 보정 회귀 실패");
            }
            finally { UnityEngine.Object.DestroyImmediate(candidate); }
        }

        private static void AssertEncounterCadence(RegionData region, int expectedMin, int expectedMax,
            float expectedChance, int expectedPity, string label, ValidationReport result)
        {
            RegionalEncounterPolicy.GetCadence(region, out int min, out int max, out float chance, out int pity);
            if (min != expectedMin || max != expectedMax || !Mathf.Approximately(chance, expectedChance) || pity != expectedPity)
                result.errors.Add($"{label} 랜덤 조우 간격 회귀 실패");
        }

        private static void ValidateBattleActions(ValidationReport result)
        {
            ShadowData data = CreateAiTestShadow("action_rule_test", ShadowElement.None);
            SkillData basic = ScriptableObject.CreateInstance<SkillData>();
            SkillData foreign = ScriptableObject.CreateInstance<SkillData>();
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            try
            {
                basic.skillId = "action_basic";
                foreign.skillId = "action_foreign";
                data.basicAttack = basic;
                var unit = new BattleUnit(new ShadowInstance(data, 1), BattleSide.Player);
                if (!BattleAction.Attack(BattleSide.Player, basic).MatchesBasicAttack(unit) ||
                    BattleAction.Attack(BattleSide.Player, foreign).MatchesBasicAttack(unit))
                    result.errors.Add("일반 공격 기술 우회 방지 회귀 실패");

                var context = new BattleContext();
                if (context.HasItem(item)) result.errors.Add("빈 인벤토리 도구 검증 회귀 실패");
                context.inventory = new Dictionary<ItemData, int> { { item, 1 } };
                if (!context.HasItem(item)) result.errors.Add("보유 도구 검증 회귀 실패");
                context.inventory[item] = 0;
                if (context.HasItem(item)) result.errors.Add("소진 도구 검증 회귀 실패");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(basic);
                UnityEngine.Object.DestroyImmediate(foreign);
                UnityEngine.Object.DestroyImmediate(item);
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        private static void ValidateBattleRewards(ValidationReport result)
        {
            if (DamageCalculator.SplitExperience(101, 3) != 33 ||
                DamageCalculator.SplitExperience(2, 6) != 1 ||
                DamageCalculator.SplitExperience(100, 0) != 0)
                result.errors.Add("참여 경험치 분배 회귀 실패");
            if (ShadowInstance.ExperienceToNext(1) != 25 ||
                ShadowInstance.ExperienceToNext(10) != 520 ||
                ShadowInstance.ExperienceToNext(ShadowInstance.MaxLevel) != 0)
                result.errors.Add("그림자 경험치 곡선 회귀 실패");

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

                var overflow = new ShadowInstance(data, 1) { exp = int.MaxValue - 1 };
                overflow.AddExp(100);
                if (overflow.level != ShadowInstance.MaxLevel || overflow.exp != 0)
                    result.errors.Add("그림자 경험치 오버플로 방지 회귀 실패");

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
                if (!summary.Contains("3T") || !summary.Contains("2T") || !summary.Contains("20%") || !unit.HasAnyStatus || unit.Debuffs.Count != 1)
                    result.errors.Add("전투 상태 이상 복합 표시 회귀 실패");
            }
            finally { UnityEngine.Object.DestroyImmediate(data); }

            ShadowData bleedData = CreateAiTestShadow("bleed_display_test", ShadowElement.None);
            try
            {
                var unit = new BattleUnit(new ShadowInstance(bleedData, 5), BattleSide.Player);
                unit.ApplyStatus(StatusEffectType.Bleed, 4);
                int first = unit.GetDotDamage();
                if (!BattleStatusFormatter.Summary(unit).Contains("1/3")) result.errors.Add("출혈 누적 1단계 표시 회귀 실패");
                unit.TickStatus();
                if (!BattleStatusFormatter.Summary(unit).Contains("2/3") || unit.GetDotDamage() != first * 2)
                    result.errors.Add("출혈 누적 2단계 표시/피해 회귀 실패");
            }
            finally { UnityEngine.Object.DestroyImmediate(bleedData); }
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
                if (region.recommendedLevelMin < 1 || region.recommendedLevelMax > ShadowInstance.MaxLevel)
                    result.errors.Add($"권장 레벨 범위 초과: {region.regionId} " +
                                      $"({region.recommendedLevelMin}-{region.recommendedLevelMax} / 최대 {ShadowInstance.MaxLevel})");
                if (region.isSettlement && (region.isHidden || region.isBossStage))
                    result.errors.Add($"{region.regionId}: 마을은 숨은 지역이나 보스 무대일 수 없습니다");
                if (region.isSettlement && (region.featuredShadows?.Length ?? 0) > 0)
                    result.errors.Add($"{region.regionId}: 마을에는 야생 출현 그림자를 선언할 수 없습니다");
                string[] connections = region.connectedRegionIds ?? Array.Empty<string>();
                if (connections.Length > 4)
                    result.errors.Add($"{region.regionId}: 포털 배치 한도 초과 ({connections.Length}/4)");
                if (connections.Contains(region.regionId))
                    result.errors.Add($"{region.regionId}: 자기 자신으로 연결된 포털");
                foreach (var duplicate in connections.GroupBy(x => x).Where(x => x.Count() > 1))
                    result.errors.Add($"{region.regionId}: 연결 지역 중복 ({duplicate.Key})");
                if (!string.IsNullOrEmpty(region.requiredFlag) && !IsDeclaredStoryFlag(region.requiredFlag))
                    result.errors.Add($"{region.regionId}: 생성되지 않는 지역 해금 플래그 {region.requiredFlag}");
            }
            foreach (var region in regions.Where(x => x != null))
                foreach (string connected in region.connectedRegionIds ?? Array.Empty<string>())
                    if (!byId.ContainsKey(connected)) result.errors.Add($"{region.regionId}의 알 수 없는 연결: {connected}");
                    else if (!(byId[connected].connectedRegionIds ?? Array.Empty<string>()).Contains(region.regionId))
                        result.errors.Add($"{region.regionId} ↔ {connected}: 양방향 연결 누락");

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

        private static void ValidateRoster(List<RegionData> regions, ValidationReport result)
        {
            if (!File.Exists(RosterPath))
            { result.errors.Add("RegionalRosterManifest.json 누락 — 먼저 전체 생성을 실행하세요"); return; }
            var roster = JsonUtility.FromJson<RosterManifest>(File.ReadAllText(RosterPath));
            int count = roster?.entries?.Count ?? 0;
            if (count != 97) result.errors.Add($"지역 확장 로스터 수 불일치: {count}/97");
            if (roster?.entries == null) return;
            if (roster.entries.Select(x => x.shadowId).Distinct().Count() != count)
                result.errors.Add("지역 확장 로스터 shadowId 중복");

            var byId = regions.Where(x => x != null).ToDictionary(x => x.regionId, StringComparer.Ordinal);
            foreach (RosterEntry entry in roster.entries)
            {
                if (string.IsNullOrEmpty(entry.shadowId) || string.IsNullOrEmpty(entry.regionId))
                { result.errors.Add("지역 확장 로스터에 빈 그림자/지역 ID가 있음"); continue; }
                if (!byId.TryGetValue(entry.regionId, out RegionData region))
                { result.errors.Add($"{entry.shadowId}: 알 수 없는 출현 지역 {entry.regionId}"); continue; }
                if (entry.sceneName != region.sceneName)
                    result.errors.Add($"{entry.shadowId}: 출현 씬 불일치 {entry.sceneName}/{region.sceneName}");
                if (entry.minLevel < region.recommendedLevelMin || entry.maxLevel > region.recommendedLevelMax ||
                    entry.minLevel > entry.maxLevel)
                    result.errors.Add($"{entry.shadowId}: 지역 레벨 범위 이탈 {entry.minLevel}-{entry.maxLevel}");
                if (!entry.featured && !region.AllowsRandomEncounters)
                    result.errors.Add($"{entry.shadowId}: 랜덤 조우 불가 지역에 일반 로스터 배치 ({region.displayName})");
            }
            foreach (RegionData region in regions.Where(x => x != null && x.AllowsRandomEncounters))
                if (!roster.entries.Any(x => x.regionId == region.regionId && !x.featured))
                    result.errors.Add($"{region.displayName}: 지역 전용 일반 그림자 로스터 누락");
        }

        private static void ValidateCompanionEvents(List<RegionData> regions, ValidationReport result)
        {
            if (!File.Exists(CompanionEventPath))
            { result.errors.Add("CompanionEventCatalog.json 누락"); return; }

            CompanionEventCatalogData catalog = JsonUtility.FromJson<CompanionEventCatalogData>(
                File.ReadAllText(CompanionEventPath));
            List<CompanionEventDefinition> events = catalog?.events ?? new List<CompanionEventDefinition>();
            if (events.Count != 12) result.errors.Add($"핵심 동행 이벤트 수 불일치: {events.Count}/12");
            if (IsDeclaredStoryFlag("companion_event_missing_regression_event_complete"))
                result.errors.Add("존재하지 않는 동행 이벤트 완료 플래그를 허용함");

            var regionIds = new HashSet<string>(regions.Where(x => x != null).Select(x => x.regionId), StringComparer.Ordinal);
            var database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>(DatabasePath);
            var shadowIds = new HashSet<string>(database?.shadows.Where(x => x != null).Select(x => x.shadowId)
                                                ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            DialogueCatalogData dialogues = File.Exists(DialoguePath)
                ? JsonUtility.FromJson<DialogueCatalogData>(File.ReadAllText(DialoguePath))
                : null;
            var dialogueIds = new HashSet<string>(dialogues?.dialogues.Select(x => x.dialogueId)
                                                  ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            int hiddenRegionRewards = 0, trueNameRewards = 0, itemRewards = 0, bondRewards = 0;
            var itemIds = new HashSet<string>(database?.items.Where(x => x != null).Select(x => x.itemId)
                                              ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            foreach (CompanionEventDefinition definition in events)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.eventId))
                { result.errors.Add("동행 이벤트의 eventId 누락"); continue; }
                if (!eventIds.Add(definition.eventId)) result.errors.Add("동행 이벤트 ID 중복: " + definition.eventId);
                if (!IsDeclaredStoryFlag(definition.CompletionFlag))
                    result.errors.Add($"{definition.eventId}: 동행 이벤트 완료 플래그 인식 실패");
                if (!regionIds.Contains(definition.regionId))
                    result.errors.Add($"{definition.eventId}: 알 수 없는 지역 {definition.regionId}");
                if (!shadowIds.Contains(definition.requiredShadowId))
                    result.errors.Add($"{definition.eventId}: 알 수 없는 그림자 {definition.requiredShadowId}");
                if (!dialogueIds.Contains(definition.dialogueId))
                    result.errors.Add($"{definition.eventId}: 알 수 없는 대화 {definition.dialogueId}");
                if (definition.minSteps < 1 || definition.guaranteedSteps < definition.minSteps)
                    result.errors.Add($"{definition.eventId}: 걸음 수 조건 오류");
                if (definition.chancePerStep <= 0f || definition.chancePerStep > 1f)
                    result.errors.Add($"{definition.eventId}: 발생 확률 범위 오류");
                if (!string.IsNullOrEmpty(definition.unlockRegionId))
                {
                    hiddenRegionRewards++;
                    RegionData target = regions.FirstOrDefault(x => x?.regionId == definition.unlockRegionId);
                    if (target?.isHidden != true)
                        result.errors.Add($"{definition.eventId}: 숨은 지역 보상 대상 오류 ({definition.unlockRegionId})");
                }
                if (!string.IsNullOrEmpty(definition.rewardItemId))
                {
                    itemRewards++;
                    if (!itemIds.Contains(definition.rewardItemId))
                        result.errors.Add($"{definition.eventId}: 알 수 없는 보상 도구 {definition.rewardItemId}");
                }
                if (definition.rewardFlag?.StartsWith("truth_", StringComparison.Ordinal) == true) trueNameRewards++;
                if (definition.rewardFlag?.StartsWith("companion_bond_", StringComparison.Ordinal) == true) bondRewards++;
            }
            if (hiddenRegionRewards != 3 || trueNameRewards != 3 || itemRewards != 3 || bondRewards != 3)
                result.errors.Add($"동행 이벤트 보상 배분 오류: 지역 {hiddenRegionRewards}, 진명 {trueNameRewards}, " +
                                  $"도구 {itemRewards}, 유대 {bondRewards} (각 3 필요)");
        }

        private static void ValidateTownServices(List<RegionData> regions, ValidationReport result)
        {
            List<RegionData> settlements = regions.Where(x => x != null && x.isSettlement).ToList();
            var rumorIds = new HashSet<string>(StringComparer.Ordinal);
            DialogueCatalogData dialogues = File.Exists(DialoguePath)
                ? JsonUtility.FromJson<DialogueCatalogData>(File.ReadAllText(DialoguePath)) : null;
            var knownDialogues = new HashSet<string>(dialogues?.dialogues?.Where(x => x != null)
                .Select(x => x.dialogueId) ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            QuestCatalogData questCatalog = File.Exists(QuestPath)
                ? JsonUtility.FromJson<QuestCatalogData>(File.ReadAllText(QuestPath)) : null;
            var questsById = (questCatalog?.quests ?? new List<QuestDefinition>())
                .Where(x => x != null && !string.IsNullOrEmpty(x.questId))
                .ToDictionary(x => x.questId, StringComparer.Ordinal);
            ShadowDatabase database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>(DatabasePath);
            var signatures = new HashSet<string>(StringComparer.Ordinal);
            foreach (RegionData region in settlements)
            {
                if (!TownServicePolicy.IsConfigured(region))
                { result.errors.Add($"{region.displayName}: 마을 서비스 정책 누락"); continue; }
                string rumor = TownServicePolicy.RumorDialogueId(region);
                if (string.IsNullOrEmpty(rumor) || !rumorIds.Add(rumor) || !knownDialogues.Contains(rumor))
                    result.errors.Add($"{region.displayName}: 소문 대화 누락 또는 중복 ({rumor})");
                string questId = TownServicePolicy.RumorQuestId(region.regionId);
                if (!questsById.TryGetValue(questId, out QuestDefinition rumorQuest) || !rumorQuest.isSideQuest ||
                    rumorQuest.autoStart || rumorQuest.objectives?.Count != 1 ||
                    !rumorQuest.objectives[0].TryGetType(out QuestObjectiveType type) || type != QuestObjectiveType.Reach ||
                    !rumorQuest.objectives[0].targetId.StartsWith("area_", StringComparison.Ordinal))
                    result.errors.Add($"{region.displayName}: 소문 탐험 퀘스트 설정 오류 ({questId})");
                string followup = TownServicePolicy.RumorFollowupDialogueId(region.regionId);
                if (string.IsNullOrEmpty(followup) || !knownDialogues.Contains(followup) ||
                    string.IsNullOrEmpty(TownServicePolicy.RumorCompletionFlag(region.regionId)))
                    result.errors.Add($"{region.displayName}: 소문 후일담·생활권 변화 설정 오류");
                IReadOnlyList<string> specialties = TownServicePolicy.SpecialtyItemIds(region);
                if (specialties.Count == 0) result.errors.Add($"{region.displayName}: 전문 판매품 누락");
                string signature = string.Join("|", specialties.OrderBy(x => x));
                if (!signatures.Add(signature)) result.errors.Add($"{region.displayName}: 다른 마을과 전문 판매품 구성이 같음");
                foreach (string itemId in specialties)
                {
                    ItemData item = database?.GetItem(itemId);
                    if (item == null) result.errors.Add($"{region.displayName}: 전문 판매품 데이터 누락 ({itemId})");
                    else if (!TownServicePolicy.AllowsItem(region, item))
                        result.errors.Add($"{region.displayName}: 전문 판매품 해금 조건 불일치 ({itemId})");
                }
            }
            var settlementIds = new HashSet<string>(settlements.Select(x => x.regionId), StringComparer.Ordinal);
            foreach (RegionData hidden in regions.Where(x => x != null && x.isHidden))
            {
                string source = TownServicePolicy.RumorSourceRegionId(hidden);
                if (string.IsNullOrEmpty(source) || !settlementIds.Contains(source) ||
                    string.IsNullOrEmpty(TownServicePolicy.HiddenMapHint(hidden)))
                    result.errors.Add($"{hidden.displayName}: 월드맵 소문 출처·힌트 누락");
            }
            if (TownServicePolicy.ShouldShowHiddenHint(false, true, true) ||
                TownServicePolicy.ShouldShowHiddenHint(true, false, true) ||
                TownServicePolicy.ShouldShowHiddenHint(true, true, false) ||
                !TownServicePolicy.ShouldShowHiddenHint(true, true, true))
                result.errors.Add("숨은 지역 월드맵 ? 공개 조건 회귀 실패");
            if (TownServicePolicy.CalculateRestCost(30, false) != 30 ||
                TownServicePolicy.CalculateRestCost(30, true) != 20 ||
                TownServicePolicy.CalculateRestCost(5, true) != 0)
                result.errors.Add("소문 완료 생활권 휴식비 보정 오류");
        }

        private static void ValidateDatabase(ValidationReport result)
        {
            var database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>(DatabasePath);
            if (database == null) { result.errors.Add("ShadowDatabase.asset 누락"); return; }
            if (database.items.Count(x => x != null) != 13)
                result.errors.Add($"도구 데이터 수 불일치: {database.items.Count(x => x != null)}/13");
            result.shadows = database.shadows.Count(x => x != null);
            if (result.shadows != 180) result.errors.Add($"그림자 데이터 수 불일치: {result.shadows}/180");
            var ids = database.shadows.Where(x => x != null).Select(x => x.shadowId).ToList();
            if (ids.Distinct().Count() != ids.Count) result.errors.Add("ShadowDatabase shadowId 중복");
            var ultimates = database.skills.Where(x => x != null && x.isUltimate).ToList();
            if (ultimates.Count == 0) result.errors.Add("진명 필살기 데이터 없음");
            if (File.Exists(RosterPath))
            {
                RosterManifest roster = JsonUtility.FromJson<RosterManifest>(File.ReadAllText(RosterPath));
                foreach (RosterEntry entry in roster?.entries?.Where(x => x.featured) ?? Enumerable.Empty<RosterEntry>())
                {
                    ShadowData featured = database.GetShadow(entry.shadowId);
                    string signatureId = entry.shadowId + "_signature";
                    if (featured == null || featured.skills == null ||
                        !featured.skills.Any(x => x != null && x.skillId == signatureId) ||
                        database.GetSkill(signatureId) == null)
                        result.errors.Add($"{entry.shadowId}: 지역 대표 고유 기술 누락 ({signatureId})");
                }
            }
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
                if (item.usableInField && item.effectType != ItemEffectType.HealFlat &&
                    item.effectType != ItemEffectType.HealRatio && item.effectType != ItemEffectType.GainExperience)
                    result.warnings.Add($"{item.itemId}: 필드 사용 효과가 아직 지원되지 않음 ({item.effectType})");
            }
            ItemData rehearsalScript=database.items.FirstOrDefault(x=>x!=null&&x.itemId=="rehearsal_script");
            if(rehearsalScript==null||rehearsalScript.effectType!=ItemEffectType.GainExperience||
               !rehearsalScript.usableInField||rehearsalScript.usableInBattle||rehearsalScript.value<1f||
               rehearsalScript.shopUnlockAct!=1)
                result.errors.Add("기억의 연습 대본 경험치 도구 설정 오류");
            ShadowData levelTestData=ScriptableObject.CreateInstance<ShadowData>();
            levelTestData.shadowId="starter_level_test";levelTestData.baseHp=100;levelTestData.hpGrowth=10;
            var levelTest=new ShadowInstance(levelTestData,5);levelTest.RaiseToLevel(10);
            if(levelTest.level!=10||levelTest.currentHp!=levelTest.MaxHp)
                result.errors.Add("스타터 최소 레벨 10 보정 회귀 실패");
            UnityEngine.Object.DestroyImmediate(levelTestData);
            ItemData boat = database.items.FirstOrDefault(x => x != null &&
                x.itemId == TraversalAbilityService.RubberBoatItemId);
            if (boat == null || boat.effectType != ItemEffectType.TraversalTool || !boat.isKeyItem ||
                boat.usableInBattle || boat.usableInField || boat.shopUnlockAct > 3 ||
                boat.buyPrice <= 0 || boat.sellPrice != 0)
                result.errors.Add("휴대용 고무보트 핵심 이동 도구 설정 오류");
            string[] eventToolIds =
            {
                TraversalAbilityService.FrostwingCompassItemId,
                TraversalAbilityService.SpiritProcessionLanternItemId,
                TraversalAbilityService.HeartWindingKeyItemId
            };
            foreach (string eventToolId in eventToolIds)
            {
                ItemData eventTool = database.items.FirstOrDefault(x => x != null && x.itemId == eventToolId);
                if (eventTool == null || eventTool.effectType != ItemEffectType.TraversalTool ||
                    !eventTool.isKeyItem || !eventTool.isEventOnly || eventTool.usableInBattle ||
                    eventTool.usableInField || eventTool.sellPrice != 0)
                    result.errors.Add($"동행 이벤트 특별 도구 설정 오류: {eventToolId}");
            }

            var boatSave = new SaveData
            {
                inventory = new List<ItemStack>
                {
                    new ItemStack { itemId = TraversalAbilityService.RubberBoatItemId, count = 1 }
                }
            };
            if (!TraversalAbilityService.TryResolve(TraversalAbility.PortableBoat, boatSave, database,
                    out TraversalAbility boatAbility, out _) || boatAbility != TraversalAbility.PortableBoat)
                result.errors.Add("휴대용 고무보트 이동 권능 판정 회귀 실패");
            if (TraversalAbilityService.TryResolve(TraversalAbility.PortableBoat, new SaveData(), database,
                    out _, out _))
                result.errors.Add("미보유 고무보트 이동 권능 판정 회귀 실패");
            var eventToolSave = new SaveData
            {
                inventory = new List<ItemStack>
                {
                    new ItemStack { itemId = TraversalAbilityService.FrostwingCompassItemId, count = 1 },
                    new ItemStack { itemId = TraversalAbilityService.SpiritProcessionLanternItemId, count = 1 },
                    new ItemStack { itemId = TraversalAbilityService.HeartWindingKeyItemId, count = 1 }
                }
            };
            if (!TraversalAbilityService.TryResolve(TraversalAbility.Glide, eventToolSave, database,
                    out TraversalAbility glide, out _) || glide != TraversalAbility.Glide ||
                !TraversalAbilityService.TryResolve(TraversalAbility.SpiritLight, eventToolSave, database,
                    out TraversalAbility light, out _) || light != TraversalAbility.SpiritLight ||
                !TraversalAbilityService.TryResolve(TraversalAbility.ClockworkRide, eventToolSave, database,
                    out TraversalAbility ride, out _) || ride != TraversalAbility.ClockworkRide)
                result.errors.Add("동행 이벤트 특별 이동 도구 판정 회귀 실패");
            ShadowData frost = database.shadows.FirstOrDefault(x => x != null && x.element == ShadowElement.Frost);
            var frostSave = new SaveData { recordedShadowIds = new List<string> { frost?.shadowId } };
            if (frost == null || !TraversalAbilityService.TryResolve(TraversalAbility.FreezeShallows,
                    frostSave, database, out TraversalAbility frostAbility, out _) ||
                frostAbility != TraversalAbility.FreezeShallows)
                result.errors.Add("서리 그림자 이동 권능 판정 회귀 실패");
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
                if (fullGrowth && (shadow.skills == null || shadow.skills.Count(x => x != null) < 2))
                    result.errors.Add($"{shadow.shadowId}: 보스 고유 전투 기술 부족");
                if (fullGrowth && BattleAI.ProfileFor(shadow, true) == BattleAiProfile.Normal)
                    result.errors.Add($"{shadow.shadowId}: 보스 전투 성향 누락");
                if ((rare || fullGrowth) && shadow.restoredForm?.enabled != true)
                    result.errors.Add($"{shadow.shadowId}: 등급 필수 기억 복원 데이터 누락");
                if (fullGrowth || shadow.salvationForm?.enabled == true || shadow.grudgeForm?.enabled == true)
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
            File.ReadAllText(CompanionEventPath) + "\n" +
            File.ReadAllText("Assets/Editor/PlayablePrologueGenerator.cs");

        private static string WorldSource => _worldSource ??=
            File.ReadAllText("Assets/Editor/PlayablePrologueGenerator.cs") + "\n" +
            File.ReadAllText("Assets/Scripts/Story/BossEncounterTrigger.cs");

        private static bool IsDeclaredStoryFlag(string flag)
        {
            if (flag == "ending_archive_count") return true;
            if (string.IsNullOrEmpty(flag) || DeclaredStoryFlags.Contains(flag)) return true;
            const string companionPrefix = "companion_event_";
            if (flag.StartsWith(companionPrefix, StringComparison.Ordinal))
            {
                var catalog = JsonUtility.FromJson<CompanionEventCatalogData>(File.ReadAllText(CompanionEventPath));
                return catalog?.events?.Any(definition => definition != null &&
                    !string.IsNullOrWhiteSpace(definition.eventId) && definition.CompletionFlag == flag) == true;
            }
            const string prefix = "quest_";
            const string suffix = "_complete";
            if (!flag.StartsWith(prefix, StringComparison.Ordinal) || !flag.EndsWith(suffix, StringComparison.Ordinal))
                return false;
            string questId = flag.Substring(prefix.Length, flag.Length - prefix.Length - suffix.Length);
            var quests = JsonUtility.FromJson<QuestCatalogData>(File.ReadAllText(QuestPath));
            return quests?.quests?.Any(quest => quest != null && quest.questId == questId) == true;
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

        private static void ValidateQuestRewards(List<RegionData> regions, ValidationReport result)
        {
            var database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>(DatabasePath);
            if (database == null || !File.Exists(QuestPath)) return;
            var itemIds = new HashSet<string>(database.items.Where(x => x != null).Select(x => x.itemId));
            var catalog = JsonUtility.FromJson<QuestCatalogData>(File.ReadAllText(QuestPath));
            var quests = catalog?.quests ?? new List<QuestDefinition>();
            if (quests.Count != 54) result.errors.Add($"퀘스트 데이터 수 불일치: {quests.Count}/54");
            var mainQuests = quests.Where(x => x != null && !x.isSideQuest).ToList();
            var sideQuests = quests.Where(x => x != null && x.isSideQuest).ToList();
            var rumorFlags = regions.Where(x => x != null && x.isSettlement)
                .ToDictionary(x => TownServicePolicy.RumorQuestId(x.regionId),
                    x => TownServicePolicy.RumorCompletionFlag(x.regionId), StringComparer.Ordinal);
            int memoryCount = sideQuests.Count(x =>
                MemoryAwakeningService.IsRegisteredMemoryQuest(x.questId, x.completionFlag));
            int rumorCount = sideQuests.Count(x => rumorFlags.ContainsKey(x.questId));
            if (mainQuests.Count != 41 || memoryCount != 6 || rumorCount != 7 || sideQuests.Count != 13)
                result.errors.Add($"메인/개인 기억/소문 퀘스트 수 불일치: {mainQuests.Count}/41, {memoryCount}/6, {rumorCount}/7");
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
                if (quest.isSideQuest && !MemoryAwakeningService.IsRegisteredMemoryQuest(quest.questId, quest.completionFlag) &&
                    (!rumorFlags.TryGetValue(quest.questId, out string rumorFlag) || rumorFlag != quest.completionFlag))
                    result.errors.Add($"{quest.questId}: 개인 기억/소문 완료 플래그 연결 오류 ({quest.completionFlag})");
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
                        if ((type == QuestObjectiveType.Talk ||
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

        private static void ValidateStoryCompletion(ValidationReport result)
        {
            if(!File.Exists(EndingPath)||!File.Exists(DialoguePath)){result.errors.Add("본편 완주 검사: 엔딩 또는 대화 카탈로그 누락");return;}
            var endings=JsonUtility.FromJson<EndingCatalogData>(File.ReadAllText(EndingPath))?.endings??new List<EndingDefinition>();
            var dialogues=JsonUtility.FromJson<DialogueCatalogData>(File.ReadAllText(DialoguePath))?.dialogues??new List<DialogueSequence>();
            var dialogueIds=new HashSet<string>(dialogues.Where(x=>x!=null&&!string.IsNullOrWhiteSpace(x.dialogueId)).Select(x=>x.dialogueId),StringComparer.Ordinal);
            if(endings.Count!=6)result.errors.Add($"본편 완주 검사: 엔딩 수 불일치 ({endings.Count}/6)");
            if(endings.Where(x=>x!=null).Select(x=>x.endingId).Distinct(StringComparer.Ordinal).Count()!=endings.Count)
                result.errors.Add("본편 완주 검사: 엔딩 ID 누락 또는 중복");
            foreach(var ending in endings.Where(x=>x!=null))
            {
                if(string.IsNullOrWhiteSpace(ending.endingId)||string.IsNullOrWhiteSpace(ending.title)||!dialogueIds.Contains(ending.dialogueId))
                    result.errors.Add($"본편 완주 검사: 엔딩 데이터·대화 연결 누락 ({ending.endingId})");
                foreach(var requirement in ending.requirements??new List<EndingFlagRequirement>())
                {
                    if(requirement==null||string.IsNullOrWhiteSpace(requirement.flag)){result.errors.Add($"{ending.endingId}: 빈 엔딩 요구 조건");continue;}
                    if(requirement.maxValue!=0&&requirement.maxValue<requirement.minValue)
                        result.errors.Add($"{ending.endingId}: 도달 불가능한 엔딩 범위 ({requirement.flag})");
                    if(!IsDeclaredStoryFlag(requirement.flag))result.errors.Add($"{ending.endingId}: 생성되지 않는 엔딩 플래그 ({requirement.flag})");
                }
            }
            string[] variableFlags=endings.SelectMany(x=>x?.requirements??new List<EndingFlagRequirement>()).Where(x=>x!=null)
                .Select(x=>x.flag).Where(x=>x!="story_finale_unlocked").Distinct(StringComparer.Ordinal).ToArray();
            var reached=new HashSet<string>(StringComparer.Ordinal);int[] samples={-3,-2,-1,0,1,2,3,4};
            foreach(var assignment in EnumerateAssignments(variableFlags,samples,0,new Dictionary<string,int>(StringComparer.Ordinal)))
            {
                assignment["story_finale_unlocked"]=1;EndingDefinition selected=endings.Where(x=>x!=null&&EndingMatches(x,assignment)).OrderByDescending(x=>x.priority).FirstOrDefault();if(selected!=null)reached.Add(selected.endingId);
            }
            foreach(var ending in endings.Where(x=>x!=null&&!reached.Contains(x.endingId)))result.errors.Add($"본편 완주 검사: 우선순위·조건상 선택 불가능한 엔딩 ({ending.endingId})");
        }

        private static IEnumerable<Dictionary<string,int>> EnumerateAssignments(string[] flags,int[] values,int index,Dictionary<string,int> current)
        {if(index>=flags.Length){yield return new Dictionary<string,int>(current,StringComparer.Ordinal);yield break;}foreach(int value in values){current[flags[index]]=value;foreach(var result in EnumerateAssignments(flags,values,index+1,current))yield return result;}current.Remove(flags[index]);}
        private static bool EndingMatches(EndingDefinition ending,IReadOnlyDictionary<string,int> flags)
        {foreach(var requirement in ending.requirements??new List<EndingFlagRequirement>()){if(requirement==null||string.IsNullOrEmpty(requirement.flag))continue;int value=flags.TryGetValue(requirement.flag,out int found)?found:0;if(value<requirement.minValue||requirement.maxValue!=0&&value>requirement.maxValue)return false;}return true;}

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
            var database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>(DatabasePath);
            foreach (ShadowData shadow in database?.shadows ?? new List<ShadowData>())
            foreach (Sprite frame in shadow?.fieldFrames ?? Array.Empty<Sprite>())
            {
                string path = AssetDatabase.GetAssetPath(frame);
                if (path.StartsWith("Assets/Art/Final/Field/", StringComparison.Ordinal) &&
                    !FinalPixelArtPipeline.IsGameplayReadableFieldFrame(path, out string reason))
                    result.errors.Add($"{shadow.shadowId}: 깨짐 위험 필드 프레임 연결 ({reason})");
            }
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
            if (core.GetComponent<DesktopPlatformController>() == null)
                result.errors.Add("CoreSystems PC 플랫폼 컨트롤러 누락");
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
            if(prefab.GetComponentsInChildren<MobileSafeArea>(true).Length<4)
                result.errors.Add("타이틀·엔딩·설정·시작 선택 화면 안전영역 보정 누락");
            var so = new SerializedObject(title);
            if(so.FindProperty("starterLevel")?.intValue!=10)
                result.errors.Add("새 게임 스타터 레벨이 10이 아님");
            foreach (string property in new[]
                     {
                         "titleRoot", "continueButton", "newCycleButton", "newGameConfirmRoot",
                         "saveSummaryText", "feedbackText", "starterSelection", "endingGallery", "settingsPanel"
                     })
                if (so.FindProperty(property)?.objectReferenceValue == null)
                    result.errors.Add($"타이틀 UI {property} 참조 누락");
            SettingsPanelController settings = prefab.GetComponent<SettingsPanelController>();
            if (settings == null) result.errors.Add("타이틀 PC 화면 설정 컨트롤러 누락");
            else
            {
                var settingsSo = new SerializedObject(settings);
                foreach (string property in new[]
                         { "desktopDisplayRoot", "screenModeValue", "resolutionValue", "vSyncValue" })
                    if (settingsSo.FindProperty(property)?.objectReferenceValue == null)
                        result.errors.Add($"PC 화면 설정 {property} 참조 누락");
            }
        }

        private static void ValidateScenes(List<RegionData> regions, ValidationReport result)
        {
            var portalLinks = new List<PortalValidationLink>();
            var traversalRouteCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var reachTargets = new HashSet<string>(StringComparer.Ordinal);
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
                    foreach (QuestAreaTrigger area in roots.SelectMany(x => x.GetComponentsInChildren<QuestAreaTrigger>(true)))
                    {
                        string areaId = new SerializedObject(area).FindProperty("areaId")?.stringValue;
                        if (!string.IsNullOrEmpty(areaId)) reachTargets.Add(areaId);
                    }
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
                        var groundMap=gridSo.FindProperty("groundTilemap")?.objectReferenceValue as UnityEngine.Tilemaps.Tilemap;
                        if(groundMap!=null)
                        {
                            BoundsInt bounds=groundMap.cellBounds;bool edgeLeak=false;
                            for(int x=bounds.xMin;x<bounds.xMax&&!edgeLeak;x++)
                                edgeLeak=!fieldGrid.IsBlocked(new Vector2Int(x,bounds.yMin),null,false)||
                                    !fieldGrid.IsBlocked(new Vector2Int(x,bounds.yMax-1),null,false);
                            for(int y=bounds.yMin;y<bounds.yMax&&!edgeLeak;y++)
                                edgeLeak=!fieldGrid.IsBlocked(new Vector2Int(bounds.xMin,y),null,false)||
                                    !fieldGrid.IsBlocked(new Vector2Int(bounds.xMax-1,y),null,false);
                            if(edgeLeak)result.errors.Add($"{region.sceneName}: 필드 외곽 충돌 틈 발견");
                        }
                    }
                    PlayerController fieldPlayer = roots.SelectMany(x => x.GetComponentsInChildren<PlayerController>(true)).FirstOrDefault();
                    if (fieldPlayer == null) result.errors.Add($"{region.sceneName}: Player 누락");
                    else if (fieldPlayer.GetComponent<PixelFieldAnimator>() == null)
                        result.errors.Add($"{region.sceneName}: 보폭 연동 플레이어 애니메이터 누락");
                    else
                    {
                        PixelCharacterMotion bodyMotion = fieldPlayer.GetComponentInChildren<PixelCharacterMotion>(true);
                        if (bodyMotion == null || bodyMotion.transform == fieldPlayer.transform)
                            result.errors.Add($"{region.sceneName}: 플레이어 시각 몸체·충돌 좌표 분리 모션 누락");
                        if (fieldPlayer.GetComponent<FieldTraversalEffects>() == null)
                            result.errors.Add($"{region.sceneName}: 풀·물 지형 반응 효과 누락");
                        if (fieldPlayer.GetComponent<FieldEnvironmentReactor>() == null)
                            result.errors.Add($"{region.sceneName}: 환경 연쇄 반응 컨트롤러 누락");
                        if (fieldPlayer.GetComponent<FieldInteractionFeedback>() == null)
                            result.errors.Add($"{region.sceneName}: 상호작용 대상 피드백 누락");
                        if (fieldPlayer.GetComponent<FieldBoundaryGuard>() == null)
                            result.errors.Add($"{region.sceneName}: 필드 경계 이탈 복구 누락");
                        if (fieldPlayer.GetComponent<FieldArrivalPresentation>() == null)
                            result.errors.Add($"{region.sceneName}: 지역 도착 정착 연출 누락");
                        if(fieldPlayer.GetComponent<FieldItemAcquisitionFeedback>()==null)
                            result.errors.Add($"{region.sceneName}: 도구 획득 머리 위 피드백 누락");
                        if(fieldPlayer.GetComponent<FieldDepthSorter>()==null)
                            result.errors.Add($"{region.sceneName}: 플레이어 Y축 깊이 정렬 누락");
                        if(fieldPlayer.GetComponent<FieldExplorationMoments>()==null)
                            result.errors.Add($"{region.sceneName}: 지역 풍경 탐험 기록 누락");
                        if(fieldPlayer.GetComponent<FieldTrailGuide>()==null)
                            result.errors.Add($"{region.sceneName}: 빈 조사 길찾기 안내 누락");
                        if(fieldPlayer.GetComponent<FieldPlayerIdleLife>()==null)
                            result.errors.Add($"{region.sceneName}: 플레이어 생활 대기 동작 누락");
                    }
                    if(!roots.SelectMany(x=>x.GetComponentsInChildren<FieldAmbientLife>(true)).Any())
                        result.errors.Add($"{region.sceneName}: 필드 미세 생활 연출 누락");
                    if(!roots.SelectMany(x=>x.GetComponentsInChildren<FieldProximityAmbience>(true)).Any())
                        result.errors.Add($"{region.sceneName}: 거리 기반 환경음 누락");
                    if(region.isSettlement)
                    {
                        if(!roots.SelectMany(x=>x.GetComponentsInChildren<TownLifeController>(true)).Any())
                            result.errors.Add($"{region.sceneName}: 마을 생활 스케줄 누락");
                        int townProps=roots.SelectMany(x=>x.GetComponentsInChildren<TownLifeProp>(true)).Count();
                        if(townProps<10)result.errors.Add($"{region.sceneName}: 마을 생활 오브젝트 부족 ({townProps}/10)");
                        if(!roots.SelectMany(x=>x.GetComponentsInChildren<FieldPushableCrate>(true)).Any())
                            result.errors.Add($"{region.sceneName}: 밀기 상자 환경 상호작용 누락");
                        int scheduledResidents=roots.SelectMany(x=>x.GetComponentsInChildren<TownNpcPatrol>(true)).Count();
                        if(scheduledResidents<5)
                            result.errors.Add($"{region.sceneName}: 시간대별 생활 동선 주민 부족 ({scheduledResidents}/5)");
                        if(!roots.SelectMany(x=>x.GetComponentsInChildren<TownInteriorController>(true)).Any())
                            result.errors.Add($"{region.sceneName}: 이동 가능한 마을 실내 공간 누락");
                        if(!roots.SelectMany(x=>x.GetComponentsInChildren<TownResidentRequest>(true)).Any())
                            result.errors.Add($"{region.sceneName}: 주민 일일 부탁·호감도 누락");
                        if(!roots.SelectMany(x=>x.GetComponentsInChildren<FieldJournalStand>(true)).Any())
                            result.errors.Add($"{region.sceneName}: 탐험 수첩 열람대 누락");
                    }
                    if(!region.isBossStage&&!roots.SelectMany(x=>x.GetComponentsInChildren<FieldScenicLandmark>(true)).Any())
                        result.errors.Add($"{region.sceneName}: 비전투 지역 명소 누락");
                    if(region.AllowsRandomEncounters&&!roots.SelectMany(x=>x.GetComponentsInChildren<FieldLeverBridgePuzzle>(true)).Any())
                        result.errors.Add($"{region.sceneName}: 길 복구 레버 퍼즐 누락");
                    if(region.AllowsRandomEncounters&&!roots.SelectMany(x=>x.GetComponentsInChildren<FieldForageNode>(true)).Any())
                        result.errors.Add($"{region.sceneName}: 일일 지역 채집 지점 누락");
                    if(region.AllowsRandomEncounters&&!roots.SelectMany(x=>x.GetComponentsInChildren<FieldEcologySpot>(true)).Any())
                        result.errors.Add($"{region.sceneName}: 낚시·곤충·생태 기록 지점 누락");
                    if(region.AllowsRandomEncounters&&!roots.SelectMany(x=>x.GetComponentsInChildren<FieldCampfire>(true)).Any())
                        result.errors.Add($"{region.sceneName}: 야영·시간 경과 지점 누락");
                    if((region.isDungeon||region.isHidden)&&
                       roots.SelectMany(x=>x.GetComponentsInChildren<FieldSecretPassage>(true)).Count()<2)
                        result.errors.Add($"{region.sceneName}: 영구 양방향 비밀 통로 누락");
                    if (!roots.SelectMany(x => x.GetComponentsInChildren<FieldCameraFollow>(true)).Any())
                        result.errors.Add($"{region.sceneName}: 이동 방향 카메라 추적 누락");
                    foreach (StoryNpc npc in roots.SelectMany(x => x.GetComponentsInChildren<StoryNpc>(true)))
                    {
                        if (npc.GetComponent<FieldNpcPresence>() == null)
                            result.errors.Add($"{region.sceneName}: NPC 시선·발견 반응 누락 ({npc.name})");
                        if (npc.GetComponent<FieldDepthSorter>() == null)
                            result.errors.Add($"{region.sceneName}: NPC Y축 깊이 정렬 누락 ({npc.name})");
                    }
                    if (!roots.Any(x => x.GetComponentInChildren<GroundContactShadow>(true) != null))
                        result.errors.Add($"{region.sceneName}: 필드 접지 그림자 누락");
                    if (!roots.Any(x => x.GetComponentInChildren<BattleStageBackdrop>(true) != null))
                        result.errors.Add($"{region.sceneName}: 지역별 출시 전투 배경 누락");
                    if (!roots.Any(x => x.GetComponentInChildren<GameFlowController>(true) != null)) result.errors.Add($"{region.sceneName}: GameFlow 누락");
                    int endingTriggers=roots.SelectMany(x=>x.GetComponentsInChildren<EndingTrigger>(true)).Count();
                    if(region.sceneName=="FinalCurtain"&&endingTriggers!=1)result.errors.Add($"{region.sceneName}: 최종 엔딩 상호작용 수 불일치 ({endingTriggers}/1)");
                    if(region.sceneName!="FinalCurtain"&&endingTriggers!=0)result.errors.Add($"{region.sceneName}: 최종막 외 엔딩 상호작용 배치됨");
                    GameObject fieldRoot = roots.FirstOrDefault(x => x.GetComponentInChildren<FieldGrid>(true) != null);
                    BattleUIController battleUi = roots.SelectMany(x => x.GetComponentsInChildren<BattleUIController>(true)).FirstOrDefault();
                    if (battleUi == null) result.errors.Add($"{region.sceneName}: 전투 UI 누락");
                    else
                    {
                        CanvasScaler battleScaler = battleUi.GetComponent<CanvasScaler>();
                        if (battleScaler == null || battleScaler.referenceResolution != new Vector2(1600f, 900f) ||
                            !Mathf.Approximately(battleScaler.matchWidthOrHeight, 1f))
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
                    int protectedFieldScreens=roots.SelectMany(x=>x.GetComponentsInChildren<MobileSafeArea>(true))
                        .Count(x=>x.GetComponentInParent<BattleUIController>()==null);
                    if(protectedFieldScreens<7)result.errors.Add($"{region.sceneName}: 모바일 안전영역 적용 화면 부족 ({protectedFieldScreens}/7)");
                    PlatformDisplayGroup[] platformGroups = roots
                        .SelectMany(x => x.GetComponentsInChildren<PlatformDisplayGroup>(true)).ToArray();
                    if (!platformGroups.Any(x => x.name == "MobileControls") ||
                        !platformGroups.Any(x => x.name == "DesktopControls"))
                        result.errors.Add($"{region.sceneName}: 모바일/PC 필드 조작 표시 전환 누락");
                    ScreenFader fader = roots.SelectMany(x => x.GetComponentsInChildren<ScreenFader>(true)).FirstOrDefault();
                    if (fader == null) result.errors.Add($"{region.sceneName}: 화면 전환 페이더 누락");
                    else
                    {
                        var faderSo = new SerializedObject(fader);
                        if (faderSo.FindProperty("overlay")?.objectReferenceValue == null ||
                            faderSo.FindProperty("encounterSilhouette")?.objectReferenceValue == null)
                            result.errors.Add($"{region.sceneName}: 랜덤 조우 색상·실루엣 전환 참조 누락");
                    }
                    RegionArrivalBanner arrival = roots.SelectMany(x => x.GetComponentsInChildren<RegionArrivalBanner>(true)).FirstOrDefault();
                    if (arrival == null) result.errors.Add($"{region.sceneName}: 지역 진입 타이틀 누락");
                    else
                    {
                        var arrivalSo = new SerializedObject(arrival);
                        foreach (string property in new[] { "root", "card", "accent", "actText", "regionText", "environmentText", "discoveryText" })
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
                            partySo.FindProperty("healButton")?.objectReferenceValue == null ||
                            partySo.FindProperty("companionButton")?.objectReferenceValue == null ||
                            partySo.FindProperty("companionPassiveText")?.objectReferenceValue == null)
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
                        if (inventory.GetComponentsInChildren<Button>(true).Any(button =>
                                button.GetComponent<HandheldButtonFeedback>() == null))
                            result.errors.Add($"{region.sceneName}: 도구 가방 버튼 촉각 피드백 누락");
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
                            pauseSo.FindProperty("journeyText")?.objectReferenceValue == null ||
                            pauseSo.FindProperty("recoverPositionButton")?.objectReferenceValue == null ||
                            pauseSo.FindProperty("titleConfirmRoot")?.objectReferenceValue == null)
                            result.errors.Add($"{region.sceneName}: 필드 메뉴 참조 누락");
                        if (pauseMenu.GetComponentsInChildren<Button>(true).Any(button =>
                                button.GetComponent<HandheldButtonFeedback>() == null))
                            result.errors.Add($"{region.sceneName}: 필드 메뉴 버튼 촉각 피드백 누락");
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
                    FieldWeatherEffects weatherEffects = roots.SelectMany(x => x.GetComponentsInChildren<FieldWeatherEffects>(true)).FirstOrDefault();
                    if (weatherEffects == null) result.errors.Add($"{region.sceneName}: 날씨 파티클·환경음 컨트롤러 누락");
                    FieldFootstepAudio footsteps = roots.SelectMany(x => x.GetComponentsInChildren<FieldFootstepAudio>(true)).FirstOrDefault();
                    if (footsteps == null) result.errors.Add($"{region.sceneName}: 발걸음 오디오 누락");
                    else
                    {
                        SerializedProperty clips = new SerializedObject(footsteps).FindProperty("clips");
                        if (clips == null || clips.arraySize < 4 || Enumerable.Range(0, clips.arraySize)
                            .Any(i => clips.GetArrayElementAtIndex(i).objectReferenceValue == null))
                            result.errors.Add($"{region.sceneName}: 발걸음 클립 누락");
                    }
                    var companionFollowers = roots.SelectMany(x => x.GetComponentsInChildren<ShadowCompanionFollower>(true)).ToArray();
                    if (companionFollowers.Length != 1)
                        result.errors.Add($"{region.sceneName}: 동행 그림자 발자국 추적기 수 불일치 ({companionFollowers.Length}/1)");
                    if (!roots.SelectMany(x => x.GetComponentsInChildren<CompanionRegionEventController>(true)).Any())
                        result.errors.Add($"{region.sceneName}: 동행 지역 이벤트 컨트롤러 누락");
                    foreach (GameObject root in roots)
                    foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject) > 0)
                            result.errors.Add($"{region.sceneName}: Missing Script ({GetPath(tr)})");

                    var portals = roots.SelectMany(x => x.GetComponentsInChildren<MapPortal>(true)).ToArray();
                    var traversalRoutes = roots.SelectMany(x => x.GetComponentsInChildren<TraversalRoute>(true)).ToArray();
                    var encounters = roots.SelectMany(x => x.GetComponentsInChildren<EncounterSymbol>(true)).ToArray();
                    var randomEncounter = roots.SelectMany(x => x.GetComponentsInChildren<RandomEncounterController>(true)).FirstOrDefault();
                    var worldState = roots.SelectMany(x => x.GetComponentsInChildren<FieldWorldStateController>(true)).FirstOrDefault();
                    var fieldIncidents = roots.SelectMany(x => x.GetComponentsInChildren<FieldIncidentController>(true)).ToArray();
                    var healers = roots.SelectMany(x => x.GetComponentsInChildren<TownHealerNpc>(true)).ToArray();
                    var rumorKeepers = roots.SelectMany(x => x.GetComponentsInChildren<TownRumorNpc>(true)).ToArray();
                    var investigators = roots.SelectMany(x => x.GetComponentsInChildren<BossInvestigatorNpc>(true)).ToArray();
                    var recoveryNpcs = roots.SelectMany(x => x.GetComponentsInChildren<TownRecoveryNpc>(true)).ToArray();
                    var restorationVisuals = roots.SelectMany(x => x.GetComponentsInChildren<TownRestorationVisualController>(true)).ToArray();
                    var townPatrols = roots.SelectMany(x => x.GetComponentsInChildren<TownNpcPatrol>(true)).ToArray();
                    var bossJournals = roots.SelectMany(x => x.GetComponentsInChildren<BossInvestigationJournalController>(true)).ToArray();
                    result.portals += portals.Length;
                    result.encounters += encounters.Length;
                    if (portals.Length == 0) result.errors.Add($"{region.sceneName}: 포털 없음");
                    if(bossJournals.Length!=1)result.errors.Add($"{region.sceneName}: 보스 조사 수첩 UI 수 불일치 ({bossJournals.Length}/1)");
                    if (worldState == null) result.errors.Add($"{region.sceneName}: 시간대·날씨 컨트롤러 누락");
                    else if (new SerializedObject(worldState).FindProperty("regionId")?.stringValue != region.regionId)
                        result.errors.Add($"{region.sceneName}: 시간대·날씨 지역 ID 불일치");
                    int expectedTownServices = region.isSettlement ? 1 : 0;
                    if (healers.Length != expectedTownServices || rumorKeepers.Length != expectedTownServices)
                        result.errors.Add($"{region.sceneName}: 회복/소문 NPC 수 불일치 " +
                                          $"({healers.Length}/{rumorKeepers.Length}, 기대 {expectedTownServices})");
                    int expectedIncidentControllers = !region.isSettlement && !region.isBossStage ? 1 : 0;
                    if (fieldIncidents.Length != expectedIncidentControllers)
                        result.errors.Add($"{region.sceneName}: 시간·날씨 필드 사건 컨트롤러 수 불일치 " +
                                          $"({fieldIncidents.Length}/{expectedIncidentControllers})");
                    if(fieldIncidents.Any(x=>x.GetComponent<Collider2D>()==null))
                        result.errors.Add($"{region.sceneName}: 필드 사건 상호작용 충돌체 누락");
                    if (fieldIncidents.Length == 1 &&
                        new SerializedObject(fieldIncidents[0]).FindProperty("regionId")?.stringValue != region.regionId)
                        result.errors.Add($"{region.sceneName}: 필드 사건 지역 ID 불일치");
                    if (investigators.Length != expectedTownServices || recoveryNpcs.Length != expectedTownServices * 2)
                        result.errors.Add($"{region.sceneName}: 조사/복구 NPC 수 불일치 " +
                                          $"({investigators.Length}/{recoveryNpcs.Length}, 기대 {expectedTownServices}/{expectedTownServices * 2})");
                    if(restorationVisuals.Length!=expectedTownServices||townPatrols.Length!=expectedTownServices*2)
                        result.errors.Add($"{region.sceneName}: 마을 복구 시각/동선 구성 불일치 " +
                                          $"({restorationVisuals.Length}/{townPatrols.Length}, 기대 {expectedTownServices}/{expectedTownServices*2})");
                    foreach (TownHealerNpc healer in healers)
                    {
                        var healerSo = new SerializedObject(healer);
                        if (healerSo.FindProperty("regionId")?.stringValue != region.regionId)
                            result.errors.Add($"{region.sceneName}: 회복 NPC 지역 ID 불일치");
                        if (fieldGrid != null && fieldGrid.IsBlocked(fieldGrid.WorldToCell(healer.transform.position),
                                healer.GetComponent<Collider2D>(), false))
                            result.errors.Add($"{region.sceneName}: 회복 NPC가 막힌 칸에 배치됨");
                    }
                    foreach (TownRumorNpc rumorKeeper in rumorKeepers)
                    {
                        var rumorSo = new SerializedObject(rumorKeeper);
                        if (rumorSo.FindProperty("regionId")?.stringValue != region.regionId ||
                            rumorSo.FindProperty("dialogueId")?.stringValue != TownServicePolicy.RumorDialogueId(region))
                            result.errors.Add($"{region.sceneName}: 소문 NPC 데이터 불일치");
                        if (fieldGrid != null && fieldGrid.IsBlocked(fieldGrid.WorldToCell(rumorKeeper.transform.position),
                                rumorKeeper.GetComponent<Collider2D>(), false))
                            result.errors.Add($"{region.sceneName}: 소문 NPC가 막힌 칸에 배치됨");
                    }
                    if (region.AllowsRandomEncounters && randomEncounter == null)
                        result.errors.Add($"{region.sceneName}: 지역 랜덤 조우 컨트롤러 누락");
                    if (!region.AllowsRandomEncounters && randomEncounter != null)
                        result.errors.Add($"{region.sceneName}: 마을/보스 무대에 랜덤 조우가 배치됨");
                    if (region.AllowsRandomEncounters && randomEncounter != null)
                    {
                        RegionalEncounterPolicy.GetCadence(region, out int expectedMin, out int expectedMax,
                            out float expectedChance, out int expectedPity);
                        var encounterSo = new SerializedObject(randomEncounter);
                        int actualMin = encounterSo.FindProperty("safeStepsMin")?.intValue ?? -1;
                        int actualMax = encounterSo.FindProperty("safeStepsMax")?.intValue ?? -1;
                        float actualChance = encounterSo.FindProperty("chancePerStep")?.floatValue ?? -1f;
                        int actualPity = encounterSo.FindProperty("pitySteps")?.intValue ?? -1;
                        if (actualMin != expectedMin || actualMax != expectedMax ||
                            !Mathf.Approximately(actualChance, expectedChance) || actualPity != expectedPity)
                            result.errors.Add($"{region.sceneName}: 지역 유형별 랜덤 조우 간격 불일치");
                    }
                    if (region.isSettlement && encounters.Length > 0)
                        result.errors.Add($"{region.sceneName}: 마을에 야생 그림자 심볼이 배치됨");
                    traversalRouteCounts[region.sceneName] = traversalRoutes.Length;
                    foreach (TraversalRoute route in traversalRoutes)
                    {
                        if (route.AllowedAbilities == TraversalAbility.None)
                            result.errors.Add($"{region.sceneName}/{route.name}: 이동 권능 누락");
                        if (fieldGrid == null || !fieldGrid.TryFindNearestWalkable(route.DestinationCell,
                                out Vector2Int routeArrival, null, 12, false) || routeArrival != route.DestinationCell)
                            result.errors.Add($"{region.sceneName}/{route.name}: 이동 도착 좌표가 막힘 {route.DestinationCell}");
                    }
                    foreach (MapPortal portal in portals)
                    {
                        var so = new SerializedObject(portal);
                        string target = so.FindProperty("targetScene")?.stringValue;
                        if (fieldGrid != null)
                        {
                            Vector2Int sourceCell = fieldGrid.WorldToCell(portal.transform.position);
                            if (fieldGrid.IsBlocked(sourceCell, portal.GetComponent<Collider2D>(), false))
                                result.errors.Add($"{region.sceneName}/{portal.name}: 포털 출구 칸이 막힘 {sourceCell}");
                        }
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
                    string[] expectedTargets = (region.connectedRegionIds ?? Array.Empty<string>())
                        .Select(id => regions.FirstOrDefault(x => x.regionId == id)?.sceneName)
                        .Where(x => !string.IsNullOrEmpty(x)).ToArray();
                    string[] actualTargets = portals.Select(x =>
                        new SerializedObject(x).FindProperty("targetScene")?.stringValue)
                        .Where(x => !string.IsNullOrEmpty(x)).ToArray();
                    foreach (var duplicate in actualTargets.GroupBy(x => x).Where(x => x.Count() > 1))
                        result.errors.Add($"{region.sceneName}: 같은 지역으로 향하는 포털 중복 ({duplicate.Key})");
                    var expectedTargetSet = new HashSet<string>(expectedTargets, StringComparer.Ordinal);
                    var actualTargetSet = new HashSet<string>(actualTargets, StringComparer.Ordinal);
                    if (!expectedTargetSet.SetEquals(actualTargetSet))
                    {
                        string missing = string.Join(",", expectedTargetSet.Except(actualTargetSet));
                        string extra = string.Join(",", actualTargetSet.Except(expectedTargetSet));
                        result.errors.Add($"{region.sceneName}: 카탈로그-씬 포털 불일치 (누락:{missing} / 초과:{extra})");
                    }
                    foreach (EncounterSymbol encounter in encounters)
                    {
                        var so = new SerializedObject(encounter);
                        if (so.FindProperty("leadShadow")?.objectReferenceValue == null)
                            result.errors.Add($"{region.sceneName}: {encounter.name} 적 데이터 누락");
                    }
                    if (region.regionId == "ashes_bell_tower" ||
                        region.regionId == "inverted_moon_path" ||
                        region.regionId == "windless_echo_cavern")
                    {
                        var puzzles = roots.SelectMany(x => x.GetComponentsInChildren<HiddenRegionSequencePuzzle>(true)).ToArray();
                        var nodes = roots.SelectMany(x => x.GetComponentsInChildren<HiddenRegionPuzzleNode>(true)).ToArray();
                        var rewards = roots.SelectMany(x => x.GetComponentsInChildren<HiddenRegionReward>(true)).ToArray();
                        var memoryNpcs = roots.SelectMany(x => x.GetComponentsInChildren<StoryNpc>(true))
                            .Where(x => x.name == "MemoryNpc").ToArray();
                        if (puzzles.Length != 1)
                            result.errors.Add($"{region.sceneName}: 숨은 지역 순서 퍼즐 수 불일치 ({puzzles.Length}/1)");
                        if (nodes.Length != 3)
                            result.errors.Add($"{region.sceneName}: 퍼즐 입력 장치 수 불일치 ({nodes.Length}/3)");
                        if (rewards.Length != 1)
                            result.errors.Add($"{region.sceneName}: 일회성 최종 보상 수 불일치 ({rewards.Length}/1)");
                        if (memoryNpcs.Length != 1)
                            result.errors.Add($"{region.sceneName}: 기억 NPC 수 불일치 ({memoryNpcs.Length}/1)");
                        int fixedEncounters = encounters.Count(x => x.IsFixed);
                        if (fixedEncounters != 1)
                            result.errors.Add($"{region.sceneName}: 희귀 고정 조우 수 불일치 ({fixedEncounters}/1)");
                        else
                        {
                            EncounterSymbol hiddenBoss = encounters.First(x => x.IsFixed);
                            BattleGimmick expectedGimmick = region.regionId == "ashes_bell_tower"
                                ? BattleGimmick.BellToll
                                : region.regionId == "inverted_moon_path"
                                    ? BattleGimmick.MoonReversal
                                    : BattleGimmick.EchoSeal;
                            if (hiddenBoss.Mode != BattleMode.Boss || hiddenBoss.Gimmick != expectedGimmick)
                                result.errors.Add($"{region.sceneName}: 숨은 보스 전투 모드·기믹 불일치");
                        }
                    }
                }
                finally { if (closeAfter) EditorSceneManager.CloseScene(scene, true); }
            }
            if (!traversalRouteCounts.TryGetValue("FrostPort", out int frostRoutes) || frostRoutes != 2)
                result.errors.Add($"FrostPort: 서리 수로 이동 경로 수 불일치 ({frostRoutes}/2)");
            if (!traversalRouteCounts.TryGetValue("GlassCoast", out int boatRoutes) || boatRoutes != 2)
                result.errors.Add($"GlassCoast: 고무보트 이동 경로 수 불일치 ({boatRoutes}/2)");
            if (File.Exists(QuestPath))
            {
                var quests = JsonUtility.FromJson<QuestCatalogData>(File.ReadAllText(QuestPath));
                foreach (QuestDefinition quest in quests?.quests ?? new List<QuestDefinition>())
                foreach (QuestObjectiveDefinition objective in quest?.objectives ?? new List<QuestObjectiveDefinition>())
                    if (objective != null && objective.TryGetType(out QuestObjectiveType type) &&
                        type == QuestObjectiveType.Reach && objective.targetId != "*" && !reachTargets.Contains(objective.targetId))
                        result.errors.Add($"{quest.questId}: 생성 씬에 배치되지 않은 Reach 대상 {objective.targetId}");
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
        [Serializable] private class RosterEntry
        {
            public string shadowId, regionId, sceneName;
            public int minLevel, maxLevel;
            public bool featured;
        }
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

        public sealed class CompletionAuditSnapshot
        {
            public readonly int regions,scenes,shadows,portals,encounters;
            public readonly List<string> errors,warnings;
            public bool Passed=>errors.Count==0;
            public CompletionAuditSnapshot(int regions,int scenes,int shadows,int portals,int encounters,List<string> errors,List<string>warnings)
            {this.regions=regions;this.scenes=scenes;this.shadows=shadows;this.portals=portals;this.encounters=encounters;this.errors=errors;this.warnings=warnings;}
        }
    }
}
#endif
