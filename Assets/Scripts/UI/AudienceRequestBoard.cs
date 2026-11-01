using System;
using ShadowTheater.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>Optional first-turn objective draft and a persistent, readable request ticket.</summary>
    public sealed class AudienceRequestBoard
    {
        private readonly BattleManager manager;
        private readonly Action returnFocus;
        private readonly GameObject hud,overlay;
        private readonly RectTransform panel;
        private readonly Text heading,description,reward,status;
        private readonly Button choose;
        private readonly Image progress;
        private readonly Button[] cards=new Button[3];
        private readonly Text[] titles=new Text[3],descriptions=new Text[3],rewards=new Text[3];
        private readonly RectTransform[] cardRects=new RectTransform[3];
        private float lastAspect=-1;
        public bool IsOpen=>overlay.activeSelf;

        public AudienceRequestBoard(Transform parent,Font font,BattleManager manager,Action returnFocus)
        {
            this.manager=manager;this.returnFocus=returnFocus;
            var ticket=RuntimeUiControls.Rect(parent,"AudienceTicket",new Vector2(.045f,.72f),new Vector2(.47f,.91f));hud=ticket.gameObject;
            TheatreUiStyle.Panel(ticket.gameObject.AddComponent<Image>(),TheatreUiStyle.Surface);
            Rule(ticket,new Vector2(0,0),new Vector2(.008f,1),TheatreUiStyle.Brass);
            Label(ticket,font,"관객 의뢰",20,new Vector2(.045f,.78f),new Vector2(.56f,.96f),TheatreUiStyle.Brass);
            heading=Label(ticket,font,"",29,new Vector2(.045f,.51f),new Vector2(.955f,.79f),TheatreUiStyle.Ivory);
            description=Label(ticket,font,"",22,new Vector2(.045f,.26f),new Vector2(.955f,.52f),TheatreUiStyle.Muted);
            reward=Label(ticket,font,"",19,new Vector2(.045f,.05f),new Vector2(.64f,.23f),TheatreUiStyle.Mint);
            choose=RuntimeUiControls.Button(ticket,font,"의뢰 선택",new Vector2(.68f,.04f),new Vector2(.96f,.24f),Open);
            choose.GetComponentInChildren<Text>().fontSize=21;
            status=Label(ticket,font,"",19,new Vector2(.60f,.79f),new Vector2(.96f,.96f),TheatreUiStyle.Brass);
            var track=RuntimeUiControls.Rect(ticket,"Progress",new Vector2(.045f,.25f),new Vector2(.955f,.264f));
            track.gameObject.AddComponent<Image>().color=TheatreUiStyle.Raised;
            progress=Rule(track,Vector2.zero,Vector2.one,TheatreUiStyle.Brass);

            var shade=RuntimeUiControls.Rect(parent,"AudienceDraft",Vector2.zero,Vector2.one);overlay=shade.gameObject;
            shade.gameObject.AddComponent<Image>().color=new Color(.008f,.014f,.026f,.94f);
            panel=RuntimeUiControls.Rect(shade,"DraftPanel",new Vector2(.055f,.10f),new Vector2(.945f,.90f));
            TheatreUiStyle.Panel(panel.gameObject.AddComponent<Image>(),TheatreUiStyle.Midnight);
            Rule(panel,new Vector2(0,.991f),Vector2.one,TheatreUiStyle.Brass);
            Label(panel,font,"이번 무대의 목표를 고르세요",38,new Vector2(.04f,.85f),new Vector2(.96f,.96f),TheatreUiStyle.Ivory);
            Label(panel,font,"첫 행동 전 한 번 선택 · 턴과 FP 소모 없음 · 선택하지 않으면 현재 의뢰 유지",23,
                new Vector2(.04f,.75f),new Vector2(.96f,.85f),TheatreUiStyle.Muted);
            for(int i=0;i<cards.Length;i++)
            {
                int index=i;
                var card=RuntimeUiControls.Rect(panel,"RequestCard_"+i,Vector2.zero,Vector2.one);cardRects[i]=card;
                cards[i]=RuntimeUiControls.Button(card,font,"",Vector2.zero,Vector2.one,()=>Select(index));
                cards[i].name="ChooseRequest_"+i;
                Rule(card,new Vector2(.04f,.94f),new Vector2(.96f,.947f),TheatreUiStyle.Brass);
                Label(card,font,"관객 제안 0"+(i+1),19,new Vector2(.07f,.79f),new Vector2(.93f,.92f),TheatreUiStyle.Brass);
                titles[i]=Label(card,font,"",31,new Vector2(.07f,.57f),new Vector2(.93f,.79f),TheatreUiStyle.Ivory);
                descriptions[i]=Label(card,font,"",25,new Vector2(.07f,.29f),new Vector2(.93f,.57f),TheatreUiStyle.Muted);
                rewards[i]=Label(card,font,"",23,new Vector2(.07f,.09f),new Vector2(.93f,.27f),TheatreUiStyle.Mint);
            }
            RuntimeUiControls.Button(panel,font,"현재 의뢰 유지 · 닫기",new Vector2(.29f,.06f),new Vector2(.71f,.16f),Close);
            overlay.SetActive(false);hud.SetActive(false);
        }
        public void Open()
        {
            if(!manager.CanChooseAudienceRequest)return;
            overlay.transform.SetAsLastSibling();overlay.SetActive(true);lastAspect=-1;Layout();
            for(int i=0;i<cards.Length;i++)
            {
                bool offered=i<manager.AudienceChoices.Count;cardRects[i].gameObject.SetActive(offered);
                if(!offered)continue;
                var preview=new AudienceRequest();
                preview.Begin(manager.AudienceChoices[i],new AudienceRequestContext{enemyCount=manager.EnemyUnits.Count,mode=manager.Context.mode});
                titles[i].text=preview.Title;
                descriptions[i].text=preview.Description;
                rewards[i].text=$"달성 후 승리·기록\n+{manager.AudienceRewardGold:N0} 금화";
            }
            DesktopPlatformController.SelectFirstUsable(overlay);
        }
        private void Select(int index)
        {
            if(index>=manager.AudienceChoices.Count||!manager.TryChooseAudienceRequest(manager.AudienceChoices[index]))return;
            Close();GameFeedbackAudio.Play(FeedbackCue.Confirm);
        }
        public void Close()
        {
            if(!IsOpen)return;
            overlay.SetActive(false);returnFocus?.Invoke();
        }
        public void Hide(){overlay.SetActive(false);hud.SetActive(false);}
        public void Refresh()
        {
            bool visible=manager.Context!=null&&manager.Audience.IsActive&&!manager.IsEnded;
            hud.SetActive(visible);
            if(!visible||!manager.CanChooseAudienceRequest)Close();
            if(!visible)return;
            var request=manager.Audience;
            Color accent=request.IsAchieved?TheatreUiStyle.Mint:request.IsFailed?new Color(.96f,.52f,.58f):TheatreUiStyle.Brass;
            heading.text=request.Title;
            description.text=request.Type==AudienceRequestType.Repertoire&&request.IsPending
                ?request.Description+$" ({request.RepertoireCount}/{AudienceRequest.RepertoireTarget})":request.Description;
            status.text=request.IsAchieved?"달성":request.IsFailed?"미달성":manager.CanChooseAudienceRequest?"선택 가능":"도전 중";
            status.color=accent;progress.color=accent;
            float amount=request.IsAchieved?1:request.Type==AudienceRequestType.Repertoire
                ?(float)request.RepertoireCount/AudienceRequest.RepertoireTarget:0;
            if(request.Type==AudienceRequestType.SwiftCurtain&&request.IsPending)
                amount=(float)Math.Max(0,request.TurnLimit-manager.Turn+1)/Math.Max(1,request.TurnLimit);
            var fill=progress.rectTransform;fill.anchorMax=new Vector2(Mathf.Clamp01(amount),1);
            reward.text=request.IsFailed?"의뢰 보너스 없음":$"달성 후 승리·기록 +{manager.AudienceRewardGold:N0} 금화";
            choose.gameObject.SetActive(manager.CanChooseAudienceRequest);
        }
        public void Layout()
        {
            if(!IsOpen||panel.rect.height<=0)return;
            float aspect=panel.rect.width/panel.rect.height;
            if(Mathf.Abs(aspect-lastAspect)<.01f)return;lastAspect=aspect;
            bool wide=aspect>=1.15f;
            for(int i=0;i<cardRects.Length;i++)
            {
                if(wide)RuntimeUiControls.Place(cardRects[i],new Vector2(.04f+i*.312f,.23f),new Vector2(.328f+i*.312f,.72f));
                else RuntimeUiControls.Place(cardRects[i],new Vector2(.04f,.56f-i*.18f),new Vector2(.96f,.72f-i*.18f));
                if(!wide)
                {
                    RuntimeUiControls.Place(titles[i].rectTransform,new Vector2(.04f,.48f),new Vector2(.65f,.78f));
                    RuntimeUiControls.Place(descriptions[i].rectTransform,new Vector2(.04f,.10f),new Vector2(.65f,.48f));
                    RuntimeUiControls.Place(rewards[i].rectTransform,new Vector2(.69f,.12f),new Vector2(.96f,.80f));
                }
                else
                {
                    RuntimeUiControls.Place(titles[i].rectTransform,new Vector2(.07f,.57f),new Vector2(.93f,.79f));
                    RuntimeUiControls.Place(descriptions[i].rectTransform,new Vector2(.07f,.29f),new Vector2(.93f,.57f));
                    RuntimeUiControls.Place(rewards[i].rectTransform,new Vector2(.07f,.09f),new Vector2(.93f,.27f));
                }
            }
        }
        private static Text Label(Transform parent,Font font,string text,int size,Vector2 min,Vector2 max,Color color)
        {
            var label=RuntimeUiControls.Label(parent,font,text,size);RuntimeUiControls.Place(label.rectTransform,min,max);
            label.alignment=TextAnchor.MiddleLeft;label.color=color;label.resizeTextForBestFit=true;
            label.resizeTextMinSize=Math.Min(18,size);label.resizeTextMaxSize=size;return label;
        }
        private static Image Rule(Transform parent,Vector2 min,Vector2 max,Color color)
        {
            var rect=RuntimeUiControls.Rect(parent,"Rule",min,max);var image=rect.gameObject.AddComponent<Image>();
            image.color=color;image.raycastTarget=false;return image;
        }
    }
}
