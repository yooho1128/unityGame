using System;
using ShadowTheater.Data;
using UnityEngine;

namespace ShadowTheater.Battle
{
    /// <summary>
    /// 연속 흥행: 도주·패배 없이 공연(승리·기록)을 이어 갈수록 승리 금화가 늘고 주연급 그림자가 자주 나타난다.
    /// 수치는 이 파일에서만 조정한다.
    /// </summary>
    public static class PerformanceStreakPolicy
    {
        public const int PercentPerStep = 3;
        public const int MaxBonusSteps = 10; // 최대 +30%

        public static int Next(int streak, BattleResult result)
        {
            switch (result)
            {
                case BattleResult.Victory:
                case BattleResult.Captured:
                    return streak >= int.MaxValue ? int.MaxValue : Math.Max(0, streak) + 1;
                case BattleResult.Defeat:
                case BattleResult.Escaped:
                    return 0;
                default:
                    return Math.Max(0, streak);
            }
        }

        public static int BonusPercent(int streak) => (Mathf.Clamp(streak, 1, MaxBonusSteps + 1) - 1) * PercentPerStep;

        public static int BonusGold(int baseGold, int streak)
        {
            long bonus = ((long)Math.Max(0, baseGold) * BonusPercent(streak) + 50L) / 100L;
            return (int)Math.Min(bonus, int.MaxValue);
        }

        public static bool IsMilestone(int streak) =>
            streak == 5 || streak == 10 || streak == 25 || (streak > 0 && streak % 50 == 0);

        public static string Title(int streak)
        {
            if (streak >= 25) return "전설의 장기 공연";
            if (streak >= 10) return "매진 행렬";
            if (streak >= 5) return "입소문";
            if (streak >= 3) return "흥행 조짐";
            return string.Empty;
        }
    }

    /// <summary>
    /// 주연급 변주 그림자: 야생 조우에서 드물게 등장하는 강화 개체.
    /// 능력치 배율은 저장 데이터와 함께 ShadowInstance가 적용하고, 여기서는 출현·보상 규칙만 정한다.
    /// </summary>
    public static class StarVariantPolicy
    {
        public const float BaseChance = 1f / 64f;
        public const float StreakChanceStep = .1f; // 연속 흥행 1회당 +10%, 최대 2배
        public const float ExperienceMultiplier = 1.5f;
        public const int GoldMultiplier = 2;
        public const string Mark = "★";

        public static float Chance(int streak) =>
            BaseChance * (1f + Mathf.Clamp(streak, 0, PerformanceStreakPolicy.MaxBonusSteps) * StreakChanceStep);

        public static bool Roll(float roll, int streak) => roll < Chance(streak);

        public static int Experience(int reward, bool star)
        {
            int normalized = Math.Max(0, reward);
            return star ? (int)Math.Min(Math.Round((double)normalized * ExperienceMultiplier), int.MaxValue) : normalized;
        }

        public static int Gold(int gold, bool star) =>
            (int)Math.Min((long)Math.Max(0, gold) * (star ? GoldMultiplier : 1), int.MaxValue);

        public static string Decorate(string name, ShadowInstance instance) =>
            instance != null && instance.starVariant ? $"{Mark} {name}" : name;
    }
}
