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

        private static string WithTurns(StatusState status)
        {
            string text = $"{Label(status.type)} {System.Math.Max(0, status.remainingTurns)}T";
            // 화상은 지속 피해 외에 공격력도 낮추므로 감소율을 함께 표시한다.
            if (status.type == StatusEffectType.Burn)
            {
                int percent = UnityEngine.Mathf.RoundToInt((1f - BattleUnit.BurnAttackFactor) * 100f);
                text += " " + L10n.Format("status.burn_detail", "(공격 -{0}%)", percent);
            }
            return text;
        }
    }
}
