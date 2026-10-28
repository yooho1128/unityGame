#if UNITY_EDITOR
using System.Collections.Generic;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    public static class HealingSkillRegression
    {
        public static void Validate(List<string> errors)
        {
            var data = ScriptableObject.CreateInstance<ShadowData>();
            var basic = ScriptableObject.CreateInstance<SkillData>();
            var first = ScriptableObject.CreateInstance<SkillData>();
            var second = ScriptableObject.CreateInstance<SkillData>();
            try
            {
                data.shadowId = "healing_limit_test"; data.baseHp = 100; data.basicAttack = basic;
                first.target = second.target = SkillTarget.Self;
                first.healRatio = second.healRatio = .35f;
                data.skills = new List<SkillData> { first, second };
                var unit = new BattleUnit(new ShadowInstance(data, 1), BattleSide.Enemy);
                if (unit.UseHealingSkill(first, 1) != 0 || unit.HealingSkills.Used != 0)
                    errors.Add("만피 회복 시 사용 횟수 차감");
                unit.TakeDamage(80);
                if (unit.UseHealingSkill(first, 1) != 35 || unit.Hp != 55 || unit.HealingSkills.Remaining != 2)
                    errors.Add("첫 회복 또는 공용 횟수 오류");
                if (unit.UseHealingSkill(second, 2) != 0 || unit.UseHealingSkill(first, 3) != 0 || unit.Hp != 55)
                    errors.Add("다른 회복 기술로 재사용 대기 우회");
                var ai = BattleAI.Choose(unit, new BattleUnit(new ShadowInstance(data, 1), BattleSide.Player),
                    5, BattleAiProfile.BossGuardian, 2);
                if (ai.skill != basic) errors.Add("AI가 대기 중 회복 선택");
                unit.TakeDamage(35);
                if (unit.UseHealingSkill(second, 4) != 35) errors.Add("두 턴 대기 이후 재사용 실패");
                unit.TakeDamage(35);
                if (unit.UseHealingSkill(first, 7) != 35 || unit.HealingSkills.Remaining != 0)
                    errors.Add("세 번째 회복 사용 실패");
                unit.TakeDamage(35);
                if (unit.UseHealingSkill(second, 10) != 0 || unit.CanUseHealingSkill(first, 99))
                    errors.Add("회복 기술 교대 사용으로 전투당 한도 우회");
                unit.RestoreAndCleanse(.35f);
                if (unit.HealingSkills.Used != 3) errors.Add("도구 회복이 스킬 제한 초기화");
                var other = new BattleUnit(new ShadowInstance(data, 1), BattleSide.Player);
                other.TakeDamage(80);
                if (other.UseHealingSkill(first, 10) != 35) errors.Add("서로 다른 개체 회복 한도 공유");
                var nextBattle = new BattleUnit(unit.Instance, BattleSide.Enemy);
                if (nextBattle.HealingSkills.Remaining != 3) errors.Add("다음 전투에 회복 한도 잔존");
            }
            finally
            {
                Object.DestroyImmediate(first); Object.DestroyImmediate(second);
                Object.DestroyImmediate(basic); Object.DestroyImmediate(data);
            }
        }
    }
}
#endif
