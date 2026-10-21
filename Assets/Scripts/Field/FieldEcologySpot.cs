using System.Collections;
using ShadowTheater.Save;
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    public enum EcologyKind{Fishing,Insect,Bird,Plant}

    /// <summary>전투 없이 지역의 물고기·곤충·새·식생을 관찰해 생태 기록을 채운다.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class FieldEcologySpot:MonoBehaviour,IInteractable
    {
        [SerializeField]private string regionId;[SerializeField]private EcologyKind kind;
        [SerializeField]private SpriteRenderer target;private bool _busy;
        private string Flag=>"ecology_recorded_"+regionId;
        public void Interact(PlayerController player)
        {
            if(_busy)return;if(SaveManager.HasFlag(Flag))
            {SaveFeedbackController.Instance?.ShowMessage("이미 수첩에 기록한 생태 흔적입니다 · "+Observation());return;}
            StartCoroutine(Observe(player));
        }
        private IEnumerator Observe(PlayerController player)
        {
            _busy=true;player?.Lock();Color home=target!=null?target.color:Color.white;
            for(float elapsed=0f;elapsed<1.05f;elapsed+=Time.unscaledDeltaTime)
            {float t=Mathf.Clamp01(elapsed/1.05f),pulse=Mathf.Sin(t*Mathf.PI*3f)*Mathf.Sin(t*Mathf.PI);if(target!=null)target.color=Color.Lerp(home,Color.white,pulse*.45f);yield return null;}
            SaveManager.SetFlag(Flag);RegionData region=RegionRepository.Get(regionId);int reward=10+Mathf.Max(1,region?.act??1)*4;
            SaveManager.AddGold(reward);SaveManager.Instance?.TrySave(false);player?.Unlock();_busy=false;GameSettings.TryVibrate();
            SaveFeedbackController.Instance?.ShowMessage($"생태 기록 추가 · {KindLabel()} · 금화 +{reward}");
        }
        private string KindLabel()=>kind==EcologyKind.Fishing?"물가 생물":kind==EcologyKind.Insect?"희귀 곤충":kind==EcologyKind.Bird?"지역 새":"고유 식생";
        private string Observation()
        {
            RegionData region=RegionRepository.Get(regionId);FieldWorldSnapshot world=FieldWorldState.Current(region);
            return $"{L10n.Text(FieldWorldState.TimeLabel(world.timeOfDay))} · {L10n.Text(FieldWorldState.WeatherLabel(world.weather))}";
        }
    }
}
