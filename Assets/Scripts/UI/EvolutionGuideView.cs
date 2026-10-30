using System;
using ShadowTheater.Data;
using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>Child panel of the book: shares its modal lock and works with legacy prefabs.</summary>
    public sealed class EvolutionGuideView
    {
        private readonly GameObject root;
        private readonly Text title, body, methodLabel, trackLabel;
        private readonly Button track, map, previous, next;
        private readonly Button[] tabs = new Button[3];
        private readonly ScrollRect scroll;
        private readonly Action closeBook;
        private ShadowInstance instance;
        private int stage, routeIndex;
        public bool IsOpen => root.activeSelf;
        public EvolutionGuideView(Transform parent, Font font, Action closeBook)
        {
            this.closeBook = closeBook;
            root = new GameObject("EvolutionGuide", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            Place((RectTransform)root.transform, .035f, .035f, .965f, .965f);
            TheatreUiStyle.Panel(root.GetComponent<Image>(), TheatreUiStyle.Midnight);
            title = Label(root.transform, font, "Title", .05f,.9f,.78f,.97f,30);
            Button(root.transform,font,"닫기",.81f,.9f,.96f,.97f,Close);
            string[] names = { "기억 복원", "구원", "원한" };
            for (int i=0;i<3;i++)
            {
                int index=i;
                tabs[i]=Button(root.transform,font,names[i],.05f+i*.305f,.81f,.335f+i*.305f,.88f,
                    () => {stage=index;routeIndex=0;Refresh();});
            }
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(root.transform,false);
            Place((RectTransform)viewport.transform,.055f,.26f,.945f,.79f);
            viewport.GetComponent<Image>().color=new Color(0,0,0,.03f);
            body=Label(viewport.transform,font,"Checklist",0,0,1,1,23);
            body.alignment=TextAnchor.UpperLeft;body.verticalOverflow=VerticalWrapMode.Overflow;
            body.rectTransform.anchorMin=new Vector2(0,1);body.rectTransform.anchorMax=Vector2.one;body.rectTransform.pivot=new Vector2(.5f,1);
            body.rectTransform.offsetMin=body.rectTransform.offsetMax=Vector2.zero;
            body.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll=viewport.GetComponent<ScrollRect>();scroll.viewport=(RectTransform)viewport.transform;
            scroll.content=body.rectTransform;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
            previous=Button(root.transform,font,"이전 방법",.05f,.17f,.29f,.24f,()=>{routeIndex--;Refresh();});
            methodLabel=Label(root.transform,font,"Method",.30f,.17f,.70f,.24f,22);
            next=Button(root.transform,font,"다음 방법",.71f,.17f,.95f,.24f,()=>{routeIndex++;Refresh();});
            track=Button(root.transform,font,"여정 추적",.05f,.06f,.49f,.145f,Track);
            trackLabel=track.GetComponentInChildren<Text>();
            map=Button(root.transform,font,"장소 지도",.51f,.06f,.95f,.145f,OpenMap);
            root.SetActive(false);
        }
        public void Show(ShadowInstance selected)
        {
            if(selected==null)return;
            instance=selected;stage=selected.memoryStage==MemoryStage.Echo?0:1;routeIndex=0;
            root.transform.SetAsLastSibling();root.SetActive(true);Refresh();
        }
        public void Close()=>root.SetActive(false);
        private EvolutionGuidanceService.Route Selected
        {
            get
            {
                var routes=EvolutionGuidanceService.Routes(EvolutionGuidanceService.Form(instance,stage)?.requiredFlag);
                return routeIndex>=0&&routeIndex<routes.Count?routes[routeIndex]:null;
            }
        }
        public void Refresh()
        {
            if(!IsOpen)return;
            title.text="진화 안내 · "+instance.DisplayName;
            for(int i=0;i<tabs.Length;i++)tabs[i].interactable=i!=stage;
            var form=EvolutionGuidanceService.Form(instance,stage);
            var routes=EvolutionGuidanceService.Routes(form?.enabled==true?form.requiredFlag:null);
            routeIndex=Mathf.Clamp(routeIndex,0,Mathf.Max(0,routes.Count-1));
            var route=form?.enabled==true?Selected:null;
            body.text=EvolutionGuidanceService.Checklist(instance,stage)+(form?.enabled==true?"\n\n"+EvolutionGuidanceService.Describe(route):"");
            methodLabel.text=routes.Count>0?$"방법 {routeIndex+1} / {routes.Count}":form?.enabled==true?"레벨 성장":"진화 단계 없음";
            previous.interactable=routeIndex>0;next.interactable=routeIndex+1<routes.Count;
            var progress=SaveManager.Current?.quests?.Find(p=>p.questId==route?.questId);
            bool canStart=route?.personal==true&&stage==0&&MemoryAwakeningService.CanStartMemoryQuest(instance,out string _);
            track.interactable=route?.kind=="Quest"&&QuestManager.Instance!=null&&(canStart||progress?.completed==false);
            trackLabel.text=canStart?"여정 시작·추적":"여정 추적";
            map.interactable=route!=null&&WorldMapController.Instance!=null&&QuestGuidanceService.ForObjective(EvolutionGuidanceService.Objective(route)).destination!=null;
            UiListLayout.Rebuild(body.transform,Vector2.zero);
            scroll.verticalNormalizedPosition=1;
        }
        private void Track()
        {
            var route=Selected;
            if(route?.kind!="Quest"||QuestManager.Instance==null)return;
            if(SaveManager.Current?.quests?.Find(p=>p.questId==route.questId)==null)
            {
                if(!route.personal||!MemoryAwakeningService.TryStartMemoryQuest(instance,out string message))
                {SaveFeedbackController.Instance?.ShowMessage("이야기를 진행해 여정을 시작하세요.");return;}
            }
            QuestManager.Instance.TryTrackQuest(route.questId,out string feedback);
            SaveFeedbackController.Instance?.ShowMessage(feedback);Refresh();
        }
        private void OpenMap()
        {
            var objective=EvolutionGuidanceService.Objective(Selected);
            if(WorldMapController.Instance==null||QuestGuidanceService.ForObjective(objective).destination==null)return;
            Close();closeBook(); // Release the book lock before the map takes its own lock.
            WorldMapController.Instance.OpenForObjective(objective);
        }
        private static Button Button(Transform parent,Font font,string label,float x,float y,float r,float t,UnityEngine.Events.UnityAction action)
        {
            var go=new GameObject(label,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);
            Place((RectTransform)go.transform,x,y,r,t);
            var button=go.GetComponent<Button>();button.targetGraphic=go.GetComponent<Image>();button.onClick.AddListener(action);
            Label(go.transform,font,"Label",.02f,0,.98f,1,22).text=label;
            TheatreUiStyle.Button(button);return button;
        }
        private static Text Label(Transform parent,Font font,string name,float x,float y,float r,float t,int size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            Place((RectTransform)go.transform,x,y,r,t);var text=go.GetComponent<Text>();text.font=font;text.fontSize=size;
            text.color=TheatreUiStyle.Ivory;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;return text;
        }
        private static void Place(RectTransform rect,float x,float y,float r,float t)
        {rect.anchorMin=new Vector2(x,y);rect.anchorMax=new Vector2(r,t);rect.offsetMin=rect.offsetMax=Vector2.zero;}
    }
}
