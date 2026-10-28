#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    public static class BattleDifficultyRegression
    {
        public static void Validate(List<string> errors)
        {
            var neutral = EncounterDifficultyPolicy.For(0, BattleMode.Boss);
            var intro = EncounterDifficultyPolicy.For(1, BattleMode.Boss);
            var catacombs = EncounterDifficultyPolicy.For(2, BattleMode.Boss);
            var late = EncounterDifficultyPolicy.For(8, BattleMode.Boss);
            if (neutral.damage != 1f || neutral.vulnerability != 1f ||
                intro.damage >= catacombs.damage || catacombs.damage >= late.damage ||
                intro.vulnerability <= late.vulnerability || catacombs.maxHitRatio != .5f)
                errors.Add("지역 난이도 진행 곡선 오류");
            var data = ScriptableObject.CreateInstance<ShadowData>();
            var skill = ScriptableObject.CreateInstance<SkillData>();
            var randomState = UnityEngine.Random.state;
            try
            {
                data.shadowId = "difficulty_test"; data.baseHp = 100; data.baseAtk = 1000;
                data.baseDef = 0; data.critRate = 0; data.evasion = 0;
                skill.target = SkillTarget.Enemy; skill.damageType = DamageType.Physical;
                skill.damageMultiplier = 4f; skill.accuracy = 1f;
                var player = new BattleUnit(new ShadowInstance(data, 1), BattleSide.Player);
                var enemy = new BattleUnit(new ShadowInstance(data, 1), BattleSide.Enemy);
                int before = DamageCalculator.EstimateDamage(player, enemy, skill);
                enemy.ConfigureDifficulty(intro);
                player.ConfigureDifficulty(intro);
                if (player.Difficulty.damage != 1f || player.Difficulty.vulnerability != 1f)
                    errors.Add("난이도 보정이 플레이어 기본 능력치에 적용됨");
                if (DamageCalculator.EstimateDamage(enemy, player, skill) != 40 ||
                    DamageCalculator.Calculate(enemy, player, skill).damage != 40)
                    errors.Add("초반 적 단일 타격 상한 오류");
                if (DamageCalculator.EstimateDamage(player, enemy, skill) <= before ||
                    enemy.MaxHp != 100 || data.baseAtk != 1000)
                    errors.Add("적 취약 보정 또는 수집 원본 능력치 보존 오류");
                var captured = new BattleUnit(enemy.Instance, BattleSide.Player);
                if (captured.Difficulty.damage != 1f || captured.Difficulty.vulnerability != 1f)
                    errors.Add("포획 개체에 적 전투 보정 잔존");
                if (PartyExperiencePolicy.Amount(100, true) != 100 ||
                    PartyExperiencePolicy.Amount(100, false) != 50 ||
                    PartyExperiencePolicy.Amount(0, false) != 0)
                    errors.Add("참여자 전체/대기자 절반 경험치 오류");
                player.TakeDamage(player.MaxHp);
                int ups = PartyExperiencePolicy.Apply(player.Instance, player.Instance.ExpToNext);
                if (ups != 1 || !player.IsFainted)
                    errors.Add("기절자 경험치 레벨업 시 부활");
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                UnityEngine.Object.DestroyImmediate(skill); UnityEngine.Object.DestroyImmediate(data);
            }
        }
    }
}
#endif
