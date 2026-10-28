#if UNITY_EDITOR
using System.Collections.Generic;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    /// <summary>관객 주문·연속 흥행·주연급 변주 그림자 규칙 회귀 검사.</summary>
    public static class ShowcaseRegression
    {
        public static void Validate(List<string> errors)
        {
            ValidateAudienceRequests(errors);
            ValidateStreak(errors);
            ValidateStarVariant(errors);
        }

        private static AudienceRequestContext Context(BattleMode mode) => new AudienceRequestContext
        {
            mode = mode, enemyCount = 1, canRecord = mode == BattleMode.Wild,
            hasWeaknessOption = true, hasStatusOption = true, hasRepertoireOption = true
        };

        private static void ValidateAudienceRequests(List<string> errors)
        {
            var reached = new HashSet<AudienceRequestType>();
            for (int i = 0; i < 400; i++) reached.Add(AudienceRequest.Choose(Context(BattleMode.Wild), i / 400f));
            if (reached.Count != 8 || reached.Contains(AudienceRequestType.None))
                errors.Add($"관객 주문 선택 범위 오류: {reached.Count}종");
            var rival = new AudienceRequestContext { mode = BattleMode.Rival, enemyCount = 2 };
            for (int i = 0; i < 400; i++)
            {
                AudienceRequestType type = AudienceRequest.Choose(rival, i / 400f);
                if (type == AudienceRequestType.RecordScene || type == AudienceRequestType.WeaknessFinale ||
                    type == AudienceRequestType.TensionScene || type == AudienceRequestType.Repertoire)
                { errors.Add("조건이 없는 관객 주문이 선택됨: " + type); break; }
            }

            var request = new AudienceRequest();
            request.Begin(AudienceRequestType.SwiftCurtain, Context(BattleMode.Wild));
            if (request.TurnLimit != 3 || request.NotifyTurnEnded(2) || !request.NotifyTurnEnded(3) ||
                request.Conclude(BattleResult.Victory))
                errors.Add("빠른 막내림 제한 턴 판정 오류");
            request.Begin(AudienceRequestType.NoUnderstudy, Context(BattleMode.Boss));
            if (!request.NotifyPlayerFaint() || request.Conclude(BattleResult.Victory))
                errors.Add("대역 없는 무대 기절 실패 판정 오류");
            request.Begin(AudienceRequestType.WeaknessFinale, Context(BattleMode.Wild));
            var effective = new HitResult { damage = 10, elementMultiplier = 2f };
            if (request.NotifyPlayerHit(effective, false) || !request.NotifyPlayerHit(effective, true) ||
                !request.Conclude(BattleResult.Victory))
                errors.Add("약점 피날레 판정 오류");
            request.Begin(AudienceRequestType.CriticalLine, Context(BattleMode.Wild));
            if (!request.NotifyPlayerHit(new HitResult { damage = 5, critical = true, elementMultiplier = 1f }, false) ||
                request.Conclude(BattleResult.Defeat))
                errors.Add("패배한 공연에 관객 주문 보상 지급");
            request.Begin(AudienceRequestType.Repertoire, Context(BattleMode.Wild));
            if (request.NotifyPlayerSkill("same") || request.NotifyPlayerSkill("same") ||
                !request.NotifyPlayerSkill(AudienceRequest.JointSkillKey))
                errors.Add("레퍼토리 주문이 같은 기술을 중복 집계");
            request.Begin(AudienceRequestType.RecordScene, Context(BattleMode.Wild));
            if (request.Conclude(BattleResult.Victory)) errors.Add("기록의 순간이 일반 승리로 달성됨");
            request.Begin(AudienceRequestType.RecordScene, Context(BattleMode.Wild));
            if (!request.Conclude(BattleResult.Captured)) errors.Add("기록의 순간이 기록 성공으로 달성되지 않음");
            if (AudienceRequest.RewardGold(0, BattleMode.Wild) != 15 || AudienceRequest.RewardGold(61, BattleMode.Boss) != 62 ||
                AudienceRequest.RewardGold(int.MaxValue, BattleMode.Boss) <= 0)
                errors.Add("관객 주문 금화 보상 계산 오류");
        }

        private static void ValidateStreak(List<string> errors)
        {
            if (PerformanceStreakPolicy.Next(4, BattleResult.Victory) != 5 ||
                PerformanceStreakPolicy.Next(4, BattleResult.Captured) != 5 ||
                PerformanceStreakPolicy.Next(4, BattleResult.Escaped) != 0 ||
                PerformanceStreakPolicy.Next(4, BattleResult.Defeat) != 0 ||
                PerformanceStreakPolicy.Next(int.MaxValue, BattleResult.Victory) != int.MaxValue)
                errors.Add("연속 흥행 전이 오류");
            if (PerformanceStreakPolicy.BonusPercent(1) != 0 || PerformanceStreakPolicy.BonusPercent(11) != 30 ||
                PerformanceStreakPolicy.BonusPercent(999) != 30 || PerformanceStreakPolicy.BonusGold(int.MaxValue, 11) <= 0)
                errors.Add("연속 흥행 보너스 상한 오류");
            if (StarVariantPolicy.Chance(999) > StarVariantPolicy.BaseChance * 2f + .0001f)
                errors.Add("주연급 출현률이 연속 흥행으로 두 배를 초과");
        }

        private static void ValidateStarVariant(List<string> errors)
        {
            var data = ScriptableObject.CreateInstance<ShadowData>();
            try
            {
                data.shadowId = "star_variant_test"; data.baseHp = 100; data.baseAtk = 50; data.baseDef = 30; data.baseSpd = 20;
                var normal = new ShadowInstance(data, 1);
                var star = new ShadowInstance(data, 1) { starVariant = true };
                star.FullHeal();
                if (star.MaxHp != 110 || star.Atk != 55 || star.Def != 33 || star.Spd != 22 || star.currentHp != star.MaxHp ||
                    normal.MaxHp != 100)
                    errors.Add("주연급 변주 능력치 배율 오류");
                var restored = JsonUtility.FromJson<ShadowInstance>(JsonUtility.ToJson(star));
                if (restored == null || !restored.starVariant) errors.Add("주연급 변주 플래그가 저장되지 않음");
                var legacy = JsonUtility.FromJson<ShadowInstance>("{\"shadowId\":\"star_variant_test\",\"level\":1}");
                if (legacy == null || legacy.starVariant) errors.Add("구버전 저장 개체가 주연급으로 해석됨");
                if (StarVariantPolicy.Decorate("가면", star) != "★ 가면" || StarVariantPolicy.Decorate("가면", normal) != "가면")
                    errors.Add("주연급 이름 표시 오류");
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }
    }
}
#endif
