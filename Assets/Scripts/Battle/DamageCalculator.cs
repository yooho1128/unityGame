using ShadowTheater.Data;
using UnityEngine;

namespace ShadowTheater.Battle
{
    /// <summary>
    /// 전투 공식 모음. 밸런싱 시 이 파일만 수정하면 되도록 BattleManager와 분리.
    /// </summary>
    public static class DamageCalculator
    {
        public const float CritMultiplier = 1.5f;
        public const float MagicDefenseFactor = 0.5f;
        public const float DefenseConstant = 100f;

        public static HitResult Calculate(BattleUnit attacker, BattleUnit defender, SkillData skill)
        {
            var r = new HitResult { elementMultiplier = 1f, status = skill.statusEffect };

            // 1) 명중 / 회피
            float hitChance = skill.accuracy * (1f - defender.Evasion);
            if (Random.value > hitChance)
            {
                r.missed = true;
                return r;
            }

            // 2) 데미지
            if (skill.DealsDamage)
            {
                float def = defender.Def * (skill.damageType == DamageType.Magical ? MagicDefenseFactor : 1f);
                float defReduce = DefenseConstant / (DefenseConstant + def);   // 방어 100 → 50% 감소

                float raw = attacker.Atk * skill.damageMultiplier * defReduce;

                ShadowElement atkElem = skill.element != ShadowElement.None ? skill.element : attacker.Data.element;
                r.elementMultiplier = ElementChart.GetMultiplier(atkElem, defender.Data.element);
                raw *= r.elementMultiplier;

                r.critical = Random.value < attacker.CritRate + skill.bonusCritRate;
                if (r.critical) raw *= CritMultiplier;

                raw *= Random.Range(0.9f, 1.0f); // 난수 편차
                r.damage = Mathf.Max(1, Mathf.RoundToInt(raw));
            }

            // 3) 상태 이상 판정 (실제 적용은 BattleManager)
            r.statusApplied = skill.statusEffect != StatusEffectType.None && Random.value < skill.statusChance;
            return r;
        }

        /// <summary>
        /// 각본 기록(포획) 성공 확률. HP가 낮을수록, 상태 이상일수록 높음.
        /// HP 100% → base x 0.33, HP 1 → base x ~1.0
        /// </summary>
        public static float CaptureChance(BattleUnit target, float itemBonus = 1f)
        {
            if (!target.Data.IsCapturable) return 0f;

            float hpFactor = 1f - (2f / 3f) * target.HpRatio;
            float statusBonus = target.MajorStatus != null
                ? (target.MajorStatus.type == StatusEffectType.Freeze ? 1.5f : 1.2f)
                : 1f;

            return Mathf.Clamp01(target.Data.baseCaptureRate * hpFactor * statusBonus * itemBonus);
        }

        public static int ExpReward(BattleUnit defeated) =>
            Mathf.RoundToInt(defeated.Data.expReward * defeated.Level / 5f) + 5;

        public static int SplitExperience(int totalExp, int participantCount) =>
            participantCount <= 0 || totalExp <= 0 ? 0 : Mathf.Max(1, totalExp / participantCount);
    }
}
