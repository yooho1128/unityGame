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
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShadowTheater.EditorTools
{
    /// <summary>Focused native file/UI checks; use only in a separate validation project.</summary>
    public static class AudienceChoicePlayModeQa
    {
        private const string Key="ShadowTheater.AudienceChoiceQa.";
        private const string Folder="Docs/Generated/AudienceChoiceQa";
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
            PlayerSettings.companyName="ShadowTheaterAudienceQA";
            PlayerSettings.productName="AudienceChoiceQA";
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
                Require(EditorApplication.timeSinceStartup-started<120,"Focused checks timed out");
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
            var starter=ShadowDatabase.Instance.GetShadow("mage");
            var enemy=ShadowDatabase.Instance.GetShadow("knight");
            var save=SaveManager.Instance.NewGame(starter,24);save.battleSpeed=4;save.mapId="PrologueTheater";
            SaveManager.SetFlag("tutorial_battle_complete");
            var flow=GameFlowController.Instance;
            while(!flow.CanStartEncounter)yield return null;
            flow.StartRandomBattle(new List<ShadowInstance>{new ShadowInstance(enemy,24)},RegionRepository.Get("moonlit_meadow"));
            while(BattleManager.Instance==null||!BattleManager.Instance.CanChooseAction)yield return null;
            float readyAt=Time.unscaledTime+.25f;
            while(Time.unscaledTime<readyAt)yield return null;
            var manager=BattleManager.Instance;var ui=manager.GetComponent<BattleUIController>();
            manager.SetSpeed(4);
            var board=Field<AudienceRequestBoard>(ui,"_requests");
            var hud=Field<GameObject>(board,"hud");
            Notes.Add("HUD active="+hud.activeInHierarchy+" parent="+hud.transform.parent.name+" rect="+((RectTransform)hud.transform).rect+
                " choose="+Field<Button>(board,"choose").gameObject.activeInHierarchy+" canvas="+hud.GetComponentInParent<Canvas>(true).renderMode);
            for(Transform parent=hud.transform;parent!=null;parent=parent.parent)
                if(parent is RectTransform rect)Notes.Add(parent.name+" rect="+rect.rect+" scale="+parent.lossyScale+" pos="+parent.position);
            Require(board!=null&&manager.AudienceChoices.Count==3&&manager.CanChooseAudienceRequest,"Three first-turn offers available");
            var offers=manager.AudienceChoices.ToArray();var original=manager.Audience.Type;
            Require(offers.Distinct().Count()==3&&!manager.TryChooseAudienceRequest(AudienceRequestType.None),"Invalid choice rejected without consuming selection");
            int hp=manager.PlayerActive.Hp,fp=manager.GetFp(BattleSide.Player),gold=save.gold;
            Click(Field<Button>(board,"choose"));yield return null;
            Require(board.IsOpen,"HUD pointer opens request draft");
            Capture(ui.gameObject,board,new Vector2Int(1920,1080),"draft-wide.png");
            Capture(ui.gameObject,board,new Vector2Int(1280,960),"draft-4x3.png");
            board.Close();board.Open();yield return null;
            Require(offers.SequenceEqual(manager.AudienceChoices)&&manager.Audience.Type==original,"Closing and reopening neither rerolls nor changes request");
            var cards=Field<Button[]>(board,"cards");
            Click(cards[1]);yield return null;
            Require(!board.IsOpen&&manager.Audience.Type==offers[1]&&!manager.CanChooseAudienceRequest,"Pointer choice commits requested objective exactly once");
            Require(!manager.TryChooseAudienceRequest(offers[2]),"Repeated selection rejected");
            Require(manager.Turn==1&&manager.PlayerActive.Hp==hp&&manager.GetFp(BattleSide.Player)==fp&&save.gold==gold,"Choosing changes no turn, HP, FP or gold");
            Capture(ui.gameObject,board,new Vector2Int(1920,1080),"battle-ticket.png");
            Notes.Add("Three offers, pointer open/select, stable cancel/reopen, invalid/repeated selection and unchanged resources PASS");
            Require(manager.SubmitAction(BattleAction.Attack(BattleSide.Player,manager.PlayerActive.BasicAttack)),"First action submitted");
            Require(!manager.CanChooseAudienceRequest&&!manager.TryChooseAudienceRequest(offers[0]),"Queued action immediately locks request");
            while(!manager.CanChooseAction&&!manager.IsEnded)yield return null;
            Require(!manager.IsEnded&&manager.Turn==2&&!manager.CanChooseAudienceRequest,"Second turn cannot reopen draft");
            Require(!Field<Button>(board,"choose").gameObject.activeSelf,"Selection control hides after first action");

            // End this bounded input fixture quickly; this is not a balance simulation.
            manager.EnemyActive.Instance.currentHp=1;
            for(int tries=0;tries<4&&!manager.IsEnded;tries++)
            {
                Require(manager.SubmitAction(BattleAction.Attack(BattleSide.Player,manager.PlayerActive.BasicAttack)),"Fixture finishing action");
                while(!manager.CanChooseAction&&!manager.IsEnded)yield return null;
            }
            Require(manager.IsEnded,"Bounded fixture ends");
            var result=Field<GameObject>(ui,"resultRoot");
            while(!result.activeSelf)yield return null;
            while(Field<CanvasGroup>(ui,"resultGroup").alpha<.99f)yield return null;
            Require(Field<Text>(ui,"resultDetailsText").text.Contains(AudienceRequest.TitleOf(offers[1])),"Result identifies chosen objective");
            Require(result.transform.Find("Card/PerformanceSeal")!=null,"Result performance seal exists");
            Capture(ui.gameObject,board,new Vector2Int(1920,1080),"result-wide.png");
            Capture(ui.gameObject,board,new Vector2Int(1280,960),"result-4x3.png");
            Click(result.transform.Find("Card/Continue").GetComponent<Button>());
            while(flow.IsInBattle)yield return null;
            Require(!PlayerController.Instance.IsLocked,"Result click returns to field and releases movement");
            Notes.Add("Queued/second-turn lock, chosen result, actual result-continue click and field unlock PASS");

            save=SaveManager.Instance.NewGame(starter,24);save.battleSpeed=4;save.mapId="PrologueTheater";
            while(!flow.CanStartEncounter)yield return null;
            flow.StartRandomBattle(new List<ShadowInstance>{new ShadowInstance(enemy,24)},RegionRepository.Get("moonlit_meadow"));
            while(!manager.CanChooseAction||manager.Turn!=1)yield return null;
            Require(manager.AudienceChoices.Count==0&&!manager.Audience.IsActive&&!manager.CanChooseAudienceRequest,"New battle resets offers; tutorial battle keeps new rules hidden");
            Require(!Field<GameObject>(board,"hud").activeSelf&&!board.IsOpen,"Tutorial has no extra request panel");
            Notes.Add("New battle reset and first-battle tutorial gating PASS");
        }
        private static void Capture(GameObject root,AudienceRequestBoard board,Vector2Int size,string name)
        {
            var canvas=root.GetComponent<Canvas>();var scaler=root.GetComponent<CanvasScaler>();
            var mode=canvas.renderMode;var previousCamera=canvas.worldCamera;float scale=canvas.scaleFactor,distance=canvas.planeDistance;
            bool scaling=scaler!=null&&scaler.enabled;
            var safe=root.transform.Find("SafeArea") as RectTransform;
            var safeComponent=safe?.GetComponent<MobileSafeArea>();bool safeEnabled=safeComponent!=null&&safeComponent.enabled;
            Vector2 min=safe!=null?safe.anchorMin:Vector2.zero,max=safe!=null?safe.anchorMax:Vector2.one;
            var go=new GameObject("AudienceQaCamera",typeof(Camera));var camera=go.GetComponent<Camera>();
            var render=new RenderTexture(size.x,size.y,24,RenderTextureFormat.ARGB32);render.Create();
            var active=RenderTexture.active;
            try
            {
                if(scaler!=null)scaler.enabled=false;
                if(safeComponent!=null)safeComponent.enabled=false;
                if(safe!=null){safe.anchorMin=Vector2.zero;safe.anchorMax=Vector2.one;}
                camera.orthographic=true;camera.transform.position=new Vector3(0,0,-100);
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=TheatreUiStyle.Midnight;camera.targetTexture=render;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;canvas.scaleFactor=size.y/1080f;
                Canvas.ForceUpdateCanvases();board.Layout();Canvas.ForceUpdateCanvases();camera.Render();
                RenderTexture.active=render;
                var texture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,size.x,size.y),0,0);texture.Apply();
                File.WriteAllBytes(Folder+"/"+name,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
                var hud=Field<GameObject>(board,"hud");
                if(hud.activeInHierarchy)
                {
                    var ui=root.GetComponent<BattleUIController>();
                    Require(!Bounds((RectTransform)hud.transform).Overlaps(Bounds((RectTransform)Field<BattleUnitPanel>(ui,"enemyPanel").transform)),"Ticket does not cover enemy information at "+size);
                    Require(!Bounds((RectTransform)hud.transform).Overlaps(Bounds((RectTransform)Field<BattleUnitPanel>(ui,"playerPanel").transform)),"Ticket does not cover player information at "+size);
                }
                if(board.IsOpen)
                {
                    var rects=Field<RectTransform[]>(board,"cardRects");
                    for(int i=1;i<rects.Length;i++)
                    {
                        var a=Bounds(rects[i-1]);var b=Bounds(rects[i]);
                        Require(!a.Overlaps(b),"Draft cards do not overlap at "+size);
                    }
                }
                Notes.Add("Rendered "+name+" "+size);
            }
            finally
            {
                RenderTexture.active=active;canvas.renderMode=mode;canvas.worldCamera=previousCamera;canvas.planeDistance=distance;canvas.scaleFactor=scale;
                if(scaler!=null)scaler.enabled=scaling;
                if(safe!=null){safe.anchorMin=min;safe.anchorMax=max;}
                if(safeComponent!=null)safeComponent.enabled=safeEnabled;
                camera.targetTexture=null;render.Release();UnityEngine.Object.DestroyImmediate(render);UnityEngine.Object.DestroyImmediate(go);
                Canvas.ForceUpdateCanvases();board.Layout();Canvas.ForceUpdateCanvases();
            }
        }
        private static Rect Bounds(RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            return new Rect(corners[0],corners[2]-corners[0]);
        }
        private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
        private static void Require(bool value,string label){if(!value)throw new InvalidOperationException(label);}
        private static void Click(Button button)
        {
            Require(button!=null&&button.IsInteractable(),"Button interactable");Canvas.ForceUpdateCanvases();
            var rect=(RectTransform)button.transform;
            var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Require(hits.Count>0&&(hits[0].gameObject==button.gameObject||hits[0].gameObject.transform.IsChildOf(button.transform)),
                "Top pointer hit: "+button.name+" at "+pointer.position+"; hits="+string.Join(",",hits.Take(5).Select(h=>h.gameObject.name+"@"+h.sortingOrder)));
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        }
    }
}
#endif
