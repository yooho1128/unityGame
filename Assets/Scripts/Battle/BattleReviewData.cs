using System;
using System.Collections.Generic;
using System.Linq;

namespace ShadowTheater.Battle
{
    [Serializable] public sealed class BattleReviewUnit
    {
        public string instanceId,shadowId,name,stage;
        public int levelBefore,levelAfter,hpBefore,hpAfter,maxHpBefore,maxHpAfter,attack,defense,speed,healingSkillUses;
        public long damageTaken,healingReceived,xpBefore,xpAfter;
    }
    [Serializable] public sealed class BattleReviewItem
    {public string itemId,name;public int before,after;}
    [Serializable] public sealed class BattleReviewEntry
    {
        public string id,sessionId,saveGuid,buildGuid,gameVersion,startedUtc,regionId,encounterId,mode,result;
        public int turns,playerFaints,declaredRewardGold,declaredExperience;
        public long goldBefore,goldAfter;
        public List<BattleReviewUnit> players=new List<BattleReviewUnit>(),enemies=new List<BattleReviewUnit>();
        public List<BattleReviewItem> items=new List<BattleReviewItem>();
    }
    [Serializable] public sealed class BattleReviewLog
    {public int version;public string saveGuid;public List<BattleReviewEntry> entries=new List<BattleReviewEntry>();}
    public static class BattleReviewPolicy
    {
        public const int Version=1,Capacity=20;
        public static bool IsFinished(string result)=>result=="Victory"||result=="Defeat"||result=="Captured"||result=="Escaped";
        public static bool IsValid(BattleReviewEntry entry,string saveGuid)
        {
            if(entry==null||!Guid.TryParse(saveGuid,out _)||entry.saveGuid!=saveGuid||!Guid.TryParse(entry.id,out _)||!Guid.TryParse(entry.sessionId,out _)||
                !DateTimeOffset.TryParse(entry.startedUtc,out _)||entry.players==null||entry.enemies==null||entry.items==null||
                entry.players.Count==0||entry.enemies.Count==0||entry.turns<0||entry.playerFaints<0||
                entry.goldBefore<0||entry.goldAfter<0||entry.declaredRewardGold<0||entry.declaredExperience<0||
                !(IsFinished(entry.result)||entry.result=="InProgress"||entry.result=="Interrupted"))return false;
            foreach(var unit in entry.players.Concat(entry.enemies))
                if(unit==null||string.IsNullOrEmpty(unit.shadowId)||unit.levelBefore<1||unit.levelAfter<1||
                    unit.hpBefore<0||unit.hpAfter<0||unit.maxHpBefore<1||unit.maxHpAfter<1||unit.hpBefore>unit.maxHpBefore||unit.hpAfter>unit.maxHpAfter||
                    unit.damageTaken<0||unit.healingReceived<0||unit.healingSkillUses<0||unit.xpBefore<0||unit.xpAfter<0)return false;
            return entry.items.All(i=>i!=null&&!string.IsNullOrEmpty(i.itemId)&&i.before>=0&&i.after>=0);
        }
        public static bool Validate(BattleReviewLog log,string saveGuid)=>Guid.TryParse(saveGuid,out _)&&log!=null&&log.version==Version&&
            log.saveGuid==saveGuid&&log.entries!=null&&log.entries.Count<=Capacity&&
            log.entries.All(e=>IsValid(e,saveGuid))&&log.entries.Select(e=>e.id).Distinct().Count()==log.entries.Count;
        public static bool Upsert(BattleReviewLog log,BattleReviewEntry entry)
        {
            if(!Validate(log,log?.saveGuid)||!IsValid(entry,log.saveGuid))return false;
            int index=log.entries.FindIndex(e=>e.id==entry.id);
            if(index>=0)
            {
                if(IsFinished(log.entries[index].result)||log.entries[index].result=="Interrupted")return false;
                log.entries[index]=entry;
            }
            else log.entries.Add(entry);
            while(log.entries.Count>Capacity)log.entries.RemoveAt(0);
            return true;
        }
        public static void MarkInterrupted(BattleReviewLog log,string currentSession)
        {
            if(log?.entries==null)return;
            foreach(var entry in log.entries)
                if(entry.result=="InProgress"&&entry.sessionId!=currentSession)entry.result="Interrupted";
        }
        public static long Add(long current,long value)=>value<=0?current:current>long.MaxValue-value?long.MaxValue:current+value;
        public static long Experience(int level,int remainder,int maxLevel,Func<int,int> next)
        {
            long value=Math.Max(0,remainder);
            for(int l=1;l<Math.Min(Math.Max(1,level),maxLevel);l++)value=Add(value,Math.Max(0,next(l)));
            return value;
        }
        public static string ResultName(string result)=>result=="Victory"?"승리":result=="Defeat"?"패배":
            result=="Captured"?"각본 기록":result=="Escaped"?"도주":result=="Interrupted"?"중단":"진행 중";
    }
}
