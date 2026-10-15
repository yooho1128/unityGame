using System;
using System.Collections;
using System.Collections.Generic;
using ShadowTheater.Save;
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    public readonly struct FieldIncidentDefinition
    {
        public readonly string id, title, message, itemId;
        public readonly FieldTimeOfDay time;
        public readonly FieldWeather weather;
        public readonly int act, gold, itemCount, bondExperience;
        public FieldIncidentDefinition(string id, string title, string message, FieldTimeOfDay time,
            FieldWeather weather, int act, int gold, string itemId, int itemCount, int bondExperience)
        { this.id = id; this.title = title; this.message = message; this.time = time; this.weather = weather;
          this.act = act; this.gold = gold; this.itemId = itemId; this.itemCount = itemCount;
          this.bondExperience = bondExperience; }
    }

    /// <summary>특정 시간·날씨 조합의 지역 사건을 하루 한 번 판정하고 보상을 안전 저장한다.</summary>
    public static class FieldIncidentService
    {
        private static readonly FieldIncidentDefinition[] Incidents =
        {
            new FieldIncidentDefinition("ash_procession", "재의 행렬", "해질녘 잿가루 속 배우들의 행렬을 끝까지 배웅했다.", FieldTimeOfDay.Evening, FieldWeather.Ashfall, 2, 90, "tonic", 1, 10),
            new FieldIncidentDefinition("frozen_margin", "얼어붙은 여백", "새벽 서리눈 아래에서 사라질 문장을 한 줄 건졌다.", FieldTimeOfDay.Dawn, FieldWeather.Snow, 3, 110, "silver_ink", 1, 12),
            new FieldIncidentDefinition("moonwind_footlights", "달바람 각광", "밤의 달바람이 동행 그림자의 잊힌 동선을 비췄다.", FieldTimeOfDay.Night, FieldWeather.MoonWind, 0, 70, "focus_elixir", 1, 14),
            new FieldIncidentDefinition("rain_script", "비에 젖은 대본", "새벽 잔향비에서 번진 대사를 복원했다.", FieldTimeOfDay.Dawn, FieldWeather.Rain, 0, 80, "ink", 2, 8),
            new FieldIncidentDefinition("thread_rehearsal", "실먼지 리허설", "해질녘 실먼지가 보이지 않는 인형극의 동선을 그렸다.", FieldTimeOfDay.Evening, FieldWeather.ThreadDust, 5, 130, "heart_winding_key", 1, 12),
            new FieldIncidentDefinition("blackout_prompt", "먹물 폭풍의 프롬프트", "밤의 먹물 폭풍 속에서 지워진 지시문을 되찾았다.", FieldTimeOfDay.Night, FieldWeather.InkStorm, 6, 150, "silver_ink", 2, 15),
            new FieldIncidentDefinition("starfall_encore", "별비의 앙코르", "별비가 그친 자리에 단 한 번의 앙코르 보상이 남았다.", FieldTimeOfDay.Night, FieldWeather.Starfall, 0, 180, "greater_potion", 1, 16),
            new FieldIncidentDefinition("mist_applause", "안개 속 박수", "기억 안개 속 홀로 남은 박수의 주인을 찾아냈다.", FieldTimeOfDay.Night, FieldWeather.Mist, 0, 65, "salve", 2, 7)
        };

        public static IReadOnlyList<FieldIncidentDefinition> All => Incidents;

        public static bool TryResolve(RegionData region, FieldWorldSnapshot world, out FieldIncidentDefinition incident)
        {
            incident = default;
            if (region == null || region.isSettlement || region.isBossStage || SaveManager.Current == null) return false;
            foreach (FieldIncidentDefinition candidate in Incidents)
            {
                if (!Matches(candidate, region, world)) continue;
                FieldIncidentRecordData record = Record(candidate.id, true);
                if (record.lastCompletedDay == world.dayIndex) continue;
                record.lastCompletedDay = world.dayIndex;
                record.completionCount = SaveValuePolicy.SaturatingAdd(record.completionCount, 1);
                if (candidate.gold > 0) SaveManager.AddGold(candidate.gold);
                if (!string.IsNullOrEmpty(candidate.itemId) && candidate.itemCount > 0)
                    SaveManager.AddItem(candidate.itemId, candidate.itemCount);
                ShadowTheater.Data.ShadowInstance companion = CompanionBondService.CurrentCompanion();
                if (companion != null)
                    CompanionBondService.AddExperience(companion.instanceId, candidate.bondExperience, out _, out _);
                incident = candidate;
                return true;
            }
            return false;
        }

        public static FieldIncidentRecordData Record(string id, bool create = false)
        {
            SaveData save = SaveManager.Current;
            if (save == null || string.IsNullOrWhiteSpace(id)) return null;
            save.fieldIncidentRecords ??= new List<FieldIncidentRecordData>();
            FieldIncidentRecordData record = save.fieldIncidentRecords.Find(x => x.incidentId == id);
            if (record == null && create)
            {
                record = new FieldIncidentRecordData { incidentId = id, lastCompletedDay = -1 };
                save.fieldIncidentRecords.Add(record);
            }
            return record;
        }

        public static bool Matches(FieldIncidentDefinition incident, RegionData region, FieldWorldSnapshot world) =>
            region != null && incident.time == world.timeOfDay && incident.weather == world.weather &&
            (incident.act == 0 || incident.act == region.act);
    }

    public class FieldIncidentController : MonoBehaviour
    {
        [SerializeField] private string regionId;
        private IEnumerator Start()
        {
            if (SaveManager.Current == null) yield return new WaitUntil(() => SaveManager.Current != null);
            yield return new WaitForSeconds(.6f);
            RegionData region = RegionRepository.Get(regionId);
            if (region == null) yield break;
            if (!FieldIncidentService.TryResolve(region, FieldWorldState.Current(region), out FieldIncidentDefinition incident)) yield break;
            SaveManager.Instance?.Save();
            string reward = $"금화 +{incident.gold}" + (string.IsNullOrEmpty(incident.itemId) ? string.Empty : $" · {incident.itemId} x{incident.itemCount}");
            SaveFeedbackController.Instance?.ShowMessage($"[{incident.title}] {incident.message}\n{reward}");
        }
    }
}
