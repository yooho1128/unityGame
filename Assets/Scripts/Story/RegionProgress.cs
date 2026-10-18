using System.Collections.Generic;
using ShadowTheater.Save;

namespace ShadowTheater.Story
{
    public enum RegionMapState { Locked, Unlocked, Visited, Current }
    public enum RegionMapKnowledge { Hidden, Locked, RouteKnown, Rumored, Visited, Current }

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

        public static RegionMapKnowledge GetKnowledge(RegionData region)
        {
            RegionMapState state = GetState(region);
            if (state == RegionMapState.Current) return RegionMapKnowledge.Current;
            if (state == RegionMapState.Visited) return RegionMapKnowledge.Visited;
            if (region?.isHidden == true)
                return TownServicePolicy.CanShowHiddenHint(region)
                    ? RegionMapKnowledge.Rumored : RegionMapKnowledge.Hidden;
            return state == RegionMapState.Unlocked ? RegionMapKnowledge.RouteKnown : RegionMapKnowledge.Locked;
        }

        public static bool CanEnterScene(string sceneName)
        {
            var region = RegionRepository.GetByScene(sceneName);
            return region != null && CanEnter(region);
        }

        /// <summary>현재 지도에서 발견한 길만 따라가는 최단 도보 경로. 숨은 길과 미해금 관문은 안내하지 않는다.</summary>
        public static List<RegionData> FindKnownRoute(RegionData destination)
        {
            var origin = RegionRepository.GetByScene(SaveManager.Current?.mapId);
            var route = new List<RegionData>();
            if (origin == null || destination == null) return route;
            var previous = new Dictionary<string, RegionData> { [origin.regionId] = null };
            var pending = new Queue<RegionData>();
            pending.Enqueue(origin);
            while (pending.Count > 0)
            {
                var region = pending.Dequeue();
                if (region.regionId == destination.regionId)
                {
                    for (var step = region; step != null; step = previous[step.regionId]) route.Add(step);
                    route.Reverse();
                    return route;
                }
                foreach (string id in region.connectedRegionIds ?? System.Array.Empty<string>())
                {
                    if (string.IsNullOrEmpty(id) || previous.ContainsKey(id)) continue;
                    var next = RegionRepository.Get(id);
                    var knowledge = GetKnowledge(next);
                    if (next == null || (knowledge != RegionMapKnowledge.Current &&
                        knowledge != RegionMapKnowledge.Visited && knowledge != RegionMapKnowledge.RouteKnown) ||
                        !MeetsTravelRequirement(next.sceneName, next.requiredFlag)) continue;
                    previous.Add(id, region);
                    pending.Enqueue(next);
                }
            }
            return route;
        }

        public static bool CanEnter(RegionData region)
        {
            var save = SaveManager.Current;
            if (region == null || save == null) return false;
            if (region.order <= 1 || save.mapId == region.sceneName) return true;
            bool requirementMet = MeetsTravelRequirement(region.sceneName, region.requiredFlag);
            if (save.unlockedRegionIds.Contains(region.regionId) || save.visitedRegionIds.Contains(region.regionId))
                return requirementMet;
            RegionData current = RegionRepository.GetByScene(save.mapId);
            if (current != null && System.Array.Exists(current.connectedRegionIds ?? new string[0],
                    connectedId => connectedId == region.regionId))
                return requirementMet;
            return false;
        }

        /// <summary>일반 길은 퀘스트 체크리스트와 분리한다. 보스 관문과 숨은 길의 조건은 유지한다.</summary>
        public static bool MeetsTravelRequirement(string targetScene, string requiredFlag)
        {
            if (string.IsNullOrEmpty(requiredFlag) || SaveManager.HasFlag(requiredFlag)) return true;
            RegionData destination = RegionRepository.GetByScene(targetScene);
            return destination != null && !destination.isHidden &&
                requiredFlag.StartsWith("quest_", System.StringComparison.Ordinal) &&
                requiredFlag.EndsWith("_complete", System.StringComparison.Ordinal);
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
            {
                if (string.IsNullOrEmpty(connectedId)) continue;
                RegionData connected = RegionRepository.Get(connectedId);
                if (connected?.isHidden == true) continue;
                AddUnique(save.unlockedRegionIds, connectedId);
            }
            SaveManager.SetFlag("region_" + region.regionId + "_visited");
        }

        private static void AddUnique(System.Collections.Generic.List<string> list, string value)
        {
            if (list != null && !string.IsNullOrEmpty(value) && !list.Contains(value)) list.Add(value);
        }
    }
}
