using System;
using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Story
{
    public static class QuestGuidanceService
    {
        [Serializable] public sealed class Target
        { public string type, targetId, regionId, label; public int x, y; }
        [Serializable] public sealed class Catalog { public Target[] entries; }
        public sealed class Guidance
        {
            public QuestObjectiveDefinition objective;
            public RegionData destination;
            public Vector3? waypoint;
            public string text;
        }
        private static Catalog catalog;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => catalog = null;
        public static IReadOnlyList<Target> Targets
        {
            get
            {
                if (catalog == null)
                {
                    var asset = Resources.Load<TextAsset>("Data/QuestNavigationCatalog");
                    catalog = asset != null ? JsonUtility.FromJson<Catalog>(asset.text) : new Catalog();
                }
                return catalog?.entries ?? Array.Empty<Target>();
            }
        }
        // Map markers only need the destination; avoid scene scans and full route text per marker.
        public static RegionData DestinationFor(QuestDefinition quest)
        {
            var progress = SaveManager.Current?.quests?.Find(p => p != null && p.questId == quest?.questId);
            var objective = QuestTrackingPolicy.NextObjective(quest, progress);
            if (objective == null) return null;
            var target = Targets.FirstOrDefault(t => t != null && t.type == objective.type && t.targetId == objective.targetId);
            var destination = ResolveDestination(objective, target);
            return destination != null && IsKnown(destination) ? destination : null;
        }
        private static RegionData ResolveDestination(QuestObjectiveDefinition objective, Target target)
        {
            bool hunt = objective.targetId == "*" && (objective.type == "Record" || objective.type == "Defeat");
            var destination = hunt ? NearestKnownHuntingGround() : target != null ? RegionRepository.Get(target.regionId) : null;
            if (destination == null && objective.type == "Reach")
            {
                string id = objective.targetId ?? "";
                destination = RegionRepository.Get(id.StartsWith("area_", StringComparison.Ordinal) ? id.Substring(5) : id)
                    ?? RegionRepository.GetByScene(id);
            }
            return destination;
        }
        public static Guidance For(QuestDefinition quest)
        {
            var save = SaveManager.Current;
            var progress = save?.quests?.Find(p => p != null && p.questId == quest?.questId);
            var objective = QuestTrackingPolicy.NextObjective(quest, progress);
            return ForObjective(objective);
        }
        // Explicit guide previews never create progress or complete a quest.
        public static Guidance ForObjective(QuestObjectiveDefinition objective)
        {
            var save = SaveManager.Current;
            var g = new Guidance { objective = objective, text = "진행할 목표를 선택하세요." };
            if (objective == null || save == null) return g;
            bool hunt = objective.targetId == "*" && (objective.type == "Record" || objective.type == "Defeat");
            var target = Targets.FirstOrDefault(t => t != null && t.type == objective.type && t.targetId == objective.targetId);
            RegionData destination = ResolveDestination(objective, target);
            if (destination == null)
            { g.text = hunt ? "알려진 야생 조우 지역으로 길을 넓혀 보세요." : "목표 장소의 단서를 여정 설명에서 확인하세요."; return g; }
            if (!IsKnown(destination))
            { g.text = "아직 안내할 수 없는 장소입니다. 마을의 소문과 이야기 단서를 확인하세요."; return g; }
            g.destination = destination;
            string place = L10n.Text(destination.displayName);
            var route = RegionProgress.FindKnownRoute(destination);
            if (route.Count == 0)
            { g.text = "목적지로 이어지는 길의 관문 조건을 먼저 확인하세요."; return g; }
            if (route.Count > 1)
            {
                var exits = UnityEngine.Object.FindObjectsOfType<MapPortal>().Where(p => p.DestinationScene == route[1].sceneName).ToArray();
                var exit = exits.FirstOrDefault(p => p.CanGuideTravel);
                if (exits.Length > 0 && exit == null)
                { g.text = "다음 출구가 잠겨 있습니다. 이야기·이동 권능 조건을 확인하세요."; return g; }
                g.waypoint = exit != null ? (Vector3?)exit.transform.position : null;
                string direction = g.waypoint.HasValue ? Direction(g.waypoint.Value) + " 출구 → " : "다음 지역 → ";
                g.text = direction + L10n.Text(route[1].displayName) + $" · 목적지까지 {route.Count - 1}구간\n목표 지역 · " + place;
                return g;
            }
            if (hunt)
            {
                g.text = objective.type == "Record" ? "◎ " + place + " · 야생 그림자를 약화시켜 각본 기록을 시도하세요."
                    : "◎ " + place + " · 야생 전투에서 승리하세요.";
                return g;
            }
            var cell = target != null ? new Vector2Int(target.x, target.y) : destination.ArrivalCell;
            Vector3 position = FieldGrid.Current != null ? FieldGrid.Current.CellToWorld(cell) : new Vector3(cell.x + .5f, cell.y + .5f, 0f);
            if (objective.type == "Talk")
            {
                var npc = UnityEngine.Object.FindObjectsOfType<StoryNpc>().FirstOrDefault(n => n.NpcId == objective.targetId);
                if (npc != null) position = npc.transform.position;
            }
            else if (objective.type == "Defeat")
            {
                var boss = UnityEngine.Object.FindObjectsOfType<BossEncounterTrigger>().FirstOrDefault(b => b.EncounterId == objective.targetId);
                if (boss != null) position = boss.transform.position;
            }
            g.waypoint = position;
            string action = objective.type == "Talk" ? "마주 보고 대화·조사하세요." : objective.type == "Defeat" ? "보스에게 다가가 전투를 시작하세요." : "입구 주변의 목표 지점을 지나가세요.";
            g.text = "◎ " + place + " · " + Direction(position) + "\n" + (target != null ? L10n.Text(target.label) + " · " : "") + action;
            return g;
        }
        public static bool IsKnown(RegionData region)
        {
            var state = RegionProgress.GetKnowledge(region);
            return region != null && (state == RegionMapKnowledge.Current || state == RegionMapKnowledge.Visited || state == RegionMapKnowledge.RouteKnown);
        }
        public static string Direction(Vector3 target)
        {
            var player = PlayerController.Instance;
            if (player == null) return "목표 방향";
            Vector2 delta = target - player.transform.position;
            if (delta.sqrMagnitude <= 2.25f) return "바로 근처";
            return Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? (delta.x > 0 ? "→ 동쪽" : "← 서쪽") : (delta.y > 0 ? "↑ 북쪽" : "↓ 남쪽");
        }
        private static RegionData NearestKnownHuntingGround()
        {
            var origin = RegionRepository.GetByScene(SaveManager.Current?.mapId);
            if (origin == null) return null;
            var pending = new Queue<RegionData>(); pending.Enqueue(origin);
            var seen = new HashSet<string> { origin.regionId };
            while (pending.Count > 0)
            {
                var region = pending.Dequeue();
                if (region.AllowsRandomEncounters) return region;
                foreach (string id in region.connectedRegionIds ?? Array.Empty<string>())
                {
                    if (!seen.Add(id)) continue;
                    var next = RegionRepository.Get(id);
                    if (next != null && IsKnown(next) && RegionProgress.MeetsTravelRequirement(next.sceneName, next.requiredFlag)) pending.Enqueue(next);
                }
            }
            return null;
        }
    }
}
