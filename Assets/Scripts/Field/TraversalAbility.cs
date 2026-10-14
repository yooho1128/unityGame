using System;
using ShadowTheater.Data;
using ShadowTheater.Save;

namespace ShadowTheater.Field
{
    [Flags]
    public enum TraversalAbility
    {
        None = 0,
        PortableBoat = 1 << 0,
        FreezeShallows = 1 << 1,
        AquaticMount = 1 << 2,
        Glide = 1 << 3,
        ClockworkRide = 1 << 4,
        SpiritLight = 1 << 5
    }

    /// <summary>가방의 핵심 도구와 각본집에 기록한 그림자를 필드 이동 권능으로 해석한다.</summary>
    public static class TraversalAbilityService
    {
        public const string RubberBoatItemId = "rubber_boat";
        public const string FrostwingCompassItemId = "frostwing_compass";
        public const string SpiritProcessionLanternItemId = "spirit_procession_lantern";
        public const string HeartWindingKeyItemId = "heart_winding_key";

        public static bool TryResolve(TraversalAbility allowed, SaveData save, ShadowDatabase database,
            out TraversalAbility resolved, out string message)
        {
            resolved = TraversalAbility.None;
            if ((allowed & TraversalAbility.PortableBoat) != 0 &&
                save?.inventory?.Exists(x => x != null && x.itemId == RubberBoatItemId && x.count > 0) == true)
            {
                resolved = TraversalAbility.PortableBoat;
                message = "휴대용 고무보트를 펼쳐 물길을 건넙니다.";
                return true;
            }
            if ((allowed & TraversalAbility.FreezeShallows) != 0 && HasRecordedElement(save, database, ShadowElement.Frost))
            {
                resolved = TraversalAbility.FreezeShallows;
                message = "서리 그림자의 권능으로 얕은 물을 얼립니다.";
                return true;
            }
            if ((allowed & TraversalAbility.AquaticMount) != 0 && HasFlag(save, "traversal_aquatic_mount"))
            {
                resolved = TraversalAbility.AquaticMount;
                message = "물의 그림자를 불러 물길을 건넙니다.";
                return true;
            }
            if ((allowed & TraversalAbility.Glide) != 0 &&
                (HasItem(save, FrostwingCompassItemId) || HasFlag(save, "traversal_glide")))
            {
                resolved = TraversalAbility.Glide;
                message = HasItem(save, FrostwingCompassItemId)
                    ? "귀항의 깃나침반으로 안전한 바람길을 찾아 건너갑니다."
                    : "비행 그림자의 날개를 빌려 건너갑니다.";
                return true;
            }
            if ((allowed & TraversalAbility.ClockworkRide) != 0 &&
                (HasItem(save, HeartWindingKeyItemId) || HasFlag(save, "traversal_clockwork_ride")))
            {
                resolved = TraversalAbility.ClockworkRide;
                message = "태엽 탈것을 꺼내 길을 달립니다.";
                return true;
            }
            if ((allowed & TraversalAbility.SpiritLight) != 0 && HasItem(save, SpiritProcessionLanternItemId))
            {
                resolved = TraversalAbility.SpiritLight;
                message = "혼불 행렬등을 밝혀 기억이 감춘 길을 드러냅니다.";
                return true;
            }

            message = MissingMessage(allowed);
            return false;
        }

        private static bool HasRecordedElement(SaveData save, ShadowDatabase database, ShadowElement element)
        {
            if (save?.recordedShadowIds == null || database == null) return false;
            foreach (string shadowId in save.recordedShadowIds)
                if (database.GetShadow(shadowId)?.element == element) return true;
            return false;
        }

        private static bool HasFlag(SaveData save, string key) =>
            save?.flags?.Exists(x => x != null && x.key == key && x.value != 0) == true;

        private static bool HasItem(SaveData save, string itemId) =>
            save?.inventory?.Exists(x => x != null && x.itemId == itemId && x.count > 0) == true;

        private static string MissingMessage(TraversalAbility allowed)
        {
            if (allowed == TraversalAbility.PortableBoat)
                return "이 물길을 건너려면 정착지 상점에서 휴대용 고무보트를 준비해야 합니다.";
            if (allowed == TraversalAbility.FreezeShallows)
                return "얕은 물을 얼리려면 서리 속성 그림자를 먼저 각본집에 기록해야 합니다.";
            if (allowed == TraversalAbility.Glide)
                return "먼 거리의 바람길을 읽어 줄 깃나침반이 필요합니다.";
            if (allowed == TraversalAbility.SpiritLight)
                return "기억이 감춘 길을 밝힐 특별한 등불이 필요합니다.";
            if (allowed == TraversalAbility.ClockworkRide)
                return "멈춘 태엽 탈것을 깨울 심장 태엽키가 필요합니다.";
            return "이 길을 이용할 수 있는 이동 도구나 그림자 권능이 아직 없습니다.";
        }
    }
}
