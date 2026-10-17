using System.Collections.Generic;
using ShadowTheater.Data;
using ShadowTheater.Story;
using UnityEngine;

namespace ShadowTheater.Field
{
    public static class IntroEncounterPolicy
    {
        public static bool IsIntro(RegionData region) => region != null && region.act == 1 &&
            !region.isBossStage && !region.isDungeon && region.recommendedLevelMin <= 5;

        public static int ClampLevel(RegionData region, int level, IReadOnlyList<ShadowInstance> party)
        {
            if (!IsIntro(region)) return level;
            int total = 0, count = 0;
            if (party != null) foreach (var member in party)
                if (member != null && !member.IsFainted) { total += member.level; count++; }
            int average = count > 0 ? Mathf.RoundToInt(total / (float)count) : 5;
            int ceiling = count < 2 ? Mathf.Clamp(average - 1, 2, 4) : Mathf.Clamp(average + 1, 3, 6);
            return Mathf.Clamp(level, 2, ceiling);
        }

        public static bool IsAvailable(RegionData region, ShadowData shadow, IReadOnlyList<ShadowInstance> party)
        {
            if (!IsIntro(region) || shadow == null || shadow.growthTier != GrowthTier.Rare) return true;
            int available = 0;
            if (party != null) foreach (var member in party) if (member != null && !member.IsFainted) available++;
            return available >= 2;
        }
    }
}
