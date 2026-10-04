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

namespace ShadowTheater.EditorTools
{
    /// <summary>180종 데이터와 40개 필드의 생성 결과를 출시 전 일괄 검사한다.</summary>
    public static class GameRegressionValidator
    {
        private const string SceneFolder = "Assets/Scenes/Prologue";
        private const string RegionPath = "Assets/Resources/Data/RegionCatalog.json";
        private const string RosterPath = "Assets/Resources/Data/RegionalRosterManifest.json";
        private const string DatabasePath = "Assets/Resources/ShadowDatabase.asset";

        [MenuItem("Tools/Shadow Theater/Validate Full Game")]
        public static void ValidateFromMenu()
        {
            var report = Run();
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
            PlayablePrologueGenerator.Generate();
            ValidateFromMenu();
        }

        public static void ValidateForCi()
        {
            var report = Run();
            if (report.errors.Count > 0)
                throw new BuildFailedException("Shadow Theater regression failed:\n" + string.Join("\n", report.errors));
        }

        private static ValidationReport Run()
        {
            var result = new ValidationReport();
            ValidateMigration(result);
            ValidateBattleAi(result);
            ValidateBattleRewards(result);
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
            ValidateFinalArt(result);
            ValidateMusic(result);
            ValidateScenes(regions, result);
            ValidateBuildSettings(regions, result);
            return result;
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
            try
            {
                var instance = new ShadowInstance(data, ShadowInstance.MaxLevel - 1);
                int ups = instance.AddExp(instance.ExpToNext * 2);
                if (ups != 1 || instance.level != ShadowInstance.MaxLevel || instance.exp != 0 ||
                    instance.ExpToNext != 0 || !Mathf.Approximately(instance.ExpProgress, 1f) ||
                    instance.AddExp(9999) != 0)
                    result.errors.Add("그림자 최대 레벨·경험치 회귀 실패");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(data);
            }
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
            result.shadows = database.shadows.Count(x => x != null);
            if (result.shadows != 180) result.errors.Add($"그림자 데이터 수 불일치: {result.shadows}/180");
            var ids = database.shadows.Where(x => x != null).Select(x => x.shadowId).ToList();
            if (ids.Distinct().Count() != ids.Count) result.errors.Add("ShadowDatabase shadowId 중복");
            var ultimates = database.skills.Where(x => x != null && x.isUltimate).ToList();
            if (ultimates.Count == 0) result.errors.Add("진명 필살기 데이터 없음");
            foreach (SkillData skill in ultimates)
            {
                if (skill.ultimateFxStyle == UltimateFxStyle.None) result.errors.Add($"{skill.skillId}: 필살기 테마 누락");
                if (skill.fxPrefab == null) result.errors.Add($"{skill.skillId}: FX 프리팹 누락");
                if (skill.sfxClip == null) result.errors.Add($"{skill.skillId}: 캐스트 SFX 누락");
                if (skill.impactSfxClip == null) result.errors.Add($"{skill.skillId}: 타격 SFX 누락");
            }
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
            SerializedProperty clips = new SerializedObject(director).FindProperty("clips");
            int expected = Enum.GetValues(typeof(MusicCue)).Length;
            if (clips == null || clips.arraySize != expected)
            { result.errors.Add($"음악 큐 수 불일치: {clips?.arraySize ?? 0}/{expected}"); return; }
            for (int i = 0; i < clips.arraySize; i++)
                if (clips.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    result.errors.Add($"음악 클립 누락: {(MusicCue)i}");
        }

        private static void ValidateScenes(List<RegionData> regions, ValidationReport result)
        {
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
                    if (!roots.Any(x => x.GetComponentInChildren<FieldGrid>(true) != null)) result.errors.Add($"{region.sceneName}: FieldGrid 누락");
                    if (!roots.Any(x => x.GetComponentInChildren<PlayerController>(true) != null)) result.errors.Add($"{region.sceneName}: Player 누락");
                    if (!roots.Any(x => x.GetComponentInChildren<GameFlowController>(true) != null)) result.errors.Add($"{region.sceneName}: GameFlow 누락");
                    BattleUIController battleUi = roots.SelectMany(x => x.GetComponentsInChildren<BattleUIController>(true)).FirstOrDefault();
                    if (battleUi == null) result.errors.Add($"{region.sceneName}: 전투 UI 누락");
                    else
                    {
                        var battleSo = new SerializedObject(battleUi);
                        if (battleSo.FindProperty("playerPartyStrip")?.objectReferenceValue == null ||
                            battleSo.FindProperty("enemyPartyStrip")?.objectReferenceValue == null ||
                            battleSo.FindProperty("optionContent")?.objectReferenceValue == null)
                            result.errors.Add($"{region.sceneName}: 전투 파티 스트립·선택 목록 참조 누락");
                        foreach (BattlePartyStrip strip in battleUi.GetComponentsInChildren<BattlePartyStrip>(true))
                        {
                            SerializedProperty slots = new SerializedObject(strip).FindProperty("slots");
                            if (slots == null || slots.arraySize != SaveData.MaxPartySize)
                                result.errors.Add($"{region.sceneName}: 전투 파티 스트립 슬롯 수 불일치");
                        }
                    }
                    PartyStorageController partyStorage = roots.SelectMany(x => x.GetComponentsInChildren<PartyStorageController>(true)).FirstOrDefault();
                    if (partyStorage == null) result.errors.Add($"{region.sceneName}: 파티·각본 서고 UI 누락");
                    else
                    {
                        var partySo = new SerializedObject(partyStorage);
                        if (partySo.FindProperty("root")?.objectReferenceValue == null ||
                            partySo.FindProperty("partyContent")?.objectReferenceValue == null ||
                            partySo.FindProperty("storageContent")?.objectReferenceValue == null)
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
                    FieldPauseMenuController pauseMenu = roots.SelectMany(x => x.GetComponentsInChildren<FieldPauseMenuController>(true)).FirstOrDefault();
                    if (pauseMenu == null) result.errors.Add($"{region.sceneName}: 필드 메뉴 누락");
                    else
                    {
                        var pauseSo = new SerializedObject(pauseMenu);
                        if (pauseSo.FindProperty("root")?.objectReferenceValue == null ||
                            pauseSo.FindProperty("masterSlider")?.objectReferenceValue == null ||
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
        private class ValidationReport
        {
            public int regions, scenes, shadows, portals, encounters;
            public readonly List<string> errors = new List<string>();
            public readonly List<string> warnings = new List<string>();
        }
    }
}
#endif
