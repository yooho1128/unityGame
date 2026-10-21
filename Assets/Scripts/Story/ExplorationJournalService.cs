using System.Linq;
using ShadowTheater.Save;
using UnityEngine;

namespace ShadowTheater.Story
{
    public static class ExplorationJournalService
    {
        public static string Summary()
        {
            var regions=RegionRepository.All.Where(x=>x!=null).ToList();
            int landmarkTotal=regions.Count(x=>!x.isBossStage),landmarks=regions.Count(x=>!x.isBossStage&&SaveManager.HasFlag("landmark_discovered_"+x.regionId));
            int fieldTotal=regions.Count(x=>x.AllowsRandomEncounters),ecology=regions.Count(x=>x.AllowsRandomEncounters&&SaveManager.HasFlag("ecology_recorded_"+x.regionId));
            int puzzle=regions.Count(x=>x.AllowsRandomEncounters&&SaveManager.HasFlag("field_lever_"+x.regionId+"_complete"));
            int secrets=regions.Count(x=>(x.isDungeon||x.isHidden)&&SaveManager.HasFlag("secret_passage_"+x.regionId));
            int townTotal=regions.Count(x=>x.isSettlement),interiors=regions.Count(x=>x.isSettlement&&SaveManager.HasFlag("interior_visited_"+x.regionId));
            int affinity=regions.Where(x=>x.isSettlement).Sum(x=>Mathf.Max(0,SaveManager.GetFlag("resident_affinity_"+x.regionId)));
            return $"탐험 수첩 · 명소 {landmarks}/{landmarkTotal} · 생태 {ecology}/{fieldTotal} · 복구 {puzzle}/{fieldTotal} · 실내 {interiors}/{townTotal} · 비밀길 {secrets} · 주민 호감 {affinity}";
        }
    }
}
