#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShadowTheater.EditorTools
{
    /// <summary>Focused native file/UI checks; use only in a separate validation project.</summary>
    public static class BattleReviewPlayModeQa
    {
        private const string Key="ShadowTheater.BattleReviewQa.";
        private const string Folder="Docs/Generated/BattleReviewQa";
        private static readonly List<string> Notes=new List<string>();
        private static IEnumerator steps;
        private static double started;
        private static int errors;

        public static void RunForCi()
        {
            Notes.Clear();errors=0;steps=null;
            Directory.CreateDirectory(Folder);
            SessionState.SetString(Key+"Company",PlayerSettings.companyName);
            SessionState.SetString(Key+"Product",PlayerSettings.productName);
            PlayerSettings.companyName="ShadowTheaterReviewQA";
            PlayerSettings.productName="BattleReviewQA";
            SessionState.SetBool(Key+"Active",true);
            SessionState.SetBool(Key+"Finished",false);
            SessionState.SetInt(Key+"Exit",1);
            SessionState.SetFloat(Key+"Started",(float)EditorApplication.timeSinceStartup);
            EditorSceneManager.OpenScene("Assets/Scenes/Prologue/PrologueTheater.unity");
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Attach()
        {
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Capture;Application.logMessageReceived+=Capture;
        }
        private static void Capture(string message,string stack,LogType type)
        {
            if(!SessionState.GetBool(Key+"Active",false)||!Application.isPlaying||
                type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            errors++;Notes.Add("Runtime error: "+message+"\n"+stack);
        }
        private static void Tick()
        {
            if(!SessionState.GetBool(Key+"Active",false))return;
            if(SessionState.GetBool(Key+"Finished",false))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                PlayerSettings.companyName=SessionState.GetString(Key+"Company","DefaultCompany");
                PlayerSettings.productName=SessionState.GetString(Key+"Product","Shadow Theater");
                AssetDatabase.SaveAssets();SessionState.SetBool(Key+"Active",false);
                EditorApplication.Exit(SessionState.GetInt(Key+"Exit",1));return;
            }
            if(!Application.isPlaying)
            {
                if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Started",0)>120)
                {Notes.Add("Play Mode startup timed out");Finish(false);}
                return;
            }
            try
            {
                if(steps==null){started=EditorApplication.timeSinceStartup;steps=Scenarios();}
                Require(EditorApplication.timeSinceStartup-started<90,"Focused checks timed out");
                if(!steps.MoveNext())Finish(errors==0);
            }
            catch(Exception e){Notes.Add(e.ToString());Finish(false);}
        }
        private static void Finish(bool pass)
        {
            (steps as IDisposable)?.Dispose();steps=null;
            Notes.Add("Runtime errors="+errors);Notes.Add(pass?"PASS":"FAIL");
            File.WriteAllLines(Folder+"/WORKFLOWS.txt",Notes);
            SessionState.SetInt(Key+"Exit",pass?0:1);SessionState.SetBool(Key+"Finished",true);
            EditorApplication.ExitPlaymode();
        }
        private static IEnumerator Scenarios()
        {
            for(int i=0;i<20;i++)yield return null;
            Require(SaveManager.Instance!=null&&PlayerController.Instance!=null,"Field bootstrapped");
            var starter=ShadowDatabase.Instance.GetShadow("mage");Require(starter!=null,"Starter exists");
            var save=SaveManager.Instance.NewGame(starter,24);
            save.mapId="PrologueTheater";
            string path=Path.Combine(Application.persistentDataPath,"BattleReviews",save.saveGuid+".json");
            var entry=Entry("InProgress");Require(BattleReviewService.Begin(entry),"Pending persisted");
            string before=File.ReadAllText(path);entry.result="Victory";
            using(var blocked=new FileStream(path+".tmp",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
            {
                Require(!BattleReviewService.Complete(entry),"Locked file rejects completion");
                Require(BattleReviewService.Entries.Single().result=="InProgress"&&File.ReadAllText(path)==before,"Failed completion preserves cache and primary");
                Require(!string.IsNullOrEmpty(BattleReviewService.Status),"File failure reported");
            }
            Require(BattleReviewService.Complete(entry)&&string.IsNullOrEmpty(BattleReviewService.Status),"Same completion retries successfully");
            typeof(BattleReviewService).GetMethod("Reset",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            Require(BattleReviewService.Entries.Single().result=="Victory","Completion survives native JSON reload");
            var pending=Entry("InProgress");
            using(var blocked=new FileStream(path+".tmp",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
                Require(!BattleReviewService.Begin(pending)&&BattleReviewService.Entries.Count==1,"Failed begin leaves history unchanged");
            Require(BattleReviewService.Begin(pending),"Begin retries successfully");
            Notes.Add("Native file lock: failed begin/complete, unchanged cache/primary, retry and JSON reload PASS");

            save=SaveManager.Instance.NewGame(starter,24);save.mapId="PrologueTheater";
            var pause=FieldPauseMenuController.Instance;Require(pause!=null,"Pause menu exists");
            pause.Open();yield return null;
            var panel=Field<GameObject>(pause,"root");
            Click(panel.GetComponentsInChildren<Button>().Single(b=>b.name=="전투 복기"));
            yield return null;
            var view=Field<BattleReviewView>(pause,"_review");
            Require(view.IsOpen&&Field<Text>(view,"body").text.Contains("아직 전투 기록이 없습니다"),"Empty review opens by pointer click");
            Require(!Field<Button>(view,"previous").interactable&&!Field<Button>(view,"next").interactable,"Empty paging disabled");
            view.Close();pause.Close();

            string[] results={"Victory","Captured","Defeat","Escaped","Interrupted","InProgress"};
            int[] turns={4,8,12,16,1000,2000};
            for(int i=0;i<results.Length;i++)
            {
                var sample=Entry("InProgress");sample.turns=turns[i];
                if(results[i]=="Interrupted")sample.sessionId=Guid.NewGuid().ToString("N");
                Require(BattleReviewService.Begin(sample),"Seed pending "+results[i]);
                if(BattleReviewPolicy.IsFinished(results[i]))
                {sample.result=results[i];Require(BattleReviewService.Complete(sample),"Seed completed "+results[i]);}
            }
            typeof(BattleReviewService).GetMethod("Reset",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            // Reload marks both unfinished entries interrupted; add a new current-session entry.
            Require(BattleReviewService.Begin(Entry("InProgress")),"Seed current session");
            pause.Open();yield return null;
            Click(panel.GetComponentsInChildren<Button>().Single(b=>b.name=="전투 복기"));yield return null;
            var total=BattleReviewPolicy.Summarize(BattleReviewService.Entries);
            Require(total.Decided==3&&total.Completed==4&&total.AverageTurns==10&&total.interrupted==2&&total.inProgress==1,"Summary excludes unfinished turns and escaped win rate");
            var summary=Field<Text>(view,"summary");
            Require(summary.text.Contains("승리 1")&&summary.text.Contains("66.7%")&&summary.text.Contains("10턴"),"Summary rendered");
            Require(!Field<Button>(view,"next").interactable&&Field<Button>(view,"previous").interactable,"Latest entry selected");
            Click(Field<Button>(view,"previous"));yield return null;
            Require(Field<Button>(view,"next").interactable,"Previous pointer click changes entry");
            Click(Field<Button>(view,"next"));yield return null;
            var body=Field<Text>(view,"body");var scroll=body.GetComponentInParent<ScrollRect>();
            Canvas.ForceUpdateCanvases();
            Require(scroll.viewport.rect.height>0&&summary.rectTransform.rect.height>=summary.preferredHeight,"Review has usable viewport and unclipped summary");
            var reviewPanel=Field<GameObject>(view,"root");
            Click(reviewPanel.GetComponentsInChildren<Button>().Single(b=>b.name=="닫기"));
            Require(!view.IsOpen&&pause.IsOpen&&PlayerController.Instance.IsLocked,"Review close retains pause movement lock");
            view.Open();yield return null;
            pause.Close();Require(!view.IsOpen&&!PlayerController.Instance.IsLocked,"Pause close releases movement");
            Notes.Add("Empty/populated review, summary, pointer paging/close, viewport and movement locks PASS");
        }
        private static BattleReviewEntry Entry(string result)=>new BattleReviewEntry
        {
            id=Guid.NewGuid().ToString("N"),sessionId=BattleReviewService.SessionId,saveGuid=SaveManager.Current.saveGuid,
            startedUtc=DateTimeOffset.UtcNow.ToString("o"),regionId="prologue_theater",mode="Wild",result=result,
            players=new List<BattleReviewUnit>{Unit("mage")},enemies=new List<BattleReviewUnit>{Unit("knight")}
        };
        private static BattleReviewUnit Unit(string id)=>new BattleReviewUnit
        {shadowId=id,name=id,stage="Base",levelBefore=24,levelAfter=24,hpBefore=100,hpAfter=100,maxHpBefore=100,maxHpAfter=100};
        private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
        private static void Require(bool value,string label){if(!value)throw new InvalidOperationException(label);}
        private static void Click(Button button)
        {
            Require(button!=null&&button.IsInteractable(),"Button interactable");Canvas.ForceUpdateCanvases();
            var rect=(RectTransform)button.transform;
            var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Require(hits.Count>0&&(hits[0].gameObject==button.gameObject||hits[0].gameObject.transform.IsChildOf(button.transform)),"Top pointer hit: "+button.name);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        }
    }
}
#endif
