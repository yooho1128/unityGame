using System.Collections.Generic;
using ShadowTheater.Data;
using UnityEngine;

namespace ShadowTheater.Battle
{
    /// <summary>
    /// 적 AI + 플레이어 자동 전투(Auto)에 공용으로 쓰는 단순 행동 선택기.
    /// 공격/스킬만 선택한다 (교체/도구는 추후 확장).
    /// </summary>
    public static class BattleAI
    {
        public static BattleAction Choose(BattleUnit self, BattleUnit opponent, int currentFp)
        {
            var basic = self.Data.basicAttack;
            var candidates = new List<(SkillData skill, float score)>();

            foreach (var s in self.Data.skills)
            {
                if (s == null || s.fpCost > currentFp) continue;
                candidates.Add((s, Score(self, opponent, s)));
            }

            if (candidates.Count == 0)
                return BattleAction.Attack(self.Side, basic);

            // 가장 점수가 높은 스킬. 단, FP를 아끼기 위해 25% 확률로 기본 공격
            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            var best = candidates[0];
            if (basic != null && Random.value < 0.25f && !best.skill.isUltimate)
                return BattleAction.Attack(self.Side, basic);

            return BattleAction.UseSkill(self.Side, best.skill);
        }

        private static float Score(BattleUnit self, BattleUnit opponent, SkillData s)
        {
            if (s.target == SkillTarget.Self)
            {
                // HP 40% 이하일 때만 회복 스킬 가치 있음
                return s.healRatio > 0f && self.HpRatio < 0.4f ? 100f : 0f;
            }

            ShadowElement elem = s.element != ShadowElement.None ? s.element : self.Data.element;
            float score = s.damageMultiplier * s.accuracy * ElementChart.GetMultiplier(elem, opponent.Data.element) * 10f;

            if (s.statusEffect != StatusEffectType.None && opponent.MajorStatus == null)
                score += s.statusChance * 5f;

            return score;
        }
    }
}
