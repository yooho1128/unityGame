using ShadowTheater.Save;

namespace ShadowTheater.Story
{
    public readonly struct CollectionReward
    {
        public readonly int milestone;
        public readonly int gold;
        public readonly int inkCount;

        public CollectionReward(int milestone, int gold, int inkCount)
        {
            this.milestone = milestone;
            this.gold = gold;
            this.inkCount = inkCount;
        }
    }

    /// <summary>각본집의 서로 다른 그림자 기록 수를 장기 수집 보상으로 연결한다.</summary>
    public static class CollectionRewardService
    {
        public const string InkItemId = "ink";
        private static readonly int[] Milestones = { 5, 15, 30, 60, 100, 150, 180 };
        private static readonly int[] GoldRewards = { 50, 100, 200, 400, 700, 1100, 1800 };

        public static bool TryClaim(int recordedCount, int totalCount, out CollectionReward reward)
        {
            reward = default;
            if (SaveManager.Current == null || recordedCount <= 0 || totalCount <= 0) return false;

            int gold = 0;
            int inks = 0;
            int milestone = 0;
            for (int i = 0; i < Milestones.Length; i++)
            {
                int target = Milestones[i];
                if (target > totalCount || recordedCount < target || SaveManager.HasFlag(Flag(target))) continue;
                SaveManager.SetFlag(Flag(target));
                gold += GoldRewards[i];
                inks++;
                milestone = target;
            }
            if (totalCount >= 150 && totalCount < Milestones[Milestones.Length - 1] &&
                recordedCount >= totalCount && !SaveManager.HasFlag(CompletionFlag(totalCount)))
            {
                SaveManager.SetFlag(CompletionFlag(totalCount));
                gold += totalCount * 5;
                inks += 2;
                milestone = totalCount;
            }
            if (milestone == 0) return false;

            SaveManager.AddGold(gold);
            SaveManager.AddItem(InkItemId, inks);
            reward = new CollectionReward(milestone, gold, inks);
            return true;
        }

        public static int NextMilestone(int recordedCount, int totalCount)
        {
            foreach (int milestone in Milestones)
                if (milestone <= totalCount && recordedCount < milestone) return milestone;
            if (totalCount >= 150 && totalCount < Milestones[Milestones.Length - 1] && recordedCount < totalCount)
                return totalCount;
            return 0;
        }

        public static int GoldFor(int milestone)
        {
            int index = System.Array.IndexOf(Milestones, milestone);
            return index >= 0 ? GoldRewards[index] : 0;
        }

        private static string Flag(int milestone) => "collection_reward_milestone_" + milestone;
        private static string CompletionFlag(int total) => "collection_reward_complete_" + total;
    }
}
