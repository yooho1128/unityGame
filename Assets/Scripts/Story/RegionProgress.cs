using ShadowTheater.Save;

namespace ShadowTheater.Story
{
    public enum RegionMapState { Locked, Unlocked, Visited, Current }

    /// <summary>지역 카탈로그와 세이브의 해금/방문 상태를 연결한다.</summary>
    public static class RegionProgress
    {
        public static RegionMapState GetState(RegionData region)
        {
            var save = SaveManager.Current;
            if (region == null || save == null) return RegionMapState.Locked;
            if (save.mapId == region.sceneName) return RegionMapState.Current;
            if (save.visitedRegionIds.Contains(region.regionId)) return RegionMapState.Visited;
            return CanEnter(region) ? RegionMapState.Unlocked : RegionMapState.Locked;
        }

        public static bool CanEnterScene(string sceneName)
        {
            var region = RegionRepository.GetByScene(sceneName);
            return region == null || CanEnter(region);
        }

        public static bool CanEnter(RegionData region)
        {
            var save = SaveManager.Current;
            if (region == null || save == null) return false;
            if (region.order <= 1 || save.mapId == region.sceneName) return true;
            if (save.unlockedRegionIds.Contains(region.regionId) || save.visitedRegionIds.Contains(region.regionId))
                return string.IsNullOrEmpty(region.requiredFlag) || SaveManager.HasFlag(region.requiredFlag);
            return false;
        }

        public static void SyncCurrentMap()
        {
            if (SaveManager.Current != null) MarkVisitedScene(SaveManager.Current.mapId);
        }

        public static void MarkVisitedScene(string sceneName)
        {
            var save = SaveManager.Current;
            var region = RegionRepository.GetByScene(sceneName);
            if (save == null || region == null) return;
            AddUnique(save.unlockedRegionIds, region.regionId);
            AddUnique(save.visitedRegionIds, region.regionId);
            foreach (string connectedId in region.connectedRegionIds ?? new string[0])
                if (!string.IsNullOrEmpty(connectedId)) AddUnique(save.unlockedRegionIds, connectedId);
            SaveManager.SetFlag("region_" + region.regionId + "_visited");
        }

        private static void AddUnique(System.Collections.Generic.List<string> list, string value)
        {
            if (list != null && !string.IsNullOrEmpty(value) && !list.Contains(value)) list.Add(value);
        }
    }
}
