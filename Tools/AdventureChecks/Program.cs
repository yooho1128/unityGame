using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ShadowTheater.Story;
using ShadowTheater.Save;
using ShadowTheater.Data;
using ShadowTheater.Field;
using UnityEngine;

class Program
{
    static int checks;
    static void Check(bool value,string label) { checks++;if(!value)throw new Exception(label); }
    static void KnownWorld()
    {
        SaveManager.Current=new SaveData{mapId="PrologueTheater"};SaveManager.Flags.Clear();
        foreach(var r in RegionRepository.All)
        {SaveManager.Current.visitedRegionIds.Add(r.regionId);if(!string.IsNullOrEmpty(r.requiredFlag))SaveManager.SetFlag(r.requiredFlag);}
    }
    static void Main(string[] args)
    {
        KnownWorld();
        Check(EvolutionGuidanceService.Conditions.Count==44,"every authored/legendary growth flag");
        foreach(var c in EvolutionGuidanceService.Conditions)
        {
            Check(c.routes.Length>0,"reachable growth condition "+c.flag);
            SaveManager.SetFlag(c.flag);Check(MemoryAwakeningService.RequirementMet(c.flag),"actual growth predicate "+c.flag);
            foreach(var route in c.routes)
            {
                string text=EvolutionGuidanceService.Describe(route);
                Check(!string.IsNullOrWhiteSpace(text)&&!text.Contains(c.flag),"human-readable route "+c.flag);
                var objective=EvolutionGuidanceService.Objective(route);
                Check(objective!=null,"actionable objective "+c.flag);
                if(route.kind!="Quest")Check(QuestGuidanceService.ForObjective(objective).destination!=null,"known destination "+c.flag);
            }
        }
        SaveManager.Flags.Clear();
        Check(!MemoryAwakeningService.RequirementMet("memory_knight_restored"),"no milestone yet");
        SaveManager.SetFlag("boss_moonlit_story_complete");
        Check(MemoryAwakeningService.RequirementMet("memory_knight_restored"),"story alias still satisfies growth");
        var knight=new ShadowInstance{level=5,Data=new ShadowData{displayName="기사",restoredForm=new MemoryFormData{enabled=true,requiredLevel=10,requiredFlag="memory_knight_restored"}}};
        Check(!MemoryAwakeningService.CanRestore(knight,out string level)&&level.Contains("10"),"required level unchanged");
        knight.level=10;Check(MemoryAwakeningService.CanRestore(knight,out _),"alias and level sufficient, no item requirement");
        SaveManager.Flags.Clear();QuestRepository.TryGet("memory_knight_vow",out var memoryQuest);
        Check(!MemoryAwakeningService.CanRestore(knight,out string hint)&&hint.Contains(memoryQuest.title),"actionable unmet memory hint");
        Check(EvolutionGuidanceService.Routes("memory_knight_restored").Count==2,"personal or story alternate route");
        knight.Data.salvationForm.enabled=true;
        Check(EvolutionGuidanceService.Checklist(knight,1).Contains("기억 복원 완료"),"prior growth requirement shown");
        var final=new ShadowInstance{memoryStage=MemoryStage.TrueName,Data=knight.Data};
        Check(!MemoryAwakeningService.CanAwaken(final,AwakeningPath.Grudge,out _),"cannot switch final branch");
        var secret=EvolutionGuidanceService.Conditions.SelectMany(c=>c.routes).First(r=>r.regionId!=null&&RegionRepository.Get(r.regionId).isHidden);
        SaveManager.Current=new SaveData{mapId="PrologueTheater"};
        Check(!EvolutionGuidanceService.Describe(secret).Contains(RegionRepository.Get(secret.regionId).displayName),"hidden evolution region not disclosed");
        Check(QuestGuidanceService.ForObjective(EvolutionGuidanceService.Objective(secret)).destination==null,"hidden map button has no target");
        Check(SaveManager.Current.quests.Count==0&&SaveManager.Flags.Count==0,"viewing guidance never starts quests or sets flags");

        var layouts=JsonUtility.FromJson<RegionLayoutCatalog>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data/RegionLayoutCatalog.json")));
        Check(layouts.entries.Length==63&&layouts.entries.Select(d=>d.profile).Distinct().Count()==9,"63 fields and 9 geography profiles");
        var signatures=new HashSet<string>();
        var previews=new List<object>();
        foreach(var definition in layouts.entries)
        {
            var plan=RegionLayoutPolicy.Build(definition);var again=RegionLayoutPolicy.Build(definition);
            Check(plan.ground.SequenceEqual(again.ground)&&plan.obstacles.SequenceEqual(again.obstacles),"deterministic layout "+definition.regionId);
            var reached=RegionLayoutPolicy.Reachable(plan);int open=0;
            for(int i=0;i<RegionLayoutPlan.Count;i++)
            {
                var cell=RegionLayoutPlan.Cell(i);
                if(RegionLayoutPlan.Interior(cell.x,cell.y))
                {if(!plan.Blocked(cell.x,cell.y)){open++;Check(reached.Contains(i),"no isolated floor "+definition.regionId);}}
                else Check(plan.Blocked(cell.x,cell.y),"closed boundary "+definition.regionId);
            }
            Check(open>100&&open<357,"walkable geography and obstacles "+definition.regionId);
            foreach(var a in definition.anchors)
            {
                Check(!plan.Blocked(a.x,a.y)&&reached.Contains(RegionLayoutPlan.Index(a.x,a.y)),"NPC/portal/landing accessible "+definition.regionId);
                for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                    if(RegionLayoutPlan.Interior(a.x+x,a.y+y))Check(!plan.Blocked(a.x+x,a.y+y),"can walk around solid NPC "+definition.regionId);
            }
            // Every interior old save cell can be kept safe and connected by migration.
            for(int y=-8;y<=8;y++)for(int x=-10;x<=10;x++)
            {
                if(!plan.Blocked(x,y))continue;
                var migrated=RegionLayoutPolicy.Build(definition,new[]{new LayoutCell(x,y)});
                Check(!migrated.Blocked(x,y)&&RegionLayoutPolicy.Reachable(migrated).Contains(RegionLayoutPlan.Index(x,y)),"old save position retained "+definition.regionId);
            }
            signatures.Add(string.Join(",",plan.ground)+"/"+string.Join(",",plan.obstacles));
            previews.Add(new{definition.regionId,definition.profile,definition.anchors,
                ground=plan.ground.Select(t=>(int)t).ToArray(),obstacles=plan.obstacles.Select(t=>t.HasValue?(int)t.Value:-1).ToArray()});
        }
        Check(signatures.Count>=25,"region layouts vary within and across themes");
        if(args.Length==1)File.WriteAllText(args[0],System.Text.Json.JsonSerializer.Serialize(previews,new System.Text.Json.JsonSerializerOptions{IncludeFields=true}));
        Console.WriteLine("PASS: "+checks+" production evolution/field checks; 44 flags, 50 routes, 63 fields/9 profiles, all anchors, connectivity, boundaries and old save cells (.NET native bridges simulated; not Unity)");
    }
}
