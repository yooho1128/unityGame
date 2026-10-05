using System;
using System.Collections.Generic;
using ShadowTheater.Data;
using ShadowTheater.Save;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>잔영 → 기억 복원 → 구원/원한 진명 각성의 조건 검사와 영구 데이터 변경.</summary>
    public static class MemoryAwakeningService
    {
        public static event Action<ShadowInstance> StageChanged;

        // 초기 6종의 전용 플래그는 기존 세이브와 데이터 ID를 유지하면서 실제 스토리 이정표에 연결한다.
        private static readonly Dictionary<string, string> StoryMilestoneAliases =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "memory_knight_restored", "boss_moonlit_story_complete" },
                { "truth_knight_revealed", "truth_ash_king_revealed" },
                { "memory_mage_restored", "memory_ferry_manifest" },
                { "truth_mage_revealed", "truth_first_forbidden_book" },
                { "memory_beast_restored", "howl_village_truth" },
                { "truth_beast_revealed", "truth_first_howl" },
                { "memory_puppet_restored", "clock_alley_released" },
                { "truth_puppet_revealed", "truth_puppet_origin" },
                { "memory_crow_restored", "boss_moonlit_story_complete" },
                { "truth_crow_revealed", "truth_first_howl" },
                { "memory_mask_restored", "mirror_truth_seen" },
                { "truth_mask_revealed", "truth_nox_family" },
                { "boss_nox_story_complete", "boss_high_censor_nox_story_complete" },
                { "memory_moonlit_truth", "boss_true_name_selene_story_complete" },
                { "legend_first_actor_found", "boss_first_actor_story_complete" },
                { "legend_first_actor_truth", "boss_first_actor_story_complete" },
                { "legend_last_audience_found", "boss_last_audience_story_complete" },
                { "legend_last_audience_truth", "boss_last_audience_story_complete" }
            };

        public static bool CanRestore(ShadowInstance instance, out string reason)
        {
            if (instance == null) { reason = "그림자를 선택해 주세요."; return false; }
            if (instance.memoryStage != MemoryStage.Echo)
            {
                reason = "이미 기억을 복원한 그림자입니다.";
                return false;
            }
            return MeetsRequirements(instance, instance.Data.restoredForm, "기억 복원", out reason);
        }

        public static bool CanAwaken(ShadowInstance instance, AwakeningPath path, out string reason)
        {
            if (instance == null) { reason = "그림자를 선택해 주세요."; return false; }
            if (path == AwakeningPath.None) { reason = "각성 방향을 선택해 주세요."; return false; }
            if (instance.memoryStage == MemoryStage.Echo)
            {
                reason = "먼저 기억을 복원해야 합니다.";
                return false;
            }
            if (instance.memoryStage == MemoryStage.TrueName)
            {
                reason = "이미 진명을 되찾은 그림자입니다.";
                return false;
            }
            var form = path == AwakeningPath.Grudge ? instance.Data.grudgeForm : instance.Data.salvationForm;
            return MeetsRequirements(instance, form, "진명 각성", out reason);
        }

        public static bool TryRestore(ShadowInstance instance, out string message)
        {
            if (!CanRestore(instance, out message)) return false;
            MaterializeMilestone(instance.Data.restoredForm);
            ApplyStage(instance, MemoryStage.Restored, AwakeningPath.None);
            message = $"{instance.DisplayName}의 기억이 복원되었습니다.";
            return true;
        }

        public static bool TryAwaken(ShadowInstance instance, AwakeningPath path, out string message)
        {
            if (!CanAwaken(instance, path, out message)) return false;
            MaterializeMilestone(path == AwakeningPath.Grudge ? instance.Data.grudgeForm : instance.Data.salvationForm);
            ApplyStage(instance, MemoryStage.TrueName, path);
            message = path == AwakeningPath.Salvation
                ? $"{instance.DisplayName}이 구원의 진명을 되찾았습니다."
                : $"{instance.DisplayName}이 원한의 진명을 받아들였습니다.";
            return true;
        }

        public static string StageLabel(ShadowInstance instance)
        {
            if (instance == null) return "미보유";
            if (instance.memoryStage == MemoryStage.TrueName)
                return instance.awakeningPath == AwakeningPath.Grudge ? "진명 각성 · 원한" : "진명 각성 · 구원";
            return instance.memoryStage == MemoryStage.Restored ? "기억 복원" : "잔영";
        }

        private static bool MeetsRequirements(ShadowInstance instance, MemoryFormData form,
            string actionName, out string reason)
        {
            if (form == null || !form.enabled)
            {
                reason = $"이 그림자의 {actionName} 데이터가 아직 설정되지 않았습니다.";
                return false;
            }
            if (instance.level < form.requiredLevel)
            {
                reason = $"레벨 {form.requiredLevel}이 필요합니다. (현재 {instance.level})";
                return false;
            }
            if (!string.IsNullOrEmpty(form.requiredFlag) && !RequirementMet(form.requiredFlag))
            {
                reason = "아직 되찾지 못한 개인 기억이 있습니다.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public static bool HasStoryMilestoneAlias(string requiredFlag) =>
            !string.IsNullOrEmpty(requiredFlag) && StoryMilestoneAliases.ContainsKey(requiredFlag);

        private static bool RequirementMet(string requiredFlag)
        {
            if (SaveManager.HasFlag(requiredFlag)) return true;
            return StoryMilestoneAliases.TryGetValue(requiredFlag, out string sourceFlag) &&
                   SaveManager.HasFlag(sourceFlag);
        }

        private static void MaterializeMilestone(MemoryFormData form)
        {
            if (form == null || string.IsNullOrEmpty(form.requiredFlag) || SaveManager.HasFlag(form.requiredFlag)) return;
            if (RequirementMet(form.requiredFlag)) SaveManager.SetFlag(form.requiredFlag);
        }

        private static void ApplyStage(ShadowInstance instance, MemoryStage stage, AwakeningPath path)
        {
            int oldMax = Mathf.Max(1, instance.MaxHp);
            float hpRatio = instance.currentHp <= 0 ? 0f : (float)instance.currentHp / oldMax;
            instance.memoryStage = stage;
            instance.awakeningPath = path;
            instance.currentHp = hpRatio <= 0f ? 0 : Mathf.Max(1, Mathf.RoundToInt(instance.MaxHp * hpRatio));
            SaveManager.Instance.Save();
            StageChanged?.Invoke(instance);
        }
    }
}
