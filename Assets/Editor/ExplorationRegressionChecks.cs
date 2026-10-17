#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShadowTheater.EditorTools
{
    public static class ExplorationRegressionChecks
    {
        public static void Validate(List<string> errors)
        {
            var previous = SaveManager.Instance;
            var host = new GameObject("ExplorationRegressionFixture");
            var manager = host.AddComponent<SaveManager>();
            try
            {
                typeof(SaveManager).GetProperty("Instance").SetValue(null, manager);
                var save = new SaveData { mapId = "PrologueTheater" };
                typeof(SaveManager).GetField("_current", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(manager, save);
                Check(RegionProgress.MeetsTravelRequirement("EchoVillage", "quest_prologue_01_awaken_complete"),
                    "아리아와 대화하기 전에 일반 길을 탐험할 수 없음", errors);
                // Walk the real route with no completed quests, captures or NPC conversations.
                foreach (string scene in new[] { "PrologueTheater", "EchoVillage", "MoonlitMeadow", "MoonlitBossStage" })
                {
                    Check(RegionProgress.CanEnterScene(scene), "수집 퀘스트가 초반 탐험을 막음: " + scene, errors);
                    save.mapId = scene;
                    RegionProgress.MarkVisitedScene(scene);
                }
                Check(!RegionProgress.CanEnterScene("CurtainPass"), "보스 승리 전에 관문이 열림", errors);
                Check(!RegionProgress.MeetsTravelRequirement("CurtainPass", "boss_moonlit_story_complete"),
                    "포털이 보스 조건을 무시함", errors);
                SaveManager.SetFlag("boss_moonlit_story_complete");
                Check(RegionProgress.CanEnterScene("CurtainPass"), "보스 승리 후에도 길이 닫혀 있음", errors);
                save.mapId = "CurtainPass";
                RegionProgress.MarkVisitedScene(save.mapId);
                Check(RegionProgress.CanEnterScene("AshBorder"), "이동 퀘스트가 다음 지역으로 가는 길을 막음", errors);
                Check(!RegionProgress.CanEnterScene("FrostPort"), "미발견 원거리로 건너뛸 수 있음", errors);
                Check(!RegionProgress.MeetsTravelRequirement("FrostPort", "act2_complete"), "막별 보스 관문 조건을 무시함", errors);
                Check(!RegionProgress.MeetsTravelRequirement("AshesBellTower", "companion_event_crow_wagon_song_complete"),
                    "숨은 길 발견 조건을 무시함", errors);
                Check(!RegionProgress.MeetsTravelRequirement("AshesBellTower", "quest_test_complete"),
                    "숨은 길의 퀘스트 조건까지 무시함", errors);
                Check(!RegionProgress.MeetsTravelRequirement("MissingScene", "quest_test_complete"),
                    "알 수 없는 목적지를 해금함", errors);
                Check(!SaveManager.HasFlag("quest_prologue_04_echoes_complete") && save.quests.Count == 0,
                    "탐험만으로 미달성 퀘스트를 완료 처리함", errors);
                Check(RegionProgress.CanEnterScene("MoonlitMeadow"), "탐험 후 방문했던 초원으로 돌아갈 수 없음", errors);
                if (errors.Count == 0) Debug.Log("[ExplorationRegression] PASS: checklist-independent routes, boss gates, hidden paths, backtracking, no quest mutation");
            }
            finally
            {
                Object.DestroyImmediate(host);
                typeof(SaveManager).GetProperty("Instance").SetValue(null, previous);
            }
        }

        private static void Check(bool condition, string message, List<string> errors)
        { if (!condition) errors.Add(message); }

        public static void BuildForCi()
        {
            MobileBuildConfigurator.Apply();
            ReleaseReadinessBatch.BuildAndroidQaForCi();
        }
    }
}
#endif
