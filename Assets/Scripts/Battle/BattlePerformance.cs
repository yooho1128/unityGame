using System;
using UnityEngine;

namespace ShadowTheater.Battle
{
    public readonly struct BattlePerformanceResult
    {
        public readonly string rank;
        public readonly int score;
        public readonly int bonusGold;

        public BattlePerformanceResult(string rank, int score, int bonusGold)
        {
            this.rank = rank;
            this.score = score;
            this.bonusGold = bonusGold;
        }
    }

    /// <summary>전투의 속도와 열연 성과를 공연 등급·추가 금화로 환산한다.</summary>
    public static class BattlePerformance
    {
        public static BattlePerformanceResult Evaluate(int turns, int enemyCount, int encoreCount,
            int encoreFinishes, int playerFaints, int baseGold)
        {
            long expectedTurns = Math.Max(1L, enemyCount) * 4L;
            long tempoBonus = Math.Max(0L, expectedTurns - Math.Max(1L, turns)) * 5L;
            long rawScore = 70L + tempoBonus + Math.Max(0L, encoreCount) * 15L +
                Math.Max(0L, encoreFinishes) * 25L - Math.Max(0L, playerFaints) * 20L;
            int score = (int)Math.Min(999L, Math.Max(0L, rawScore));

            string rank;
            int bonusPercent;
            if (score >= 140) { rank = "S"; bonusPercent = 25; }
            else if (score >= 110) { rank = "A"; bonusPercent = 15; }
            else if (score >= 80) { rank = "B"; bonusPercent = 5; }
            else { rank = "C"; bonusPercent = 0; }

            int safeBaseGold = Mathf.Max(0, baseGold);
            long roundedBonus = ((long)safeBaseGold * bonusPercent + 50L) / 100L;
            int bonusGold = (int)Math.Min(Math.Max(0L, roundedBonus), (long)int.MaxValue - safeBaseGold);
            return new BattlePerformanceResult(rank, score, bonusGold);
        }
    }
}
