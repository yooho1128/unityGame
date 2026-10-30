using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShadowTheater.Data;
using ShadowTheater.Save;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>Read-only evolution checklist. Uses the same requirement predicate as growth.</summary>
    public static class EvolutionGuidanceService
    {
        [Serializable] public sealed class Route
        {
            public string kind, questId, regionId, targetId, eventId, label, instruction;
            public bool personal;
        }
        [Serializable] public sealed class Condition { public string flag; public Route[] routes; }
        [Serializable] public sealed class Catalog { public Condition[] entries; }
        private static Catalog catalog;
        private static CompanionEventCatalogData events;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { catalog = null; events = null; }
        public static IReadOnlyList<Condition> Conditions
        {
            get
            {
                if (catalog == null)
                {
                    var asset = Resources.Load<TextAsset>("Data/EvolutionGuidanceCatalog");
                    catalog = asset != null ? JsonUtility.FromJson<Catalog>(asset.text) : new Catalog();
                }
                return catalog?.entries ?? Array.Empty<Condition>();
            }
        }
        public static IReadOnlyList<Route> Routes(string flag) =>
            Conditions.FirstOrDefault(c => c.flag == flag)?.routes ?? Array.Empty<Route>();

        public static MemoryFormData Form(ShadowInstance instance, int stage) => instance?.Data == null ? null :
            stage == 0 ? instance.Data.restoredForm : stage == 1 ? instance.Data.salvationForm : instance.Data.grudgeForm;

        public static string Checklist(ShadowInstance instance, int stage)
        {
            var form = Form(instance, stage);
            if (form?.enabled != true) return "이 그림자는 해당 단계로 진화하지 않습니다.";
            if (instance == null) return "보유한 그림자를 선택하세요.";
            var text = new StringBuilder(form.formName).Append("\n\n");
            bool prior = stage == 0 || instance.memoryStage != MemoryStage.Echo;
            if (stage > 0) text.AppendLine((prior ? "✓ " : "□ ") + "기억 복원 완료");
            text.AppendLine((instance.level >= form.requiredLevel ? "✓ " : "□ ") + $"Lv.{form.requiredLevel} 이상 · 현재 Lv.{instance.level}");
            bool memory = string.IsNullOrEmpty(form.requiredFlag) || MemoryAwakeningService.RequirementMet(form.requiredFlag);
            text.AppendLine((memory ? "✓ " : "□ ") + (string.IsNullOrEmpty(form.requiredFlag) ? "추가 기억 조건 없음" : "아래 방법 중 하나로 기억 조건 충족"));
            text.AppendLine("소비 아이템 없음 · 레벨과 기억 조건으로 진화");
            if (instance.memoryStage == MemoryStage.TrueName)
                text.AppendLine("현재 최종 형태입니다. 구원·원한을 다시 선택할 수 없습니다.");
            else if (stage == 0 && instance.memoryStage == MemoryStage.Restored)
                text.AppendLine("이미 기억을 복원했습니다.");
            else if (prior && memory && instance.level >= form.requiredLevel)
                text.AppendLine("조건 충족 · 각본집으로 돌아가 진화 버튼을 누르세요.");
            return text.ToString();
        }
        public static string RequirementHint(string flag)
        {
            var route = Routes(flag).FirstOrDefault();
            if (route == null) return "기억 조건을 확인하려면 각본집의 진화 안내를 여세요.";
            if (route.kind == "Quest") return "기억 조건 · 「" + route.label + "」 완료 (진화 안내에서 추적)";
            return "기억 조건 · " + Describe(route).Replace('\n', ' ');
        }
        private static CompanionEventDefinition Event(Route route)
        {
            if (events == null)
            {
                var asset = Resources.Load<TextAsset>("Data/CompanionEventCatalog");
                events = asset != null ? JsonUtility.FromJson<CompanionEventCatalogData>(asset.text) : new CompanionEventCatalogData();
            }
            return events?.events?.FirstOrDefault(e => e.eventId == route?.eventId);
        }
        public static QuestObjectiveDefinition Objective(Route route)
        {
            if (route == null) return null;
            if (route.kind == "Quest")
            {
                if (!QuestRepository.TryGet(route.questId, out var quest)) return null;
                var progress = SaveManager.Current?.quests?.Find(p => p.questId == route.questId);
                return progress != null ? QuestTrackingPolicy.NextObjective(quest, progress) : quest.objectives?.FirstOrDefault();
            }
            return new QuestObjectiveDefinition { type = route.kind == "Companion" ? "Reach" : route.kind,
                targetId = route.kind == "Companion" ? "area_" + route.regionId : route.targetId };
        }
        public static string Describe(Route route)
        {
            if (route == null) return "레벨 조건을 충족하면 됩니다.";
            if (route.kind == "Quest")
            {
                if (!QuestRepository.TryGet(route.questId, out var quest)) return "여정 정보를 불러오지 못했습니다.";
                var progress = SaveManager.Current?.quests?.Find(p => p.questId == route.questId);
                return "여정 · " + quest.title + "\n" + (progress?.completed == true ? "✓ 완료" :
                    progress != null ? QuestGuidanceService.For(quest).text : route.personal ? "「여정 시작·추적」으로 개인 기억 여정을 시작하세요." : "이야기를 진행해 이 여정을 시작하세요.");
            }
            var region = RegionRepository.Get(route.regionId);
            if (region == null || !QuestGuidanceService.IsKnown(region))
                return "장소 단서 필요 · 마을 소문과 이야기를 진행해 길을 발견하세요.";
            string text = region.displayName + " · " + route.label + "\n" + route.instruction;
            if (route.kind == "Companion")
            {
                var e = Event(route);
                if (e == null) return "동행 기억 정보를 불러오지 못했습니다.";
                var shadow = ShadowDatabase.Instance?.GetShadow(e.requiredShadowId);
                text += "\n필요 동행 · " + (shadow != null ? shadow.displayName : "해당 기억의 그림자") +
                    $"\n같은 동행을 유지해 {e.minSteps}걸음부터 탐색, {e.guaranteedSteps}걸음에 발동을 시도합니다. 대화를 끝까지 마치세요.";
            }
            return text + "\n\n" + QuestGuidanceService.ForObjective(Objective(route)).text;
        }
    }
}
