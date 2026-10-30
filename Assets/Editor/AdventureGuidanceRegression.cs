#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Story;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    public static class AdventureGuidanceRegression
    {
        public static void Validate(List<string> errors)
        {
            var flags=new HashSet<string>(EvolutionGuidanceService.Conditions.Select(c=>c.flag));
            var database=ShadowDatabase.Instance;
            if(database!=null)foreach(var shadow in database.shadows)
            {
                if(shadow==null)continue;
                foreach(var form in new[]{shadow.restoredForm,shadow.salvationForm,shadow.grudgeForm})
                    if(form?.enabled==true&&!string.IsNullOrEmpty(form.requiredFlag)&&!flags.Contains(form.requiredFlag))
                        errors.Add("진화 안내 조건 누락: "+shadow.shadowId);
            }
            foreach(var condition in EvolutionGuidanceService.Conditions)
                if(condition.routes==null||condition.routes.Length==0)errors.Add("진화 안내 경로 없음: "+condition.flag);
            var asset=Resources.Load<TextAsset>("Data/RegionLayoutCatalog");
            var catalog=asset!=null?JsonUtility.FromJson<RegionLayoutCatalog>(asset.text):null;
            if(catalog?.entries==null){errors.Add("지역 구조 카탈로그 없음");return;}
            foreach(var definition in catalog.entries)
            {
                var plan=RegionLayoutPolicy.Build(definition);var reached=RegionLayoutPolicy.Reachable(plan);
                foreach(var cell in definition.anchors)
                    if(plan.Blocked(cell.x,cell.y)||!reached.Contains(RegionLayoutPlan.Index(cell.x,cell.y)))
                        errors.Add("지역 구조 목적지 막힘: "+definition.regionId);
                for(int y=-9;y<=9;y++)for(int x=-11;x<=11;x++)
                    if(!RegionLayoutPlan.Interior(x,y)&&!plan.Blocked(x,y))errors.Add("지역 구조 외곽 틈: "+definition.regionId);
            }
        }
    }
}
#endif
