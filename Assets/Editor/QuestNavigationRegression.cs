#if UNITY_EDITOR
using System.Collections.Generic;
using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    public static class QuestNavigationRegression
    {
        public static void Validate(List<string> errors)
        {
            var targets = QuestGuidanceService.Targets;
            var keys = new HashSet<string>();
            foreach (var t in targets)
            {
                if (t == null || !keys.Add(t.type + "/" + t.targetId) || RegionRepository.Get(t.regionId) == null || string.IsNullOrEmpty(t.label))
                    errors.Add("퀘스트 안내 대상 중복/지역/이름 오류");
            }
            foreach (var quest in QuestRepository.All)
            foreach (var objective in quest.objectives)
                if (objective.targetId != "*" && !keys.Contains(objective.type + "/" + objective.targetId))
                    errors.Add("퀘스트 위치 안내 누락: " + quest.questId + "/" + objective.objectiveId);

            var main = new QuestDefinition { questId = "navigation_main" };
            var side = new QuestDefinition { questId = "navigation_side", isSideQuest = true };
            var definitions = new[] { side, main };
            var progress = new List<QuestProgressData>
            { new QuestProgressData { questId = side.questId }, new QuestProgressData { questId = main.questId } };
            if (QuestTrackingPolicy.Resolve(definitions, progress, null) != main ||
                QuestTrackingPolicy.Resolve(definitions, progress, side.questId) != side)
                errors.Add("퀘스트 수동 추적 우선순위 오류");
            progress[0].completed = true;
            if (QuestTrackingPolicy.Resolve(definitions, progress, side.questId) != main)
                errors.Add("완료 퀘스트 추적 복귀 오류");
            var original = new SaveData { trackedQuestId = side.questId, quests = progress };
            var restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(original));
            if (restored.trackedQuestId != side.questId) errors.Add("추적 퀘스트 저장 필드 유실");
            var legacy = JsonUtility.FromJson<SaveData>("{\"version\":13}");
            SaveManager.Migrate(legacy, out _);
            if (legacy.version != SaveData.CurrentVersion || !string.IsNullOrEmpty(legacy.trackedQuestId))
                errors.Add("v13 퀘스트 추적 기본값 마이그레이션 실패");
        }
    }
}
#endif
