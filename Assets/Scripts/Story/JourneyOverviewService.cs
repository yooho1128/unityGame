using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Data;
using ShadowTheater.Save;

namespace ShadowTheater.Story
{
    public enum JourneyGoalKind { None, Quest, Region, Collection, Ending }

    public readonly struct JourneyOverview
    {
        public readonly int percent, rankIndex;
        public readonly int visitedRegions, totalRegions;
        public readonly int recordedShadows, totalShadows;
        public readonly int completedQuests, totalQuests;
        public readonly int unlockedEndings, totalEndings;
        public readonly JourneyGoalKind goalKind;
        public readonly string goalTitle, goalDetail;
        public readonly int goalCurrent, goalTarget;

        public JourneyOverview(int percent, int rankIndex, int visitedRegions, int totalRegions,
            int recordedShadows, int totalShadows, int completedQuests, int totalQuests,
            int unlockedEndings, int totalEndings, JourneyGoalKind goalKind, string goalTitle,
            string goalDetail, int goalCurrent, int goalTarget)
        {
            this.percent = percent; this.rankIndex = rankIndex;
            this.visitedRegions = visitedRegions; this.totalRegions = totalRegions;
            this.recordedShadows = recordedShadows; this.totalShadows = totalShadows;
            this.completedQuests = completedQuests; this.totalQuests = totalQuests;
            this.unlockedEndings = unlockedEndings; this.totalEndings = totalEndings;
            this.goalKind = goalKind; this.goalTitle = goalTitle; this.goalDetail = goalDetail;
            this.goalCurrent = goalCurrent; this.goalTarget = goalTarget;
        }
    }

    /// <summary>흩어진 지역·도감·퀘스트·엔딩 진행을 하나의 여정 브리핑으로 요약한다.</summary>
    public static class JourneyOverviewService
    {
        public static JourneyOverview Build()
        {
            SaveData save = SaveManager.Current;
            if (save == null) return default;

            var regions = RegionRepository.All.Where(x => x != null && !string.IsNullOrEmpty(x.regionId)).ToList();
            var regionIds = new HashSet<string>(regions.Select(x => x.regionId));
            int visited = UniqueValidCount(save.visitedRegionIds, regionIds);

            var shadowIds = new HashSet<string>((ShadowDatabase.Instance?.shadows ?? new List<ShadowData>())
                .Where(x => x != null && !string.IsNullOrEmpty(x.shadowId)).Select(x => x.shadowId));
            int recorded = UniqueValidCount(save.recordedShadowIds, shadowIds);

            var quests = QuestRepository.All.Where(x => x != null && !string.IsNullOrEmpty(x.questId)).ToList();
            var questIds = new HashSet<string>(quests.Select(x => x.questId));
            int completed = save.quests == null ? 0 : save.quests
                .Where(x => x != null && x.completed && questIds.Contains(x.questId))
                .Select(x => x.questId).Distinct().Count();

            var endingIds = new HashSet<string>(EndingRepository.All
                .Where(x => x != null && !string.IsNullOrEmpty(x.endingId)).Select(x => x.endingId));
            int endings = UniqueValidCount(save.unlockedEndingIds, endingIds);

            int percent = Weighted(visited, regions.Count, 35) + Weighted(recorded, shadowIds.Count, 35) +
                Weighted(completed, quests.Count, 20) + Weighted(endings, endingIds.Count, 10);
            percent = System.Math.Min(100, System.Math.Max(0, percent));

            BuildGoal(save, recorded, shadowIds.Count, out JourneyGoalKind kind, out string title,
                out string detail, out int current, out int target);
            return new JourneyOverview(percent, RankIndex(percent), visited, regions.Count, recorded,
                shadowIds.Count, completed, quests.Count, endings, endingIds.Count, kind, title, detail,
                current, target);
        }

        public static int RankIndex(int percent)
        {
            if (percent >= 100) return 5;
            if (percent >= 80) return 4;
            if (percent >= 60) return 3;
            if (percent >= 40) return 2;
            if (percent >= 20) return 1;
            return 0;
        }

        public static int Weighted(int completed, int total, int weight)
        {
            if (total <= 0 || weight <= 0) return 0;
            long safe = System.Math.Min(System.Math.Max(0L, completed), total);
            return (int)((safe * weight + total / 2L) / total);
        }

        private static void BuildGoal(SaveData save, int recorded, int totalShadows,
            out JourneyGoalKind kind, out string title, out string detail, out int current, out int target)
        {
            kind = JourneyGoalKind.None; title = detail = string.Empty; current = target = 0;
            QuestProgressData progress = ActiveQuest(save, false) ?? ActiveQuest(save, true);
            if (progress != null && QuestRepository.TryGet(progress.questId, out QuestDefinition quest))
            {
                QuestObjectiveDefinition objective = (quest.objectives ?? new List<QuestObjectiveDefinition>())
                    .FirstOrDefault(x =>
                    {
                        if (x == null) return false;
                        QuestObjectiveProgressData p = progress.objectives?.Find(y => y != null && y.objectiveId == x.objectiveId);
                        return (p?.current ?? 0) < System.Math.Max(1, x.requiredCount);
                    });
                kind = JourneyGoalKind.Quest;
                title = quest.title;
                detail = objective?.description ?? quest.summary;
                QuestObjectiveProgressData objectiveProgress = objective == null ? null :
                    progress.objectives?.Find(x => x != null && x.objectiveId == objective.objectiveId);
                current = objectiveProgress?.current ?? 0;
                target = objective == null ? 1 : System.Math.Max(1, objective.requiredCount);
                return;
            }

            RegionData region = RegionRepository.Ordered().FirstOrDefault(x => x != null &&
                RegionProgress.GetState(x) == RegionMapState.Unlocked);
            if (region != null)
            {
                kind = JourneyGoalKind.Region; title = region.displayName; detail = region.environment;
                current = 0; target = 1; return;
            }

            int milestone = CollectionRewardService.NextMilestone(recorded, totalShadows);
            if (milestone > 0)
            {
                kind = JourneyGoalKind.Collection; current = recorded; target = milestone; return;
            }
            if ((save.unlockedEndingIds?.Count ?? 0) < EndingRepository.All.Count)
                kind = JourneyGoalKind.Ending;
        }

        private static QuestProgressData ActiveQuest(SaveData save, bool includeSideQuest) =>
            save.quests?.FirstOrDefault(x => x != null && !x.completed &&
                QuestRepository.TryGet(x.questId, out QuestDefinition quest) &&
                (includeSideQuest || !quest.isSideQuest));

        private static int UniqueValidCount(IEnumerable<string> values, HashSet<string> valid) => values == null
            ? 0 : new HashSet<string>(values.Where(x => !string.IsNullOrEmpty(x) && valid.Contains(x))).Count;
    }
}
