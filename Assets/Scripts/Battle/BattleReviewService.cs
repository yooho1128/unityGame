using System;
using System.Collections.Generic;
using System.IO;
using ShadowTheater.Save;
using UnityEngine;

namespace ShadowTheater.Battle
{
    /// <summary>Local sidecar only. Failure never changes or prevents saving the game itself.</summary>
    public static class BattleReviewService
    {
        private static string session=Guid.NewGuid().ToString("N"),key;
        private static BattleReviewLog log;
        public static string SessionId=>session;
        public static string Status {get;private set;}="";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){session=Guid.NewGuid().ToString("N");key=null;log=null;Status="";}
        public static string SaveKey(string value)=>Guid.TryParse(value,out var guid)?guid.ToString("N"):null;
        private static string PathFor(string id)=>Path.Combine(Application.persistentDataPath,"BattleReviews",id+".json");
        public static IReadOnlyList<BattleReviewEntry> Entries=>Ensure()?.entries??(IReadOnlyList<BattleReviewEntry>)Array.Empty<BattleReviewEntry>();
        private static BattleReviewLog Ensure()
        {
            string current=SaveKey(SaveManager.Current?.saveGuid);
            if(current==null)return null;
            if(log!=null&&key==current)return log;
            key=current;Status="";string path=PathFor(key);
            log=Read(path,key)??Read(path+".tmp",key)??Read(path+".bak",key)??new BattleReviewLog{version=BattleReviewPolicy.Version,saveGuid=key};
            BattleReviewPolicy.MarkInterrupted(log,session);return log;
        }
        private static BattleReviewLog Read(string path,string expected)
        {
            try
            {
                if(!File.Exists(path))return null;
                var value=JsonUtility.FromJson<BattleReviewLog>(File.ReadAllText(path));
                if(BattleReviewPolicy.Validate(value,expected))return value;
                Status="손상되었거나 다른 저장의 전투 기록을 제외했습니다.";
            }
            catch(Exception e){Status="전투 기록을 읽지 못했습니다.";Debug.LogWarning("[BattleReview] "+e.Message);}
            return null;
        }
        public static bool Begin(BattleReviewEntry entry)=>entry?.result=="InProgress"&&Store(entry);
        public static bool Complete(BattleReviewEntry entry)=>BattleReviewPolicy.IsFinished(entry?.result)&&Store(entry);
        private static bool Store(BattleReviewEntry entry)
        {
            try
            {
                var current=Ensure();if(current==null||entry==null||SaveKey(entry.saveGuid)!=key)return false;
                // The combat producer continues updating its DTO. Keep the pending history immutable.
                var copy=JsonUtility.FromJson<BattleReviewEntry>(JsonUtility.ToJson(entry));copy.saveGuid=key;
                if(!BattleReviewPolicy.Upsert(current,copy))return false;
                string path=PathFor(key);Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path+".tmp",JsonUtility.ToJson(current,true));
                if(File.Exists(path))
                {
                    if(Read(path,key)!=null)
                    {if(File.Exists(path+".bak"))File.Delete(path+".bak");File.Move(path,path+".bak");}
                    else File.Move(path,path+".invalid_"+Guid.NewGuid().ToString("N"));
                }
                File.Move(path+".tmp",path);Status="";return true;
            }
            catch(Exception e){Status="전투 기록 파일을 저장하지 못했습니다. 게임 진행은 유지됩니다.";Debug.LogWarning("[BattleReview] "+e.Message);return false;}
        }
        public static string ExportJson()
        {var current=Ensure();return current!=null?JsonUtility.ToJson(current,true):"";}
    }
}
