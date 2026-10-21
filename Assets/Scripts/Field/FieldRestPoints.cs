using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;

namespace ShadowTheater.Field
{
    public static class FieldRestService
    {
        public static bool TryRest(string regionId,bool camp,out string message)
        {
            RegionData region=RegionRepository.Get(regionId);int day=FieldWorldState.Current(region).dayIndex;
            string flag=(camp?"camp_rest_":"bench_rest_")+regionId;
            if(SaveManager.GetFlag(flag,0)==day+1){message="오늘은 이미 이곳에서 충분히 쉬었습니다.";return false;}
            SaveManager.SetFlag(flag,day+1);SaveManager.HealParty();
            SaveManager.AdvancePlayTime(FieldWorldState.SegmentSeconds*(camp?1f:.25f));
            SaveManager.Instance?.TrySave(false);message=camp?"모닥불 곁에서 막간을 보냈습니다 · 파티 회복 · 시간 경과":"잠시 앉아 숨을 골랐습니다 · 파티 회복";return true;
        }
    }
}
