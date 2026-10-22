using System.Collections.Generic;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>중복 기록, 숙련도, 대기 파티 따라잡기와 성장 방향을 하나의 장기 성장 루프로 연결한다.</summary>
    public static class CollectionGrowthService
    {
        public const int FocusUnlockMastery = 20;
        public const int FocusChangeCost = 8;

        public static int DuplicateFragments(ShadowData shadow)
        {
            if (shadow == null) return 2;
            switch (shadow.growthTier)
            {
                case GrowthTier.Legendary: return 12;
                case GrowthTier.RegionalBoss: return 8;
                case GrowthTier.Rare: return 5;
                default: return 3;
            }
        }

        public static string ApplyBattleGrowth(BattleOutcome outcome, BattleMode mode)
        {
            SaveData save = SaveManager.Current;
            if (save == null || outcome == null || outcome.result != BattleResult.Victory) return string.Empty;
            var participants = new HashSet<string>(outcome.participantInstanceIds ?? new List<string>());
            int mastery = mode == BattleMode.Boss ? 6 : mode == BattleMode.Rival ? 4 : 2;
            int reserveExp = Mathf.Max(2, Mathf.RoundToInt(Mathf.Max(1, outcome.expGained) * .12f));
            int trained = 0, leveled = 0;
            foreach (ShadowInstance shadow in AllOwned(save))
            {
                if (shadow == null) continue;
                if (participants.Contains(shadow.instanceId))
                {
                    shadow.masteryPoints = SaveValuePolicy.SaturatingAdd(shadow.masteryPoints, mastery);
                    shadow.victories = SaveValuePolicy.SaturatingAdd(shadow.victories, 1);
                    trained++;
                }
                else if (!shadow.IsFainted && !shadow.IsMaxLevel)
                {
                    if (shadow.AddExp(reserveExp) > 0) leveled++;
                }
            }
            if (trained == 0 && leveled == 0) return string.Empty;
            return $"배역 숙련 +{mastery} · 대기 그림자 경험치 +{reserveExp}" +
                   (leveled > 0 ? $" · 레벨 상승 {leveled}명" : string.Empty);
        }

        public static bool TryCycleFocus(ShadowInstance shadow, out string message)
        {
            if (shadow == null) { message = "그림자를 선택해 주세요."; return false; }
            if (shadow.masteryPoints < FocusUnlockMastery)
            {
                message = $"배역 숙련 {FocusUnlockMastery}이 필요합니다. (현재 {shadow.masteryPoints})";
                return false;
            }
            bool firstChoice = shadow.trainingFocus == TrainingFocus.Balanced;
            if (!firstChoice && (SaveManager.Current?.memoryFragments ?? 0) < FocusChangeCost)
            {
                message = $"성장 방향을 다시 바꾸려면 기억 조각 {FocusChangeCost}개가 필요합니다.";
                return false;
            }
            int oldMax = shadow.MaxHp;
            float ratio = shadow.currentHp <= 0 ? 0f : (float)shadow.currentHp / Mathf.Max(1, oldMax);
            shadow.trainingFocus = shadow.trainingFocus == TrainingFocus.Tempo
                ? TrainingFocus.Power : (TrainingFocus)((int)shadow.trainingFocus + 1);
            if (!firstChoice) SaveManager.Current.memoryFragments -= FocusChangeCost;
            shadow.currentHp = ratio <= 0f ? 0 : Mathf.Max(1, Mathf.RoundToInt(shadow.MaxHp * ratio));
            SaveManager.Instance?.Save();
            message = $"성장 방향 · {FocusLabel(shadow.trainingFocus)}" +
                      (firstChoice ? " (첫 선택 무료)" : $" · 기억 조각 -{FocusChangeCost}");
            return true;
        }

        public static string FocusLabel(TrainingFocus focus)
        {
            switch (focus)
            {
                case TrainingFocus.Power: return "공격 연기";
                case TrainingFocus.Guard: return "수호 연기";
                case TrainingFocus.Tempo: return "속도 연기";
                default: return "균형 연기";
            }
        }

        private static IEnumerable<ShadowInstance> AllOwned(SaveData save)
        {
            foreach (ShadowInstance shadow in save.party ?? new List<ShadowInstance>()) yield return shadow;
            foreach (ShadowInstance shadow in save.storage ?? new List<ShadowInstance>()) yield return shadow;
        }
    }
}
