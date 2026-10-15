using ShadowTheater.Data;
using ShadowTheater.Save;

namespace ShadowTheater.Story
{
    /// <summary>동행 이벤트로 맺은 유대가 필드 탐험에 주는 영구 보정.</summary>
    public static class CompanionBondService
    {
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
