using ShadowTheater.Data;
using ShadowTheater.Story;

namespace ShadowTheater.Field
{
    /// <summary>지역 종류와 그림자 등급을 랜덤 조우 수치로 변환하는 공용 정책.</summary>
    public static class RegionalEncounterPolicy
    {
        public static void GetCadence(RegionData region, out int safeMin, out int safeMax,
            out float chancePerStep, out int pitySteps)
        {
            if (IntroEncounterPolicy.IsIntro(region))
            {
                safeMin = 14; safeMax = 20; chancePerStep = .08f; pitySteps = 24;
            }
            else if (region != null && region.isHidden)
            {
                safeMin = 9; safeMax = 13; chancePerStep = .09f; pitySteps = 22;
            }
            else if (region != null && region.isDungeon)
            {
                safeMin = 5; safeMax = 8; chancePerStep = .16f; pitySteps = 14;
            }
            else
            {
                safeMin = 7; safeMax = 11; chancePerStep = .12f; pitySteps = 18;
            }
        }

        public static float FeaturedWeight(int rank)
        {
            switch (rank)
            {
                case 0: return 5f;
                case 1: return 3.5f;
                case 2: return 2.25f;
                default: return 1.25f;
            }
        }

        public static float RarityMultiplier(GrowthTier tier)
        {
            switch (tier)
            {
                case GrowthTier.Standard: return 1f;
                case GrowthTier.Rare: return .55f;
                default: return 0f;
            }
        }

        public static bool IsEligible(RegionData region, ShadowData shadow)
        {
            return region != null && shadow != null && shadow.IsCapturable &&
                   shadow.growthTier < GrowthTier.RegionalBoss &&
                   (string.IsNullOrEmpty(region.bossShadowName) || shadow.displayName != region.bossShadowName);
        }
    }
}
