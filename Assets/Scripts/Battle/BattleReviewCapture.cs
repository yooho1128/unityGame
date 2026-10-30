using System;
using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Data;
using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;

namespace ShadowTheater.Battle
{
    public static class BattleReviewCapture
    {
        public static BattleReviewEntry Begin(BattleContext context,IReadOnlyList<BattleUnit> players,IReadOnlyList<BattleUnit> enemies)
        {
            var entry=new BattleReviewEntry{id=Guid.NewGuid().ToString("N"),sessionId=BattleReviewService.SessionId,
                saveGuid=BattleReviewService.SaveKey(SaveManager.Current?.saveGuid),buildGuid=Application.buildGUID,gameVersion=Application.version,
                startedUtc=DateTimeOffset.UtcNow.ToString("o"),regionId=context.regionId,encounterId=context.encounterId,
                mode=context.mode.ToString(),result="InProgress",goldBefore=SaveManager.Current?.gold??0,goldAfter=SaveManager.Current?.gold??0};
            foreach(var unit in players)entry.players.Add(Snapshot(unit));
            foreach(var unit in enemies)entry.enemies.Add(Snapshot(unit));
            if(context.inventory!=null)foreach(var item in context.inventory)
                if(item.Key!=null)entry.items.Add(new BattleReviewItem{itemId=item.Key.itemId,name=item.Key.displayName,before=Math.Max(0,item.Value),after=Math.Max(0,item.Value)});
            return entry;
        }
        private static BattleReviewUnit Snapshot(BattleUnit unit)=>new BattleReviewUnit
        {
            instanceId=unit.Instance.instanceId,shadowId=unit.Instance.shadowId,name=unit.Name,stage=MemoryAwakeningService.StageLabel(unit.Instance),
            levelBefore=unit.Instance.level,levelAfter=unit.Instance.level,hpBefore=unit.Hp,hpAfter=unit.Hp,
            maxHpBefore=unit.MaxHp,maxHpAfter=unit.MaxHp,attack=unit.Atk,defense=unit.Def,speed=unit.Spd,
            xpBefore=TotalXp(unit.Instance),xpAfter=TotalXp(unit.Instance)
        };
        private static long TotalXp(ShadowInstance instance)=>BattleReviewPolicy.Experience(instance.level,instance.exp,
            ShadowInstance.MaxLevel,ShadowInstance.ExperienceToNext);
        public static void EndCombat(BattleOutcome outcome,int turn,int playerFaints,IReadOnlyList<BattleUnit> players,IReadOnlyList<BattleUnit> enemies)
        {
            var entry=outcome.review;if(entry==null)return;
            entry.result=outcome.result.ToString();entry.turns=Math.Max(0,turn);entry.playerFaints=Math.Max(0,playerFaints);
            entry.declaredRewardGold=outcome.goldGained;entry.declaredExperience=outcome.expGained;
            FinishUnits(entry.players,players);FinishUnits(entry.enemies,enemies);
        }
        private static void FinishUnits(List<BattleReviewUnit> rows,IReadOnlyList<BattleUnit> units)
        {
            for(int i=0;i<Math.Min(rows.Count,units.Count);i++)
            {
                var row=rows[i];var unit=units[i];row.levelAfter=unit.Instance.level;row.hpAfter=unit.Hp;row.maxHpAfter=unit.MaxHp;
                row.damageTaken=unit.ReviewDamageTaken;row.healingReceived=unit.ReviewHealingReceived;row.healingSkillUses=unit.HealingSkills.Used;
                row.xpAfter=TotalXp(unit.Instance);
            }
        }
        public static void AfterSettlement(BattleContext context,BattleOutcome outcome)
        {
            var entry=outcome?.review;if(entry==null)return;
            entry.goldAfter=SaveManager.Current?.gold??entry.goldBefore;
            foreach(var item in entry.items)
                item.after=context.inventory?.Where(p=>p.Key!=null&&p.Key.itemId==item.itemId).Select(p=>Math.Max(0,p.Value)).FirstOrDefault()??item.before;
            BattleReviewService.Complete(entry);
        }
    }
}
