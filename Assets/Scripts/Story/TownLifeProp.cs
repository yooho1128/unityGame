using System.Collections;
using ShadowTheater.Field;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Story
{
    public enum TownPropKind { House, Door, Stall, Well, Bench, NoticeBoard, Lantern, Planter }

    /// <summary>마을 장식이 시간·날씨와 플레이어 조사에 반응하도록 만드는 가벼운 생활 오브젝트.</summary>
    [DisallowMultipleComponent]
    public sealed class TownLifeProp : MonoBehaviour, IInteractable
    {
        [SerializeField] private TownPropKind kind;
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private string customMessage;
        private Vector3 _baseScale;
        private Color _baseColor;
        private bool _busy;
        private bool _night;
        private FieldWeather _weather;
        private float _visibilityAlpha=1f;

        private void Awake()
        {
            if(target==null)target=GetComponent<SpriteRenderer>();
            _baseScale=transform.localScale;
            _baseColor=target!=null?target.color:Color.white;
        }

        public void Configure(TownPropKind propKind,SpriteRenderer renderer,string message="")
        {kind=propKind;target=renderer;customMessage=message;_baseScale=transform.localScale;_baseColor=renderer!=null?renderer.color:Color.white;}

        public void ApplyWorldState(FieldWorldSnapshot state)
        {
            _night=state.timeOfDay==FieldTimeOfDay.Night;
            _weather=state.weather;
            if(target==null)return;
            if(kind==TownPropKind.Lantern)
            {
                float glow=_night?1f:state.timeOfDay==FieldTimeOfDay.Evening?.78f:.34f;
                target.color=Color.Lerp(_baseColor,new Color(1f,.72f,.28f,_baseColor.a),glow);
            }
            else if(kind==TownPropKind.Stall)
                target.color=Color.Lerp(_baseColor,new Color(.48f,.5f,.58f,_baseColor.a),_night?.42f:0f);
            else if(kind==TownPropKind.Planter&&state.weather==FieldWeather.Rain)
                target.color=Color.Lerp(_baseColor,new Color(.45f,.8f,.48f,_baseColor.a),.24f);
        }

        private void LateUpdate()
        {
            if(kind!=TownPropKind.House||target==null)return;
            PlayerController player=PlayerController.Instance;
            bool obscures=player!=null&&player.transform.position.y>transform.position.y-.25f&&
                player.transform.position.y<transform.position.y+2.8f&&
                Mathf.Abs(player.transform.position.x-transform.position.x)<Mathf.Max(1.1f,transform.localScale.x*.62f);
            float desired=obscures?.42f:1f;
            _visibilityAlpha=Mathf.MoveTowards(_visibilityAlpha,desired,Time.unscaledDeltaTime*3.8f);
            Color c=target.color;c.a=_baseColor.a*_visibilityAlpha;target.color=c;
        }

        public void Interact(PlayerController player)
        {
            if(_busy)return;
            if(kind==TownPropKind.Door&&TownInteriorController.Instance!=null)
            {
                TownInteriorController.Instance.Enter(player,name.Contains("West")?"조용한 주민의 집":"마을 공동 휴게실");return;
            }
            if(kind==TownPropKind.Bench)
            {
                RegionData region=RegionRepository.GetByScene(ShadowTheater.Save.SaveManager.Current?.mapId);
                if(region==null){SaveFeedbackController.Instance?.ShowMessage("지금은 쉴 수 없습니다.");return;}
                bool rested=FieldRestService.TryRest(region.regionId,false,out string restMessage);
                if(rested)StartCoroutine(React());
                SaveFeedbackController.Instance?.ShowMessage(restMessage);return;
            }
            StartCoroutine(React());
            GameSettings.TryVibrate();
            SaveFeedbackController.Instance?.ShowMessage(string.IsNullOrEmpty(customMessage)?Message():customMessage);
        }

        private string Message()
        {
            switch(kind)
            {
                case TownPropKind.House:return _night?"창문 너머로 따뜻한 불빛과 이야기 소리가 새어 나옵니다.":"사람이 드나든 흔적이 있는 단정한 집입니다.";
                case TownPropKind.Door:return _night?"문이 잠겨 있습니다. 안에서는 조용한 숨소리가 들립니다.":"문이 부드럽게 열렸다가 다시 닫힙니다.";
                case TownPropKind.Stall:return _night?"오늘 장사는 끝났지만 향신료 냄새가 아직 남아 있습니다.":"지역에서 모은 물건들이 가지런히 진열되어 있습니다.";
                case TownPropKind.Well:return _weather==FieldWeather.Rain?"빗방울이 우물 수면에 동그란 파문을 만듭니다.":"깊은 곳에서 맑은 물소리가 울립니다.";
                case TownPropKind.Bench:return "잠깐 앉아 마을 사람들의 움직임을 바라보았습니다.";
                case TownPropKind.NoticeBoard:return _night?"내일의 장터 일정과 순찰 교대표가 붙어 있습니다.":"새 소식과 주민들의 부탁이 빼곡히 붙어 있습니다.";
                case TownPropKind.Lantern:return _night?"등불이 골목을 따뜻하게 밝히고 있습니다.":"해가 지면 켜질 수 있도록 심지가 잘 손질되어 있습니다.";
                default:return "계절에 맞춰 정성껏 돌본 작은 화단입니다.";
            }
        }

        private IEnumerator React()
        {
            _busy=true;
            Vector3 from=_baseScale;
            for(float elapsed=0f;elapsed<.3f;elapsed+=Time.unscaledDeltaTime)
            {
                float t=Mathf.Clamp01(elapsed/.3f),pulse=Mathf.Sin(t*Mathf.PI);
                Vector3 response=kind==TownPropKind.Door?new Vector3(1f-pulse*.72f,1f+pulse*.03f,1f):
                    kind==TownPropKind.Lantern?new Vector3(1f+pulse*.04f,1f+pulse*.09f,1f):
                    new Vector3(1f+pulse*.06f,1f-pulse*.045f,1f);
                transform.localScale=Vector3.Scale(from,response);
                yield return null;
            }
            transform.localScale=from;_busy=false;
        }
    }
}
