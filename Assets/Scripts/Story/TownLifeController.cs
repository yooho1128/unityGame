using System.Collections.Generic;
using ShadowTheater.Field;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>시간·날씨·주민 간 거리에 따라 마을의 불빛, 장터, 순찰과 짧은 인사를 갱신한다.</summary>
    public sealed class TownLifeController:MonoBehaviour
    {
        [SerializeField]private string regionId;
        private TownLifeProp[] _props;
        private TownNpcPatrol[] _patrols;
        private FieldNpcPresence[] _people;
        private FieldWorldSnapshot _state;
        private int _segment=-1;
        private float _nextRefresh;
        private float _nextSocialAt;

        public void Configure(string id)=>regionId=id;
        private void Start()
        {
            _props=GetComponentsInChildren<TownLifeProp>(true);
            _patrols=GetComponentsInChildren<TownNpcPatrol>(true);
            _people=GetComponentsInChildren<FieldNpcPresence>(true);
            Refresh(true);_nextSocialAt=Time.time+4f;
        }
        private void Update()
        {
            if(Time.unscaledTime>=_nextRefresh)Refresh(false);
            if(Time.time>=_nextSocialAt){PlaySocialBeat();_nextSocialAt=Time.time+Random.Range(7f,13f);}
        }
        private void Refresh(bool force)
        {
            _nextRefresh=Time.unscaledTime+2f;
            RegionData region=RegionRepository.Get(regionId);if(region==null)return;
            FieldWorldSnapshot next=FieldWorldState.Current(region);
            int segment=next.dayIndex*4+(int)next.timeOfDay;
            if(!force&&segment==_segment&&next.weather==_state.weather)return;
            _state=next;_segment=segment;
            foreach(TownLifeProp prop in _props)if(prop!=null)prop.ApplyWorldState(next);
            foreach(TownNpcPatrol patrol in _patrols)if(patrol!=null)patrol.SetWorldState(next);
        }
        private void PlaySocialBeat()
        {
            if(_people==null||_people.Length<2||PlayerController.Instance==null)return;
            for(int i=0;i<_people.Length;i++)for(int j=i+1;j<_people.Length;j++)
            {
                if(_people[i]==null||_people[j]==null)continue;
                if(Vector2.Distance(_people[i].transform.position,_people[j].transform.position)>3.2f)continue;
                if(Vector2.Distance(_people[i].transform.position,PlayerController.Instance.transform.position)>7f)continue;
                _people[i].PlaySocialGesture(_people[j].transform);
                _people[j].PlaySocialGesture(_people[i].transform);
                _people[Random.value>.5f?i:j].ShowAmbientLine(AmbientLine());return;
            }
        }

        private string AmbientLine()
        {
            switch(_state.weather)
            {
                case FieldWeather.Rain:return Random.value>.5f?"비가 굵어지네.":"장터 천막을 단단히 묶어야겠어.";
                case FieldWeather.Snow:return Random.value>.5f?"길이 얼기 전에 쓸어 두자.":"따뜻한 차가 생각나는 날이야.";
                case FieldWeather.Ashfall:return "오늘도 지붕의 재를 털어야겠군.";
                case FieldWeather.InkStorm:return "간판 글씨가 번지지 않게 조심해.";
                case FieldWeather.MoonWind:return "달바람이 골목까지 들어왔네.";
            }
            switch(_state.timeOfDay)
            {
                case FieldTimeOfDay.Dawn:return "빵 굽는 냄새가 골목에 퍼지네.";
                case FieldTimeOfDay.Evening:return "이제 가게 문을 정리할 시간이군.";
                case FieldTimeOfDay.Night:return "등불 심지를 한 번 더 살펴야겠어.";
                default:return Random.value>.5f?"오늘 장터는 제법 활기차군.":"우물가에 새 소식이 돌던데.";
            }
        }
    }
}
