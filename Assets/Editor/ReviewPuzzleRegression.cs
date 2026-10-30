#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Battle;
using ShadowTheater.Field;
using ShadowTheater.Story;

namespace ShadowTheater.EditorTools
{
    public static class ReviewPuzzleRegression
    {
        public static void Validate(List<string> errors)
        {
            var ids=new HashSet<string>();var regions=new HashSet<string>();
            foreach(var puzzle in RegionalPuzzleCatalog.All)
            {
                if(!RegionalPuzzleState.Valid(puzzle)||!ids.Add(puzzle.id)||!regions.Add(puzzle.regionId)||RegionRepository.Get(puzzle.regionId)==null)
                {errors.Add("지역 퍼즐 카탈로그 오류");continue;}
                var state=new RegionalPuzzleState(puzzle);
                if(puzzle.kind=="Sequence")foreach(int node in puzzle.sequence)state.Activate(node);
                else if(puzzle.kind=="Mirrors")
                    for(int i=0;i<3;i++)for(int n=0;n<puzzle.rotations[i];n++)state.Activate(i);
                else
                    for(int mask=0;mask<8&&!state.Solved;mask++)
                    {state=new RegionalPuzzleState(puzzle);for(int i=0;i<3;i++)if((mask&(1<<i))!=0)state.Activate(i);}
                if(!state.Solved||!new RegionalPuzzleState(puzzle,true).Solved)errors.Add("지역 퍼즐 해결/복구 오류: "+puzzle.id);
            }
            if(ids.Count!=3)errors.Add("지역 퍼즐 3종 누락");
            string save=Guid.NewGuid().ToString("N");
            var unit=new BattleReviewUnit{shadowId="test",levelBefore=24,levelAfter=24,maxHpBefore=100,maxHpAfter=100,hpBefore=100,hpAfter=0};
            var entry=new BattleReviewEntry{id=Guid.NewGuid().ToString("N"),sessionId=Guid.NewGuid().ToString("N"),saveGuid=save,
                startedUtc=DateTimeOffset.UtcNow.ToString("o"),result="InProgress",players=new List<BattleReviewUnit>{unit},enemies=new List<BattleReviewUnit>{unit}};
            var log=new BattleReviewLog{version=BattleReviewPolicy.Version,saveGuid=save};
            if(!BattleReviewPolicy.Upsert(log,entry))errors.Add("전투 복기 시작 오류");
            var complete=UnityEngine.JsonUtility.FromJson<BattleReviewEntry>(UnityEngine.JsonUtility.ToJson(entry));complete.result="Defeat";
            if(!BattleReviewPolicy.Upsert(log,complete)||BattleReviewPolicy.Upsert(log,complete)||log.entries.Count!=1||log.entries[0].players[0].hpAfter!=0)
                errors.Add("전투 복기 완료/중복/패배 HP 오류");
            for(int i=0;i<30;i++)
            {var row=UnityEngine.JsonUtility.FromJson<BattleReviewEntry>(UnityEngine.JsonUtility.ToJson(entry));row.id=Guid.NewGuid().ToString("N");BattleReviewPolicy.Upsert(log,row);}
            BattleReviewPolicy.MarkInterrupted(log,Guid.NewGuid().ToString("N"));
            if(log.entries.Count!=BattleReviewPolicy.Capacity||log.entries.Any(e=>e.result=="InProgress")||!BattleReviewPolicy.Validate(log,save))
                errors.Add("전투 복기 개수/중단 복구 오류");
        }
    }
}
#endif
