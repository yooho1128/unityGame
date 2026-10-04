using System.Collections.Generic;
using ShadowTheater.Data;
using UnityEngine;

namespace ShadowTheater.Battle
{
    /// <summary>
    /// 적 AI + 플레이어 자동 전투(Auto)에 공용으로 쓰는 행동 선택기.
    /// 공격·회복·상태 이상뿐 아니라 위험 HP와 속성 상성을 보고 교체 후보도 고른다.
    /// </summary>
    public static class BattleAI
    {
        private const float EmergencyHp = .28f;
        private const float SafeBenchHp = .48f;

        public static BattleAction Choose(BattleUnit self, BattleUnit opponent, int currentFp)
        {
            var basic = self.BasicAttack;
            var candidates = new List<(SkillData skill, float score)>();

            foreach (var s in self.Skills)
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

        /// <summary>
        /// 현재 유닛보다 확실히 안전한 후보만 반환한다. -1이면 교체하지 않는다.
        /// 교체 쿨다운은 BattleManager가 관리한다.
        /// </summary>
        public static int ChooseSwitchIndex(IReadOnlyList<BattleUnit> party, int activeIndex,
                                            BattleUnit opponent)
        {
            if (party == null || opponent == null || activeIndex < 0 || activeIndex >= party.Count || party.Count < 2)
                return -1;

            BattleUnit current = party[activeIndex];
            float currentMatchup = Matchup(current, opponent);
            int bestIndex = -1;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < party.Count; i++)
            {
                BattleUnit candidate = party[i];
                if (i == activeIndex || candidate == null || candidate.IsFainted || candidate.HpRatio < .2f) continue;

                float matchup = Matchup(candidate, opponent);
                float score = matchup * 2f + candidate.HpRatio;
                if (score <= bestScore) continue;
                bestScore = score;
                bestIndex = i;
            }

            if (bestIndex < 0) return -1;
            BattleUnit best = party[bestIndex];
            float bestMatchup = Matchup(best, opponent);

            bool emergency = current.HpRatio <= EmergencyHp && best.HpRatio >= SafeBenchHp &&
                             best.HpRatio >= current.HpRatio + .25f && bestMatchup >= currentMatchup - .25f;
            bool counterPick = currentMatchup < 0f && best.HpRatio >= .35f &&
                               bestMatchup >= currentMatchup + .75f;
            return emergency || counterPick ? bestIndex : -1;
        }

        /// <summary>양수면 유리, 음수면 불리. 공격 상성과 받는 상성을 함께 평가한다.</summary>
        private static float Matchup(BattleUnit self, BattleUnit opponent)
        {
            float outgoing = ElementChart.GetMultiplier(self.Data.element, opponent.Data.element);
            float incoming = ElementChart.GetMultiplier(opponent.Data.element, self.Data.element);
            return outgoing - incoming;
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
