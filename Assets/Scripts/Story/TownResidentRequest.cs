using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>게임 내 하루마다 작은 도구 부탁을 갱신하고 완료 횟수를 주민 호감도로 기록한다.</summary>
    public sealed class TownResidentRequest:MonoBehaviour,IInteractable
    {
        [SerializeField]private string regionId;[SerializeField]private string requestedItemId="salve";
        [SerializeField,Min(1)]private int rewardGold=40;
        public void Interact(PlayerController player)
        {
            RegionData region=RegionRepository.Get(regionId);int day=FieldWorldState.Current(region).dayIndex;
            string daily="resident_request_day_"+regionId,affinity="resident_affinity_"+regionId;
            ItemData item=ShadowDatabase.Instance?.GetItem(requestedItemId);string itemName=item!=null?L10n.Text(item.displayName):requestedItemId;
            if(SaveManager.GetFlag(daily,0)==day+1)
            {SaveFeedbackController.Instance?.ShowMessage($"오늘 부탁은 이미 해결됐습니다 · 주민 호감도 {SaveManager.GetFlag(affinity)}");return;}
            if(SaveManager.GetItemCount(requestedItemId)<=0)
            {SaveFeedbackController.Instance?.ShowMessage($"주민 부탁 · {itemName} 1개를 구해 주세요.");return;}
            SaveManager.AddItem(requestedItemId,-1);SaveManager.AddGold(rewardGold);SaveManager.SetFlag(daily,day+1);
            SaveManager.SetFlag(affinity,SaveValuePolicy.SaturatingAdd(SaveManager.GetFlag(affinity),1));
            TownRestorationService.AddActivity(regionId,1);SaveManager.Instance?.TrySave(false);GameSettings.TryVibrate();
            SaveFeedbackController.Instance?.ShowMessage($"부탁 완료 · 금화 +{rewardGold} · 주민 호감도 {SaveManager.GetFlag(affinity)}");
        }
    }
}
