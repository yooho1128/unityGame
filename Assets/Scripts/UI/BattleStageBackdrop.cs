using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.UI;
namespace ShadowTheater.UI
{
    [RequireComponent(typeof(Image))]
    public class BattleStageBackdrop : MonoBehaviour
    {
        private Image[] motes;
        private Vector2[] homes;
        private float clock;
        private void OnEnable()
        {
            RegionData region=RegionRepository.GetByScene(SaveManager.Current?.mapId);
            int act=region?.act??1;string theme=ThemeForAct(act);
            Sprite sprite=Resources.Load<Sprite>("Art/BattleBackgrounds/"+theme);
            if(sprite==null)sprite=Resources.Load<Sprite>("Art/BattleBackgrounds/Theater");
            if(sprite!=null){GetComponent<Image>().sprite=sprite;GetComponent<Image>().color=Color.white;}
            Color tint=act==2?new Color(1,.5f,.22f):act==7?new Color(.3f,.85f,1):act==8?new Color(.75f,.5f,1):new Color(.65f,.78f,1);
            if(motes==null)
            {
                motes=new Image[12];homes=new Vector2[12];
                for(int i=0;i<motes.Length;i++)
                {
                    var image=new GameObject("StageMote"+i,typeof(RectTransform),typeof(Image)).GetComponent<Image>();
                    image.transform.SetParent(transform,false);image.sprite=BattleFxSpriteLibrary.Get(BattleFxGlyph.Star);
                    image.raycastTarget=false;image.rectTransform.sizeDelta=Vector2.one*(3+i%3*2);
                    homes[i]=new Vector2((i*.618034f)%1f,.15f+(i*.381966f)% .65f);motes[i]=image;
                }
            }
            foreach(Image mote in motes)mote.color=tint;
        }
        private void Update()
        {
            if(motes==null)return;clock+=Time.unscaledDeltaTime;
            for(int i=0;i<motes.Length;i++)
            {
                var rect=motes[i].rectTransform;
                Vector2 p=homes[i]+new Vector2(Mathf.Sin(clock*.19f+i)*.018f,Mathf.Repeat(clock*(.008f+i%3*.003f)+i*.07f,.6f)-.3f);
                rect.anchorMin=rect.anchorMax=p;rect.anchoredPosition=Vector2.zero;
                Color color=motes[i].color;color.a=.12f+.18f*(.5f+.5f*Mathf.Sin(clock*.8f+i*2.3f));motes[i].color=color;
            }
        }
        public static string ThemeForAct(int act)=>act==2?"Ember":act==3 || act==6?"Archive":act==7?"Ocean":act==8?"Cosmos":"Theater";
    }
}
