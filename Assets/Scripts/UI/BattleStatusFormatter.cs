using System.Collections.Generic;
using ShadowTheater.Battle;
using ShadowTheater.Data;

namespace ShadowTheater.UI
{
    public static class BattleStatusFormatter
    {
        public static string Label(StatusEffectType status) =>
            L10n.Get("status." + status.ToString().ToLowerInvariant(), status.ToString());

        public static string Summary(BattleUnit unit)
        {
            if (unit == null || !unit.HasAnyStatus) return string.Empty;
            var labels = new List<string>();
            if (unit.MajorStatus != null) labels.Add(WithTurns(unit.MajorStatus));
            foreach (StatusState status in unit.Debuffs)
                if (status != null) labels.Add(WithTurns(status));
            return string.Join(" · ", labels);
        }

        private static string WithTurns(StatusState status) =>
            $"{Label(status.type)} {System.Math.Max(0, status.remainingTurns)}T";
    }
}
