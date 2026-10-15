using ShadowTheater.Data;
using ShadowTheater.Save;
using ShadowTheater.Battle;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>동행 이벤트로 맺은 유대가 필드 탐험에 주는 영구 보정.</summary>
    public static class CompanionBondService
    {
        private static readonly int[] LevelThresholds = { 0, 40, 120, 260, 480 };
        public const int MaxBondLevel = 5;
        public const int JointSkillUnlockLevel = 3;
        public const string MaskCalmFlag = "companion_bond_mask_calm";
        public const string InkFoxTracelessFlag = "companion_bond_ink_fox_traceless";
        public const string ThreadCatMendingFlag = "companion_bond_thread_cat_mending";

        public static int EncounterSafeStepBonus(string shadowId) =>
            shadowId == "mask" && SaveManager.HasFlag(MaskCalmFlag) ? 3 : 0;

        public static float EncounterChanceMultiplier(string shadowId) =>
            shadowId == "ink_fox" && SaveManager.HasFlag(InkFoxTracelessFlag) ? .75f : 1f;

        public static int ModifyFieldHealing(string shadowId, int amount)
        {
            if (shadowId == "thread_cat" && SaveManager.HasFlag(ThreadCatMendingFlag))
                return UnityEngine.Mathf.CeilToInt(amount * 1.2f);
            return amount;
        }

        public static string CurrentCompanionShadowId()
        {
            SaveData save = SaveManager.Current;
            if (save?.party == null) return string.Empty;
            var companion = save.party.Find(x => x.instanceId == save.companionInstanceId);
            return companion?.shadowId ?? (save.party.Count > 0 ? save.party[0].shadowId : string.Empty);
        }

        public static ShadowInstance CurrentCompanion()
        {
            SaveData save = SaveManager.Current;
            if (save?.party == null) return null;
            return save.party.Find(x => x.instanceId == save.companionInstanceId) ??
                   (save.party.Count > 0 ? save.party[0] : null);
        }

        public static CompanionBondProgressData Progress(string instanceId, bool create = false)
        {
            SaveData save = SaveManager.Current;
            if (save == null || string.IsNullOrWhiteSpace(instanceId)) return null;
            save.companionBonds ??= new List<CompanionBondProgressData>();
            CompanionBondProgressData progress = save.companionBonds.Find(x => x.instanceId == instanceId);
            if (progress == null && create)
            {
                progress = new CompanionBondProgressData { instanceId = instanceId };
                save.companionBonds.Add(progress);
            }
            return progress;
        }

        public static int BondLevel(string instanceId) => LevelForExperience(Progress(instanceId)?.experience ?? 0);

        public static int LevelForExperience(int experience)
        {
            int safe = Mathf.Max(0, experience);
            for (int i = LevelThresholds.Length - 1; i >= 0; i--)
                if (safe >= LevelThresholds[i]) return i + 1;
            return 1;
        }

        public static int ExperienceForNextLevel(string instanceId)
        {
            int level = BondLevel(instanceId);
            return level >= MaxBondLevel ? 0 : LevelThresholds[level];
        }

        public static bool AddExperience(string instanceId, int amount, out int oldLevel, out int newLevel)
        {
            oldLevel = BondLevel(instanceId);
            CompanionBondProgressData progress = Progress(instanceId, amount > 0);
            if (progress == null || amount <= 0) { newLevel = oldLevel; return false; }
            progress.experience = SaveValuePolicy.SaturatingAdd(Mathf.Max(0, progress.experience), amount);
            newLevel = LevelForExperience(progress.experience);
            return newLevel > oldLevel;
        }

        public static bool CanUseJointSkill(ShadowInstance companion) =>
            companion != null && BondLevel(companion.instanceId) >= JointSkillUnlockLevel;

        public static CompanionJointProfile JointProfile(ShadowInstance companion)
        {
            if (companion?.Data == null) return CompanionJointProfile.None;
            ShadowData data = companion.Data;
            string name;
            switch (data.element)
            {
                case ShadowElement.Flame: name = "붉은 막의 이중주"; break;
                case ShadowElement.Frost: name = "서리 장면의 이중주"; break;
                case ShadowElement.Shade: name = "그림자 막간의 이중주"; break;
                default: name = "무명의 합동 연출"; break;
            }
            StatusEffectType status = data.role == ShadowRole.SpeedUtility ? StatusEffectType.SpeedDown
                : data.role == ShadowRole.Tank ? StatusEffectType.AttackDown
                : data.role == ShadowRole.Support ? StatusEffectType.DefenseDown : StatusEffectType.None;
            int level = BondLevel(companion.instanceId);
            return new CompanionJointProfile(name, data.element, 1.35f + level * .08f,
                status, status == StatusEffectType.None ? 0f : .45f + level * .05f);
        }

        public static void RecordJointSkillUse(string instanceId)
        {
            CompanionBondProgressData progress = Progress(instanceId, true);
            if (progress != null) progress.jointSkillUses = SaveValuePolicy.SaturatingAdd(progress.jointSkillUses, 1);
        }

        public static int VictoryExperience(BattleMode mode) =>
            mode == BattleMode.Boss ? 24 : mode == BattleMode.Rival ? 14 : 8;

        /// <summary>
        /// 지정 동행의 속성은 탐험 보정, 역할은 전투 보정을 결정한다.
        /// 개별 유대 이벤트 보정과 달리 모든 그림자에 항상 적용된다.
        /// </summary>
        public static CompanionPassiveSnapshot CurrentPassive()
        {
            SaveData save = SaveManager.Current;
            if (save?.party == null) return CompanionPassiveSnapshot.None;
            ShadowInstance companion = save.party.Find(x => x.instanceId == save.companionInstanceId) ??
                                       (save.party.Count > 0 ? save.party[0] : null);
            return companion?.Data == null ? CompanionPassiveSnapshot.None : From(companion.Data);
        }

        public static CompanionPassiveSnapshot From(ShadowData shadow)
        {
            if (shadow == null) return CompanionPassiveSnapshot.None;
            float encounter = shadow.element == ShadowElement.Shade ? .92f : 1f;
            float healing = shadow.element == ShadowElement.Frost ? 1.08f : 1f;
            int safeSteps = shadow.element == ShadowElement.Flame ? 1 : 0;
            float attack = shadow.role == ShadowRole.PhysicalDealer || shadow.role == ShadowRole.MagicNuker ? 1.05f : 1f;
            float defense = shadow.role == ShadowRole.Tank ? 1.07f : shadow.role == ShadowRole.Support ? 1.03f : 1f;
            float speed = shadow.role == ShadowRole.SpeedUtility ? 1.07f : 1f;
            float critical = shadow.role == ShadowRole.PhysicalDealer ? .02f : 0f;
            float evasion = shadow.role == ShadowRole.SpeedUtility ? .02f : 0f;
            float capture = shadow.role == ShadowRole.Support ? 1.08f : 1f;
            return new CompanionPassiveSnapshot(shadow.shadowId, encounter, healing, safeSteps,
                attack, defense, speed, critical, evasion, capture);
        }

        public static string Describe(ShadowData shadow)
        {
            if (shadow == null) return "동행 효과 없음";
            CompanionPassiveSnapshot p = From(shadow);
            var values = new List<string>();
            if (p.safeStepBonus > 0) values.Add($"안전 걸음 +{p.safeStepBonus}");
            if (p.encounterMultiplier < 1f) values.Add($"조우율 -{Percent(1f - p.encounterMultiplier)}");
            if (p.fieldHealingMultiplier > 1f) values.Add($"필드 회복 +{Percent(p.fieldHealingMultiplier - 1f)}");
            if (p.attackMultiplier > 1f) values.Add($"공격 +{Percent(p.attackMultiplier - 1f)}");
            if (p.defenseMultiplier > 1f) values.Add($"방어 +{Percent(p.defenseMultiplier - 1f)}");
            if (p.speedMultiplier > 1f) values.Add($"속도 +{Percent(p.speedMultiplier - 1f)}");
            if (p.criticalBonus > 0f) values.Add($"치명 +{Percent(p.criticalBonus)}");
            if (p.evasionBonus > 0f) values.Add($"회피 +{Percent(p.evasionBonus)}");
            if (p.captureMultiplier > 1f) values.Add($"기록률 +{Percent(p.captureMultiplier - 1f)}");
            return string.Join(" · ", values);
        }

        private static int Percent(float value) => UnityEngine.Mathf.RoundToInt(value * 100f);
    }

    public readonly struct CompanionJointProfile
    {
        public static readonly CompanionJointProfile None = new CompanionJointProfile(
            string.Empty, ShadowElement.None, 0f, StatusEffectType.None, 0f);
        public readonly string displayName;
        public readonly ShadowElement element;
        public readonly float power;
        public readonly StatusEffectType status;
        public readonly float statusChance;
        public CompanionJointProfile(string displayName, ShadowElement element, float power,
            StatusEffectType status, float statusChance)
        { this.displayName = displayName; this.element = element; this.power = power; this.status = status; this.statusChance = statusChance; }
    }

    public readonly struct CompanionPassiveSnapshot
    {
        public static readonly CompanionPassiveSnapshot None = new CompanionPassiveSnapshot(
            string.Empty, 1f, 1f, 0, 1f, 1f, 1f, 0f, 0f, 1f);

        public readonly string shadowId;
        public readonly float encounterMultiplier, fieldHealingMultiplier;
        public readonly int safeStepBonus;
        public readonly float attackMultiplier, defenseMultiplier, speedMultiplier;
        public readonly float criticalBonus, evasionBonus, captureMultiplier;

        public CompanionPassiveSnapshot(string shadowId, float encounterMultiplier,
            float fieldHealingMultiplier, int safeStepBonus, float attackMultiplier,
            float defenseMultiplier, float speedMultiplier, float criticalBonus,
            float evasionBonus, float captureMultiplier)
        {
            this.shadowId = shadowId;
            this.encounterMultiplier = encounterMultiplier;
            this.fieldHealingMultiplier = fieldHealingMultiplier;
            this.safeStepBonus = safeStepBonus;
            this.attackMultiplier = attackMultiplier;
            this.defenseMultiplier = defenseMultiplier;
            this.speedMultiplier = speedMultiplier;
            this.criticalBonus = criticalBonus;
            this.evasionBonus = evasionBonus;
            this.captureMultiplier = captureMultiplier;
        }
    }
}
