using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using ShadowTheater.Battle;
using ShadowTheater.Field;
using ShadowTheater.Save;
using UnityEngine;

class Program
{
    static int checks;
    static string saveId=Guid.NewGuid().ToString("N");
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static T Clone<T>(T value)=>JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
    static BattleReviewEntry Entry(string result="InProgress")=>new BattleReviewEntry
    {
        id=Guid.NewGuid().ToString("N"),sessionId=BattleReviewService.SessionId,saveGuid=saveId,startedUtc=DateTimeOffset.UtcNow.ToString("o"),
        regionId="ember_catacombs",encounterId="boss_headless_guard",mode="Boss",result=result,goldBefore=100,goldAfter=100,
        players=new List<BattleReviewUnit>{Unit("knight")},enemies=new List<BattleReviewUnit>{Unit("headless_guard")}
    };
    static BattleReviewUnit Unit(string id)=>new BattleReviewUnit{shadowId=id,name=id,levelBefore=24,levelAfter=24,hpBefore=100,hpAfter=100,maxHpBefore=100,maxHpAfter=100};
    static void ResetService()=>typeof(BattleReviewService).GetMethod("Reset",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
    static string Signature(RegionalPuzzleState state)=>state.Solved?"solved":state.definition.kind=="Sequence"?"prefix"+state.Progress:
        state.definition.kind=="Toggle"?"mask"+state.Mask:string.Join("/",Enumerable.Range(0,3).Select(state.Status));
    static RegionalPuzzleState Replay(RegionalPuzzleDefinition d,IEnumerable<int> path)
    {var state=new RegionalPuzzleState(d);foreach(int node in path)state.Activate(node);return state;}
    static Dictionary<string,int[]> Reachable(RegionalPuzzleDefinition d)
    {
        var paths=new Dictionary<string,int[]>{{Signature(new RegionalPuzzleState(d)),Array.Empty<int>()}};
        var pending=new Queue<int[]>();pending.Enqueue(Array.Empty<int>());
        while(pending.Count>0)
        {
            var path=pending.Dequeue();
            for(int i=0;i<3;i++)
            {
                var next=path.Concat(new[]{i}).ToArray();var state=Replay(d,next);string key=Signature(state);
                if(paths.ContainsKey(key))continue;
                paths[key]=next;if(!state.Solved)pending.Enqueue(next);
            }
        }
        return paths;
    }
    static void Main()
    {
        Check(RegionalPuzzleCatalog.All.Count==3,"three unique regional puzzles");
        var layouts=JsonUtility.FromJson<RegionLayoutCatalog>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data/RegionLayoutCatalog.json")));
        foreach(var d in RegionalPuzzleCatalog.All)
        {
            Check(RegionalPuzzleState.Valid(d),"real puzzle schema "+d.id);
            var states=Reachable(d);Check(states.ContainsKey("solved"),"reachable goal "+d.id);
            var plan=RegionLayoutPolicy.Build(layouts.entries.First(l=>l.regionId==d.regionId));
            foreach(var cell in new[]{d.board}.Concat(d.nodes).Concat(d.exits))Check(!plan.Blocked(cell.x,cell.y),"authored board/node/shortcut floor "+d.id);
            var restored=new RegionalPuzzleState(d,true);Check(restored.Solved&&!restored.Activate(0),"saved completion is idempotent");
            foreach(var path in states.Values)
            {
                var current=Replay(d,path);string before=Signature(current);
                Check(!current.Activate(-1)&&!current.Activate(3)&&Signature(current)==before,"invalid node leaves state unchanged");
                if(current.Solved)continue;
                bool reachable=false;
                if(d.kind=="Sequence")
                {foreach(int node in d.sequence)current.Activate(node);reachable=current.Solved;}
                else if(d.kind=="Toggle")
                {
                    for(int mask=0;mask<8;mask++)
                    {var test=Replay(d,path);for(int i=0;i<3;i++)if((mask&(1<<i))!=0)test.Activate(i);reachable|=test.Solved;}
                }
                else
                {
                    string[] labels={"↑ 북","→ 동","↓ 남","← 서"};
                    for(int i=0;i<3;i++)for(int count=0;count<4&&!current.Solved&&current.Status(i)!=labels[d.rotations[i]];count++)current.Activate(i);
                    reachable=current.Solved;
                }
                Check(reachable,"every reachable partial state can still solve "+d.id);
            }
        }
        var repeated=Clone(RegionalPuzzleCatalog.All.First(d=>d.kind=="Sequence"));repeated.sequence=new[]{0,1,0,2};
        var prefix=Replay(repeated,new[]{0,1,0,1});Check(prefix.Progress==2,"overlapping sequence prefix preserved");
        prefix.Activate(0);prefix.Activate(2);Check(prefix.Solved,"overlap recovery reaches completion");
        var invalid=Clone(repeated);invalid.nodes=null;Check(!RegionalPuzzleState.Valid(invalid),"bad puzzle rejected");

        var log=new BattleReviewLog{version=BattleReviewPolicy.Version,saveGuid=saveId};
        var pendingEntry=Entry();Check(BattleReviewPolicy.Upsert(log,pendingEntry),"new pending review");
        var finished=Clone(pendingEntry);finished.result="Defeat";finished.turns=20;finished.players[0].hpAfter=0;
        Check(BattleReviewPolicy.Upsert(log,finished)&&log.entries.Count==1,"pending becomes one completed record");
        Check(!BattleReviewPolicy.Upsert(log,finished)&&log.entries.Count==1,"duplicate completion rejected");
        var foreign=Entry();foreign.saveGuid=Guid.NewGuid().ToString("N");Check(!BattleReviewPolicy.Upsert(log,foreign),"cross-save review rejected");
        var corrupt=Clone(finished);corrupt.players[0].healingReceived=-1;Check(!BattleReviewPolicy.IsValid(corrupt,saveId),"negative metrics rejected");
        corrupt=Clone(finished);corrupt.players[0].hpAfter=101;Check(!BattleReviewPolicy.IsValid(corrupt,saveId),"HP exceeds final max rejected");
        corrupt=Clone(finished);corrupt.startedUtc="not a date";Check(!BattleReviewPolicy.IsValid(corrupt,saveId),"bad timestamp rejected");
        var badLog=Clone(log);badLog.version=2;Check(!BattleReviewPolicy.Validate(badLog,saveId),"unknown schema rejected");
        badLog=Clone(log);badLog.entries.Add(Clone(finished));Check(!BattleReviewPolicy.Validate(badLog,saveId),"duplicate ids invalidate file");
        for(int i=0;i<30;i++)Check(BattleReviewPolicy.Upsert(log,Entry("Victory")),"append bounded history");
        Check(log.entries.Count==BattleReviewPolicy.Capacity&&BattleReviewPolicy.Validate(log,saveId),"bounded valid retention");
        var oldSession=Entry();log=new BattleReviewLog{version=1,saveGuid=saveId,entries=new List<BattleReviewEntry>{oldSession}};
        BattleReviewPolicy.MarkInterrupted(log,Guid.NewGuid().ToString("N"));
        Check(log.entries[0].result=="Interrupted"&&!BattleReviewPolicy.IsFinished(log.entries[0].result),"interruption is not a defeat or victory");
        Check(BattleReviewPolicy.Add(long.MaxValue-2,5)==long.MaxValue,"diagnostic total saturates safely");
        Check(BattleReviewPolicy.Experience(3,7,120,l=>20+5*l*l)==72,"actual cumulative XP boundary example");

        string directory=Path.Combine(Path.GetTempPath(),"ShadowReviewChecks_"+Guid.NewGuid().ToString("N"));
        Application.persistentDataPath=directory;SaveManager.Current=new SaveData{saveGuid=saveId,gold=123,hp=24};ResetService();
        try
        {
            var entry=Entry();Check(BattleReviewService.Begin(entry),"shipping file service writes pending");
            string path=Path.Combine(directory,"BattleReviews",saveId+".json");Check(File.Exists(path),"sidecar exists");
            entry.result="Defeat";entry.players[0].hpAfter=0;
            Check(BattleReviewService.Entries[0].result=="InProgress"&&BattleReviewService.Entries[0].players[0].hpAfter==100,"producer mutation cannot corrupt pending snapshot");
            Check(BattleReviewService.Complete(entry)&&File.Exists(path+".bak"),"shipping service completes and backs up");
            string bytes=File.ReadAllText(path);Check(!BattleReviewService.Complete(entry)&&File.ReadAllText(path)==bytes,"duplicate complete does not rewrite history");
            string other=Guid.NewGuid().ToString("N");SaveManager.Current=new SaveData{saveGuid=other};Check(BattleReviewService.Entries.Count==0,"second save isolated");
            SaveManager.Current=new SaveData{saveGuid=saveId,gold=123,hp=24};Check(BattleReviewService.Entries.Count==1,"original save history restored");
            var interrupted=Entry();Check(BattleReviewService.Begin(interrupted),"second pending written");ResetService();
            Check(BattleReviewService.Entries.Any(e=>e.result=="Interrupted"),"app restart marks unfinished review interrupted");
            File.WriteAllText(path,"{broken");ResetService();Check(BattleReviewService.Entries.Count==1,"valid backup recovers corrupt primary");
            Check(BattleReviewService.Begin(Entry())&&Directory.GetFiles(Path.GetDirectoryName(path),"*.invalid_*").Length==1,"corrupt primary retained before next write");
            SaveManager.Current.saveGuid="../../outside";Check(BattleReviewService.Entries.Count==0&&!BattleReviewService.Begin(Entry()),"unsafe guid cannot select a path");
            string blocker=Path.Combine(directory,"blocker");File.WriteAllText(blocker,"occupied");Application.persistentDataPath=blocker;
            SaveManager.Current=new SaveData{saveGuid=saveId,gold=123,hp=24};ResetService();
            Check(!BattleReviewService.Begin(Entry())&&!string.IsNullOrEmpty(BattleReviewService.Status),"I/O failure is contained");
            Check(SaveManager.Current.gold==123&&SaveManager.Current.hp==24,"diagnostics never alter gameplay save state");
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("PASS: "+checks+" production review/file/puzzle checks: all puzzle state spaces recoverable, overlap sequence, immutable snapshots, retention, slot isolation, backup recovery, interrupted sessions and I/O failure (.NET Unity bridges simulated; not Unity Play Mode)");
    }
}
