// Executes the production policy files with a minimal managed compatibility surface.
// This does not execute Unity, the BattleManager coroutine or native serialization.
using System;
using System.Collections.Generic;
using ShadowTheater.Battle;

static class Program
{
    static int count;
    static void Check(bool condition, string label)
    { count++; if (!condition) throw new Exception(label); }
    static void Main()
    {
        var context = new AudienceRequestContext { mode = BattleMode.Wild, enemyCount = 1,
            canRecord = true, hasWeaknessOption = true, hasStatusOption = true, hasRepertoireOption = true };
        var selected = new HashSet<AudienceRequestType>();
        for (int i = 0; i < 400; i++) selected.Add(AudienceRequest.Choose(context, i / 400f));
        Check(selected.Count == 8 && !selected.Contains(AudienceRequestType.None), "eight attainable request types");
        var limited = new AudienceRequestContext { mode = BattleMode.Rival, enemyCount = 2 };
        selected.Clear();
        for (int i = 0; i < 400; i++) selected.Add(AudienceRequest.Choose(limited, i / 400f));
        Check(!selected.Contains(AudienceRequestType.RecordScene), "rival cannot record");
        Check(!selected.Contains(AudienceRequestType.WeaknessFinale), "no unattainable weakness");
        Check(!selected.Contains(AudienceRequestType.TensionScene), "no unattainable status");
        Check(!selected.Contains(AudienceRequestType.Repertoire), "no unattainable repertoire");
        foreach (BattleResult result in Enum.GetValues(typeof(BattleResult)))
        {
            var request = new AudienceRequest(); request.Begin(AudienceRequestType.RecordScene, context);
            Check(request.Conclude(result) == (result == BattleResult.Captured), "record result " + result);
            request.Begin(AudienceRequestType.CriticalLine, context);
            Check(request.NotifyPlayerHit(new HitResult { damage = 10, critical = true }, false), "critical achieves");
            Check(request.Conclude(result) == (result == BattleResult.Victory || result == BattleResult.Captured), "no loss rewards " + result);
        }
        var r = new AudienceRequest();
        r.Begin(AudienceRequestType.NoUnderstudy, context);
        Check(r.NotifyPlayerFaint(), "knockout before simultaneous victory");
        Check(!r.Conclude(BattleResult.Victory), "mutual knockout not flawless");
        r.Begin(AudienceRequestType.SwiftCurtain, context);
        Check(r.TurnLimit == 3 && !r.NotifyTurnEnded(2), "swift within limit");
        Check(r.Conclude(BattleResult.Victory), "last allowed turn victory");
        r.Begin(AudienceRequestType.SwiftCurtain, context);
        Check(r.NotifyTurnEnded(3) && !r.Conclude(BattleResult.Victory), "swift expires");
        r.Begin(AudienceRequestType.Repertoire, context);
        Check(!r.NotifyPlayerSkill("one") && !r.NotifyPlayerSkill("one"), "deduplicate skills");
        Check(r.NotifyPlayerSkill(AudienceRequest.JointSkillKey), "joint is second skill");
        Check(!r.NotifyPlayerSkill("third"), "only one mid-battle reward");
        r.Begin(AudienceRequestType.WeaknessFinale, context);
        var hit = new HitResult { damage = 10, elementMultiplier = 1.5f };
        Check(!r.NotifyPlayerHit(hit, false) && r.NotifyPlayerHit(hit, true), "weakness knockout");
        Check(AudienceRequest.RewardGold(0, BattleMode.Wild) == 15, "minimum gold");
        Check(AudienceRequest.RewardGold(61, BattleMode.Boss) == 62, "boss gold");
        Check(AudienceRequest.RewardGold(int.MaxValue, BattleMode.Boss) == int.MaxValue, "gold saturation");
        Check(PerformanceStreakPolicy.Next(4, BattleResult.Victory) == 5, "victory streak");
        Check(PerformanceStreakPolicy.Next(4, BattleResult.Captured) == 5, "capture streak");
        Check(PerformanceStreakPolicy.Next(4, BattleResult.Escaped) == 0, "escape resets");
        Check(PerformanceStreakPolicy.Next(4, BattleResult.Defeat) == 0, "defeat resets");
        Check(PerformanceStreakPolicy.Next(int.MaxValue, BattleResult.Victory) == int.MaxValue, "streak saturation");
        Check(PerformanceStreakPolicy.BonusPercent(int.MinValue) == 0, "corrupt negative streak");
        Check(PerformanceStreakPolicy.BonusPercent(1) == 0, "first victory");
        Check(PerformanceStreakPolicy.BonusPercent(11) == 30 && PerformanceStreakPolicy.BonusPercent(int.MaxValue) == 30, "bonus cap");
        Check(PerformanceStreakPolicy.BonusGold(100, 2) == 3, "bonus gold");
        Check(StarVariantPolicy.Chance(0) == 1f / 64f && StarVariantPolicy.Chance(int.MaxValue) == 1f / 32f, "star chance cap");
        Check(StarVariantPolicy.Experience(100, true) == 150, "star experience");
        Check(StarVariantPolicy.Experience(100, false) == 100, "ordinary experience unchanged");
        Check(StarVariantPolicy.Experience(3, true) == 4, "round midpoint to even");
        Check(StarVariantPolicy.Experience(int.MaxValue, true) == int.MaxValue, "experience saturation");
        Check(StarVariantPolicy.Experience(-1, true) == 0, "negative experience");
        Check(StarVariantPolicy.Gold(int.MaxValue, true) == int.MaxValue, "star gold saturation");
        Console.WriteLine("PASS: " + count + " checks against production audience/streak/star policy files (.NET, not Unity)");
    }
}
namespace UnityEngine
{
    public static class Mathf
    {
        public static int Clamp(int v, int min, int max) => Math.Min(Math.Max(v, min), max);
        public static float Clamp01(float v) => Math.Min(Math.Max(v, 0), 1);
    }
}
namespace ShadowTheater.Data { public class ShadowInstance { public bool starVariant; } }
namespace ShadowTheater.Battle
{
    public enum BattleMode { Wild, Rival, Boss }
    public enum BattleResult { None, Victory, Defeat, Captured, Escaped }
    public struct HitResult { public bool missed, critical; public int damage; public float elementMultiplier; public bool IsEffective => elementMultiplier > 1f; }
}
