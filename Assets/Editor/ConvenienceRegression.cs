#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Data;
using ShadowTheater.Story;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    public static class ConvenienceRegression
    {
        public static void Validate(List<string> errors)
        {
            var ids=new HashSet<string>();
            foreach(var hub in JourneyHubPolicy.All)
            {
                var region=RegionRepository.Get(hub.regionId);
                if(!ids.Add(hub.regionId)||region==null||region.act<7||region.isSettlement||region.isHidden||region.isBossStage)
                    errors.Add("후반 보급 거점의 지역 설정 오류: "+hub.regionId);
                foreach(string id in hub.itemIds)
                {
                    var item=ShadowDatabase.Instance?.GetItem(id);
                    if(item==null||!JourneyHubPolicy.AllowsItem(region,item))errors.Add("보급 상품 누락/조건 오류: "+id);
                }
            }
            if(ids.Count!=2||RegionRepository.All.Count(JourneyHubPolicy.HasServices)!=9)errors.Add("후반 거점/마을 서비스 수 오류");
            if(!ItemTradePolicy.TryQuote(true,60,false,0,300,5,out var quote,out _)||quote.goldAfter!=0||quote.ownedAfter!=5)
                errors.Add("일괄 구매 합계/수량 오류");
            if(ItemTradePolicy.TryQuote(false,20,false,1,int.MaxValue,1,out _,out _)||
                ItemTradePolicy.TryQuote(true,1,false,int.MaxValue,100,1,out _,out _))errors.Add("거래 정수 상한 우회");
            var query=new CollectionQuery{search="숨은 이름"};
            if(query.Apply(new[]{new ShadowSearchEntry{name="숨은 이름"}}).Count!=0)errors.Add("도감 미발견 이름 검색 노출");
        }
    }
}
#endif
