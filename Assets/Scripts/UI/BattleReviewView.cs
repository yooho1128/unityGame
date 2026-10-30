using System;
using System.Linq;
using System.Text;
using ShadowTheater.Battle;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>Child of Pause: owns no additional movement lock.</summary>
    public sealed class BattleReviewView
    {
        private readonly GameObject root;
        private readonly Text body,page;
        private readonly Button previous,next;
        private int index;
        public bool IsOpen=>root.activeSelf;
        public BattleReviewView(Transform parent,Font font)
        {
            var rect=RuntimeUiControls.Rect(parent,"BattleReview",new Vector2(.035f,.035f),new Vector2(.965f,.965f));root=rect.gameObject;
            TheatreUiStyle.Panel(rect.gameObject.AddComponent<Image>(),TheatreUiStyle.Midnight);
            var title=RuntimeUiControls.Label(rect,font,"전투 복기",32);Place(title,new Vector2(.05f,.9f),new Vector2(.70f,.98f));
            RuntimeUiControls.Button(rect,font,"닫기",new Vector2(.80f,.91f),new Vector2(.95f,.98f),Close);
            var viewport=RuntimeUiControls.Rect(rect,"Viewport",new Vector2(.055f,.20f),new Vector2(.945f,.89f));
            viewport.gameObject.AddComponent<Image>().color=new Color(0,0,0,.03f);viewport.gameObject.AddComponent<RectMask2D>();
            body=RuntimeUiControls.Label(viewport,font,"",22);body.alignment=TextAnchor.UpperLeft;body.verticalOverflow=VerticalWrapMode.Overflow;
            body.rectTransform.anchorMin=new Vector2(0,1);body.rectTransform.anchorMax=Vector2.one;body.rectTransform.pivot=new Vector2(.5f,1);
            body.rectTransform.offsetMin=body.rectTransform.offsetMax=Vector2.zero;
            body.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=body.rectTransform;
            scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
            previous=RuntimeUiControls.Button(rect,font,"이전 전투",new Vector2(.05f,.10f),new Vector2(.31f,.18f),()=>{index--;Refresh();});
            page=RuntimeUiControls.Label(rect,font,"",20);Place(page,new Vector2(.32f,.1f),new Vector2(.68f,.18f));
            next=RuntimeUiControls.Button(rect,font,"다음 전투",new Vector2(.69f,.10f),new Vector2(.95f,.18f),()=>{index++;Refresh();});
            RuntimeUiControls.Button(rect,font,"기록 JSON 복사",new Vector2(.28f,.015f),new Vector2(.72f,.085f),()=>
            {GUIUtility.systemCopyBuffer=BattleReviewService.ExportJson();SaveFeedbackController.Instance?.ShowMessage("전투 복기 기록을 복사했습니다.");});
            root.SetActive(false);
        }
        public void Open(){index=BattleReviewService.Entries.Count-1;root.transform.SetAsLastSibling();root.SetActive(true);Refresh();}
        public void Close()=>root.SetActive(false);
        private void Refresh()
        {
            var entries=BattleReviewService.Entries;index=Mathf.Clamp(index,0,Mathf.Max(0,entries.Count-1));
            previous.interactable=index>0;next.interactable=index+1<entries.Count;page.text=$"{(entries.Count==0?0:index+1)} / {entries.Count} · 최근 {BattleReviewPolicy.Capacity}전";
            if(entries.Count==0){body.text="아직 전투 기록이 없습니다.\n새 전투를 마치면 당시 파티·적·회복·보상을 여기에서 확인할 수 있습니다.\n"+BattleReviewService.Status;return;}
            var e=entries[index];var b=new StringBuilder();var region=RegionRepository.Get(e.regionId);
            b.AppendLine((region!=null?region.displayName:"지역 미상")+" · "+BattleReviewPolicy.ResultName(e.result));
            if(DateTimeOffset.TryParse(e.startedUtc,out var time))b.AppendLine(time.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
            string mode=e.mode=="Boss"?"보스전":e.mode=="Wild"?"야생전":"대결";
            b.AppendLine($"{e.turns}턴 · 기절 {e.playerFaints}회 · {mode}");
            b.AppendLine($"전투 보상 {e.declaredRewardGold:N0} 금화 · 전투 경험치 합계 {e.declaredExperience:N0}");
            b.AppendLine($"결과 반영 구간 금화 {e.goldBefore:N0} → {e.goldAfter:N0} (퀘스트 보상 포함 가능)");
            Units(b,"당시 파티",e.players);Units(b,"상대",e.enemies);
            var used=e.items.Where(i=>i.before>i.after).ToList();b.AppendLine("\n소비 도구 · "+(used.Count==0?"없음":string.Join(" · ",used.Select(i=>(string.IsNullOrEmpty(i.name)?i.itemId:i.name)+" x"+(i.before-i.after)))));
            b.AppendLine("\n회복량은 기술·아이템·보스 페이즈를 포함하며 레벨업 회복은 제외합니다. 중단된 전투는 승패 집계에서 제외합니다.");
            if(!string.IsNullOrEmpty(BattleReviewService.Status))b.AppendLine(BattleReviewService.Status);
            body.text=b.ToString();UiListLayout.Rebuild(body.transform,Vector2.zero);
        }
        private static void Units(StringBuilder b,string title,System.Collections.Generic.List<BattleReviewUnit> units)
        {
            b.AppendLine("\n"+title);
            foreach(var u in units)
            {
                b.AppendLine($"{u.name} · {u.stage} · Lv.{u.levelBefore}"+(u.levelBefore!=u.levelAfter?$" → {u.levelAfter}":""));
                b.AppendLine($"  HP {u.hpBefore}/{u.maxHpBefore} → {u.hpAfter}/{u.maxHpAfter} · 시작 공/방/속 {u.attack}/{u.defense}/{u.speed}");
                b.AppendLine($"  받은 피해 {u.damageTaken:N0} · 회복 {u.healingReceived:N0} · 회복 기술 {u.healingSkillUses}/{HealingSkillBudget.Limit}회 · 실제 XP +{Math.Max(0,u.xpAfter-u.xpBefore):N0}");
            }
        }
        private static void Place(Text text,Vector2 min,Vector2 max)=>RuntimeUiControls.Place(text.rectTransform,min,max);
    }
}
