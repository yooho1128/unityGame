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
    }
}
