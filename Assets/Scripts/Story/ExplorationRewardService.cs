using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Save;

namespace ShadowTheater.Story
{
    public readonly struct ExplorationReward
    {
        public readonly bool regionDiscovered;
        public readonly int gold;
        public readonly int bondExperience;
        public readonly int potionCount;
        public readonly int milestone;
        public readonly int visitedCount;
        public readonly int totalRegions;

        public ExplorationReward(bool regionDiscovered, int gold, int bondExperience, int potionCount,
            int milestone, int visitedCount, int totalRegions)
        {
            this.regionDiscovered = regionDiscovered;
            this.gold = gold;
            this.bondExperience = bondExperience;
            this.potionCount = potionCount;
            this.milestone = milestone;
            this.visitedCount = visitedCount;
            this.totalRegions = totalRegions;
        }
    }

    /// <summary>첫 지역 발견과 누적 탐험 이정표를 실제 성장 보상으로 연결한다.</summary>
    public static class ExplorationRewardService
    {
        public const string PotionItemId = "minor_potion";
        private static readonly int[] Milestones = { 5, 10, 20, 35, 50, 63 };
        private static readonly int[] MilestoneGold = { 50, 100, 200, 350, 500, 1000 };

        public static bool TryClaimArrival(RegionData region, out ExplorationReward reward)
        {
            reward = default;
            SaveData save = SaveManager.Current;
            if (save == null || region == null || string.IsNullOrEmpty(region.regionId) ||
                save.visitedRegionIds?.Contains(region.regionId) != true) return false;

            bool regionDiscovered = !SaveManager.HasFlag(RegionFlag(region.regionId));
            int gold = 0;
            int bondExperience = 0;
            int potions = 0;
            if (regionDiscovered)
            {
                SaveManager.SetFlag(RegionFlag(region.regionId));
                gold += RegionGold(region);
                bondExperience = region.isHidden || region.isLegendary ? 8 : 3;
                if (region.isHidden) potions++;
            }

            int visited = CountVisited(save.visitedRegionIds);
            int milestone = 0;
            for (int i = 0; i < Milestones.Length; i++)
            {
                if (visited < Milestones[i] || SaveManager.HasFlag(MilestoneFlag(Milestones[i]))) continue;
                SaveManager.SetFlag(MilestoneFlag(Milestones[i]));
                gold += MilestoneGold[i];
                potions++;
                milestone = Milestones[i];
            }

            if (!regionDiscovered && milestone == 0) return false;
            SaveManager.AddGold(gold);
            if (potions > 0) SaveManager.AddItem(PotionItemId, potions);
            ShadowTheater.Data.ShadowInstance companion = CompanionBondService.CurrentCompanion();
            if (companion != null && bondExperience > 0)
                CompanionBondService.AddExperience(companion.instanceId, bondExperience, out _, out _);

            reward = new ExplorationReward(regionDiscovered, gold, bondExperience, potions, milestone,
                visited, RegionRepository.All.Count);
            return true;
        }

        public static int RegionGold(RegionData region)
        {
            if (region == null) return 0;
            int value = 10 + System.Math.Max(1, region.act) * 5;
            if (region.isDungeon) value += 5;
            if (region.isHidden) value += 15;
            if (region.isLegendary) value += 20;
            return value;
        }

        public static int NextMilestone(int visitedCount)
        {
            foreach (int milestone in Milestones)
                if (visitedCount < milestone) return milestone;
            return 0;
        }

        public static int MilestoneGoldFor(int milestone)
        {
            int index = System.Array.IndexOf(Milestones, milestone);
            return index >= 0 ? MilestoneGold[index] : 0;
        }

        private static int CountVisited(IEnumerable<string> visitedIds)
        {
            var valid = new HashSet<string>(RegionRepository.All.Where(x => x != null).Select(x => x.regionId));
            return visitedIds == null ? 0 : new HashSet<string>(visitedIds.Where(valid.Contains)).Count;
        }

        private static string RegionFlag(string regionId) => "exploration_reward_region_" + regionId;
        private static string MilestoneFlag(int count) => "exploration_reward_milestone_" + count;
    }
}
