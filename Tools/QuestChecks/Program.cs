using System;
using System.Linq;
using System.Collections.Generic;
using ShadowTheater.Story;
using ShadowTheater.Save;
using ShadowTheater.Field;
using UnityEngine;
class Program
{
    static int checks;
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static QuestProgressData Progress(QuestDefinition q)=>new QuestProgressData {questId=q.questId};
    static void KnownWorld(string regionId)
    {
        SaveManager.Current=new SaveData{mapId=RegionRepository.Get(regionId).sceneName};
        foreach(var r in RegionRepository.All){SaveManager.Current.visitedRegionIds.Add(r.regionId);if(!string.IsNullOrEmpty(r.requiredFlag))SaveManager.Flags.Add(r.requiredFlag);}
        UnityEngine.Object.Scene.Clear();
    }
    static void Main()
    {
        var side=new QuestDefinition{questId="side",isSideQuest=true};var main=new QuestDefinition{questId="main"};
        var definitions=new[]{side,main};var progress=new List<QuestProgressData>{Progress(side),Progress(main)};
        Check(QuestTrackingPolicy.Resolve(definitions,progress,null)==main,"main first");
        Check(QuestTrackingPolicy.Resolve(definitions,progress,"side")==side,"explicit side priority");
        Check(QuestTrackingPolicy.Resolve(definitions,progress,"missing")==main,"invalid pin fallback");
        progress[0].completed=true;Check(QuestTrackingPolicy.Resolve(definitions,progress,"side")==main,"completed pin fallback");
        progress[1].completed=true;Check(QuestTrackingPolicy.Resolve(definitions,progress,"side")==null,"all complete");
        Check(QuestTrackingPolicy.Active(definitions,progress,"side")==null,"cannot pin completed");
        Check(QuestTrackingPolicy.Resolve(null,null,null)==null,"no save");
        progress[0].completed=false;Check(QuestTrackingPolicy.Resolve(definitions,progress,null)==side,"side only");
        Check(QuestTrackingPolicy.Resolve(definitions,new[]{Progress(main)},"side")==main,"pin from another slot rejected");
        main.objectives=new List<QuestObjectiveDefinition>{new QuestObjectiveDefinition{objectiveId="a",requiredCount=2},new QuestObjectiveDefinition{objectiveId="b"}};
        var p=Progress(main);p.objectives.Add(new QuestObjectiveProgressData{objectiveId="a",current=1});
        Check(QuestTrackingPolicy.NextObjective(main,p).objectiveId=="a","partial count remains");
        p.objectives[0].current=2;Check(QuestTrackingPolicy.NextObjective(main,p).objectiveId=="b","skip completed objective");
        p.completed=true;Check(QuestTrackingPolicy.NextObjective(main,p)==null,"no completed guidance");
        Check(QuestTrackingPolicy.NextObjective(side,p)==null,"mismatched progress rejected");
        var keys=new HashSet<string>();foreach(var t in QuestGuidanceService.Targets)Check(keys.Add(t.type+"/"+t.targetId),"unique target");
        Check(QuestRepository.All.Count==54&&keys.Count==69,"actual source coverage");
        foreach(var q in QuestRepository.All)
        {
            var first=q.objectives[0];var target=QuestGuidanceService.Targets.FirstOrDefault(t=>t.type==first.type&&t.targetId==first.targetId);
            KnownWorld(target?.regionId??"moonlit_meadow");SaveManager.Current.quests.Add(Progress(q));
            var g=QuestGuidanceService.For(q);Check(g.objective==first&&g.destination!=null,"real first goal "+q.questId);
            if(first.targetId!="*")Check(g.waypoint.HasValue,"local target point "+q.questId);
            foreach(var o in q.objectives)if(o.targetId!="*")Check(keys.Contains(o.type+"/"+o.targetId),"mapped objective "+q.questId);
        }
        var talk=QuestRepository.All.First(q=>q.objectives[0].targetId=="npc_ash_priest");
        KnownWorld("prologue_theater");SaveManager.Current.quests.Add(Progress(talk));
        var dest=RegionRepository.Get("ember_catacombs");var route=RegionProgress.FindKnownRoute(dest);
        var exit=new MapPortal{DestinationScene=route[1].sceneName,CanGuideTravel=true};exit.transform.position=new Vector3(0,8,0);UnityEngine.Object.Scene.Add(exit);
        var travel=QuestGuidanceService.For(talk);Check(travel.waypoint.HasValue&&travel.text.Contains("북쪽"),"correct live exit direction");
        exit.CanGuideTravel=false;travel=QuestGuidanceService.For(talk);Check(!travel.waypoint.HasValue&&travel.text.Contains("잠겨"),"closed edge not guided");
        KnownWorld("ember_catacombs");SaveManager.Current.quests.Add(Progress(talk));
        var npc=new StoryNpc{NpcId="npc_ash_priest"};npc.transform.position=new Vector3(9,1,0);UnityEngine.Object.Scene.Add(npc);
        Check(QuestGuidanceService.For(talk).waypoint.Value.x==9,"use live NPC instead of stale spawn");
        var hidden=QuestRepository.All.First(q=>q.objectives[0].targetId=="area_forgotten_tent");
        SaveManager.Current=new SaveData{mapId="PrologueTheater"};SaveManager.Current.quests.Add(Progress(hidden));
        SaveManager.Current.unlockedRegionIds.Add("forgotten_tent");TownServicePolicy.ShowRumor=false;
        var secret=QuestGuidanceService.For(hidden);Check(secret.destination==null&&!secret.text.Contains(RegionRepository.Get("forgotten_tent").displayName),"undiscovered hidden location stays hidden");
        TownServicePolicy.ShowRumor=true;Check(QuestGuidanceService.For(hidden).destination==null,"rumor does not disclose hidden coordinates");
        SaveManager.Current=null;Check(QuestGuidanceService.For(talk).destination==null,"no save safe");
        Console.WriteLine("PASS: "+checks+" production quest tracking/guidance checks, 54 quests/69 targets, live NPC/exit, blocked edge and hidden-place privacy (.NET native bridges simulated; not Unity)");
    }
}
