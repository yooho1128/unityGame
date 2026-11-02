using ShadowTheater.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>Runtime skin for both existing generated battle canvases and new ones.</summary>
    public static class BattleStageStyle
    {
        public static void LayoutUnits(BattleUnitPanel player,BattleUnitPanel enemy,BattlePartyStrip playerParty,BattlePartyStrip enemyParty)
        {
            if(player!=null){Place(player.transform,new Vector2(.05f,.335f),new Vector2(.49f,.67f));Paint(player.gameObject,TheatreUiStyle.Surface);}
            if(enemy!=null){Place(enemy.transform,new Vector2(.51f,.565f),new Vector2(.95f,.90f));Paint(enemy.gameObject,TheatreUiStyle.Surface);}
            if(playerParty!=null)Place(playerParty.transform,new Vector2(.05f,.285f),new Vector2(.45f,.33f));
            if(enemyParty!=null)Place(enemyParty.transform,new Vector2(.55f,.515f),new Vector2(.95f,.56f));
        }
        public sealed class ResultWidgets
        {
            public GameObject root;public CanvasGroup group;public RectTransform card;public Image accent;
            public Text title,summary,details,continueText;
        }
        public static ResultWidgets CreateResult(Transform parent,Font font,UnityEngine.Events.UnityAction confirm)
        {
            var widgets=new ResultWidgets();
            var shade=RuntimeUiControls.Rect(parent,"Result",Vector2.zero,Vector2.one);widgets.root=shade.gameObject;
            shade.gameObject.AddComponent<Image>().color=new Color(.008f,.014f,.026f,.9f);
            widgets.group=shade.gameObject.AddComponent<CanvasGroup>();
            widgets.card=RuntimeUiControls.Rect(shade,"Card",new Vector2(.16f,.12f),new Vector2(.84f,.88f));
            Paint(widgets.card.gameObject,TheatreUiStyle.Midnight);
            widgets.accent=RuntimeUiControls.Rect(widgets.card,"Accent",new Vector2(0,.985f),Vector2.one).gameObject.AddComponent<Image>();
            widgets.title=ResultText(widgets.card,font,"Title",46,new Vector2(.065f,.79f),new Vector2(.72f,.94f));
            widgets.summary=ResultText(widgets.card,font,"Summary",28,new Vector2(.07f,.66f),new Vector2(.93f,.78f));
            widgets.details=ResultText(widgets.card,font,"Details",23,new Vector2(.07f,.23f),new Vector2(.93f,.63f));
            widgets.details.alignment=TextAnchor.UpperLeft;
            var button=RuntimeUiControls.Button(widgets.card,font,"계속",new Vector2(.3f,.06f),new Vector2(.7f,.19f),confirm);
            button.name="Continue";widgets.continueText=button.GetComponentInChildren<Text>();
            widgets.root.SetActive(false);return widgets;
        }
        private static Text ResultText(Transform parent,Font font,string name,int size,Vector2 min,Vector2 max)
        {
            var text=RuntimeUiControls.Label(parent,font,"",size);text.name=name;
            RuntimeUiControls.Place(text.rectTransform,min,max);return text;
        }
        public static void Apply(Transform root,GameObject actions,GameObject message,GameObject options,GameObject result)
        {
            EnsureStageArt(root);
            var background=root.Find("Backdrop")?.GetComponent<Image>();
            if(background!=null&&background.sprite==null)background.color=TheatreUiStyle.Midnight;
            Paint(root.Find("SafeArea/TopBar")?.gameObject,TheatreUiStyle.Surface);
            Paint(actions,TheatreUiStyle.Midnight);Paint(message,TheatreUiStyle.Surface);Paint(options,TheatreUiStyle.Surface);
            Place(options!=null?options.transform:null,new Vector2(.04f,.035f),new Vector2(.96f,.50f));
            if(actions!=null)
            {
                Place(actions.transform,new Vector2(.04f,.035f),new Vector2(.96f,.19f));
                string[] names={"Attack","Skills","Switch","Record","Items","Escape"};
                for(int i=0;i<names.Length;i++)Place(actions.transform.Find(names[i]),new Vector2(i/6f+.006f,.08f),new Vector2((i+1)/6f-.006f,.92f));
            }
            if(actions!=null)foreach(var button in actions.GetComponentsInChildren<Button>(true))
                TheatreUiStyle.Button(button,button.name=="Attack");
            if(options!=null)foreach(var button in options.GetComponentsInChildren<Button>(true))TheatreUiStyle.Button(button);
            if(result==null)return;
            var card=result.transform.Find("Card") as RectTransform;if(card==null)return;
            Paint(card.gameObject,TheatreUiStyle.Midnight);
            RuntimeUiControls.Place(card,new Vector2(.16f,.12f),new Vector2(.84f,.88f));
            foreach(var text in card.GetComponentsInChildren<Text>(true))
                text.color=text.name=="Summary"?TheatreUiStyle.Brass:TheatreUiStyle.Ivory;
            foreach(var button in card.GetComponentsInChildren<Button>(true))TheatreUiStyle.Button(button,true);
            Place(card.Find("Title"),new Vector2(.065f,.79f),new Vector2(.72f,.94f));
            Place(card.Find("Summary"),new Vector2(.07f,.66f),new Vector2(.93f,.78f));
            Place(card.Find("Details"),new Vector2(.07f,.23f),new Vector2(.93f,.63f));
            var title=card.Find("Title")?.GetComponent<Text>();if(title==null)return;
            var badge=RuntimeUiControls.Rect(card,"PerformanceSeal",new Vector2(.76f,.80f),new Vector2(.94f,.94f));
            Paint(badge.gameObject,TheatreUiStyle.Raised);
            var seal=RuntimeUiControls.Label(badge,title.font,"",42);seal.color=TheatreUiStyle.Brass;
            seal.fontStyle=FontStyle.Bold;seal.resizeTextForBestFit=true;seal.resizeTextMinSize=20;seal.resizeTextMaxSize=42;
        }
        private static void EnsureStageArt(Transform root)
        {
            if(root.Find("ReleaseStageArt")!=null)return;
            var safeArea=root.Find("SafeArea");
            if(Resources.Load<Sprite>("Art/BattleBackgrounds/Theater")==null)return;
            var art=RuntimeUiControls.Rect(root,"ReleaseStageArt",Vector2.zero,Vector2.one);
            art.gameObject.AddComponent<Image>().raycastTarget=false;
            art.gameObject.AddComponent<BattleStageBackdrop>();
            var backdrop=root.Find("Backdrop");
            art.SetSiblingIndex(safeArea!=null?safeArea.GetSiblingIndex():backdrop!=null?backdrop.GetSiblingIndex()+1:0);
        }
        public static void ConfigureHealthFill(Image image)
        {
            if(image==null)return;
            if(image.sprite==null)image.sprite=BattleFxSpriteLibrary.Get(BattleFxGlyph.Solid);
            image.type=Image.Type.Filled;image.fillMethod=Image.FillMethod.Horizontal;
            image.fillOrigin=0;image.raycastTarget=false;
        }
        public static void PresentResult(GameObject result,BattleOutcome outcome)
        {
            var label=result.transform.Find("Card/PerformanceSeal")?.GetComponentInChildren<Text>();
            if(label==null)return;
            bool won=outcome.result==BattleResult.Victory||outcome.result==BattleResult.Captured;
            label.text=won?(string.IsNullOrEmpty(outcome.performanceRank)?"완료":outcome.performanceRank):"—";
            label.color=won?TheatreUiStyle.Brass:TheatreUiStyle.Muted;
        }
        private static void Paint(GameObject go,Color color)
        {
            if(go==null)return;
            var image=go.GetComponent<Image>()??go.AddComponent<Image>();TheatreUiStyle.Panel(image,color);
        }
        private static void Place(Transform transform,Vector2 min,Vector2 max)
        {if(transform is RectTransform rect)RuntimeUiControls.Place(rect,min,max);}
    }
}
