#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using ShadowTheater.Data;
using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShadowTheater.EditorTools
{
    public static class QuestRecordRegressionChecks
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        public static void Validate(List<string> errors)
        {
            QuestRepository.Reload();
            if (!QuestRepository.TryGet("prologue_03_record", out var first) ||
                !QuestRepository.TryGet("prologue_04_echoes", out var echoes))
            { errors.Add("기록 퀘스트 정의 누락"); return; }
            var firstRecord = first.objectives.Find(x => x.objectiveId == "record_shadow");
            var more = echoes.objectives.Find(x => x.objectiveId == "record_more");
            Check(firstRecord?.countKnownRecords == true && firstRecord.excludeStarterRecord,
                "첫 포획은 시작 그림자를 제외한 기존 기록을 인정해야 함", errors);
            Check(more?.countKnownRecords == true && !more.excludeStarterRecord && more.requiredCount == 3,
                "초원 누적 목표는 첫 그림자를 포함한 총 3종이어야 함", errors);
            if (firstRecord == null || more == null) return;
            var save = Fixture(false);
            var old = new QuestObjectiveProgressData { objectiveId = "record_more", current = 1,
                cumulativeBaselineSet = true, cumulativeBaselineCounter = 2, cumulativeBaselineProgress = 0 };
            Check(QuestProgressRecovery.TryGetCumulativeProgress(save, more, old, out int recovered) && recovered == 3,
                "구버전 기준점에 묶인 총 3종 기록 복구 실패", errors);
            save.recordedShadowIds = new List<string> { "knight", "knight", "", "puppet", "puppet" };
            Check(QuestProgressRecovery.TryGetCumulativeProgress(save, more, old, out recovered) && recovered == 2,
                "중복 종·빈 ID가 수집 종수에 포함됨", errors);
            save.recordedShadowIds = new List<string> { "knight" };
            Check(QuestProgressRecovery.TryGetCumulativeProgress(save, firstRecord, null, out recovered) && recovered == 0,
                "시작 그림자만으로 첫 포획이 완료됨", errors);
            save.recordedShadowIds.Add("puppet");
            Check(QuestProgressRecovery.TryGetCumulativeProgress(save, firstRecord, null, out recovered) && recovered == 1,
                "퀘스트 전에 잡은 첫 그림자를 인정하지 않음", errors);
            ValidateManager(errors);
        }

        private static void ValidateManager(List<string> errors)
        {
            var previousSave = SaveManager.Instance;
            var previousQuest = QuestManager.Instance;
            var host = new GameObject("QuestRecordRegressionFixture");
            var saveManager = host.AddComponent<SaveManager>();
            var manager = host.AddComponent<QuestManager>();
            try
            {
                SetSingleton(typeof(SaveManager), saveManager); SetSingleton(typeof(QuestManager), manager);
                typeof(SaveManager).GetField("_suppressSaveWrites", Hidden).SetValue(saveManager, true);
                // Existing stuck save: starter + the only two available wild species are already recorded.
                var save = Fixture(true); SetSave(saveManager, manager, save); Initialize(manager);
                var progress = manager.GetProgress("prologue_04_echoes");
                Check(progress.completed && progress.rewardClaimed && save.gold == 300 &&
                    manager.GetProgress("prologue_05_boss") != null,
                    "기존 막힌 세이브를 불러와도 보스 퀘스트가 열리지 않음", errors);
                ResetInitialization(manager); Initialize(manager);
                Check(save.gold == 300, "재접속 시 복구 보상이 중복 지급됨", errors);

                save = Fixture(false); SetSave(saveManager, manager, save); Initialize(manager);
                progress = manager.GetProgress("prologue_04_echoes");
                Check(!progress.completed && manager.GetObjectiveProgress("prologue_04_echoes", "record_more").current == 3,
                    "기존 기록 복구 시 아직 듣지 않은 등불지기 대화까지 완료 처리함", errors);
                manager.Notify(QuestObjectiveType.Talk, "npc_lantern_keeper");
                Check(progress.completed && save.gold == 300 && manager.GetProgress("prologue_05_boss") != null,
                    "기록 복구 뒤 등불지기와 대화해도 진행되지 않음", errors);

                // Capture before quest acceptance: new quest immediately starts at 2/3, not 0/3.
                save = Fixture(false); save.quests.RemoveAll(x => x.questId == "prologue_04_echoes" || x.questId == "prologue_03_record");
                save.recordedShadowIds.Remove("crow"); SetSave(saveManager, manager, save);
                manager.TryStartQuest("prologue_03_record", false);
                Check(manager.GetProgress("prologue_03_record").completed &&
                    manager.GetObjectiveProgress("prologue_04_echoes", "record_more").current == 2,
                    "미리 포획한 기록이 다음 퀘스트 시작 진행도에 반영되지 않음", errors);
                manager.Notify(QuestObjectiveType.Record, "puppet");
                Check(manager.GetObjectiveProgress("prologue_04_echoes", "record_more").current == 2,
                    "동일 종의 중복 이벤트로 총 수집 종수가 증가함", errors);
                save.recordedShadowIds.Add("crow"); manager.Notify(QuestObjectiveType.Record, "crow");
                manager.Notify(QuestObjectiveType.Talk, "npc_lantern_keeper");
                Check(manager.GetProgress("prologue_04_echoes").completed && save.recordedShadowIds.Count == 3,
                    "두 야생 종만 있는 최소 출현 목록으로 퀘스트를 완료할 수 없음", errors);
            }
            catch (Exception exception) { errors.Add("기록 퀘스트 실제 진행 회귀: " + exception); }
            finally
            {
                SetSingleton(typeof(SaveManager), previousSave); SetSingleton(typeof(QuestManager), previousQuest);
                Object.DestroyImmediate(host);
            }
        }

        private static SaveData Fixture(bool talked)
        {
            var save = new SaveData { saveGuid = Guid.NewGuid().ToString("N"), starterShadowId = "knight", gold = 100,
                mapId = "MoonlitMeadow", recordedShadowIds = new List<string> { "knight", "puppet", "crow" } };
            foreach (string id in save.recordedShadowIds)
            {
                var data = AssetDatabase.LoadAssetAtPath<ShadowData>($"Assets/Data/Generated/Shadows/{id}.asset");
                if (data != null) save.party.Add(new ShadowInstance(data, 5));
            }
            foreach (string id in new[] { "prologue_01_awaken", "prologue_02_meadow", "prologue_03_record" })
                save.quests.Add(new QuestProgressData { questId = id, completed = true, rewardClaimed = true });
            save.quests.Add(new QuestProgressData { questId = "prologue_04_echoes", objectives = new List<QuestObjectiveProgressData>
                { new QuestObjectiveProgressData { objectiveId = "record_more", current = 1, cumulativeBaselineSet = true,
                    cumulativeBaselineCounter = 2 }, new QuestObjectiveProgressData { objectiveId = "talk_keeper", current = 0 } } });
            if (talked) save.flags.Add(new FlagEntry { key = "talked_lantern_keeper", value = 1 });
            return save;
        }
        private static void SetSave(SaveManager manager, QuestManager quests, SaveData save)
        { typeof(SaveManager).GetField("_current", Hidden).SetValue(manager, save); ResetInitialization(quests); }
        private static void ResetInitialization(QuestManager manager) => typeof(QuestManager).GetField("_initializedSaveGuid", Hidden).SetValue(manager, null);
        private static void Initialize(QuestManager manager) => typeof(QuestManager).GetMethod("InitializeCurrentSave", Hidden).Invoke(manager, new object[] { false });
        private static void SetSingleton(Type type, object value) => type.GetProperty("Instance").SetValue(null, value);
        private static void Check(bool condition, string message, List<string> errors) { if (!condition) errors.Add(message); }

        public static void BuildForCi()
        {
            MobileBuildConfigurator.Apply();
            var errors = new List<string>(); Validate(errors);
            if (errors.Count > 0) throw new BuildFailedException(string.Join("\n", errors));
            Debug.Log("[QuestRecordRegression] PASS: existing save recovery, two-species roster, pre-capture credit, duplicate/reward idempotence");
            ReleaseReadinessBatch.BuildAndroidQaForCi();
        }
    }
}
#endif
