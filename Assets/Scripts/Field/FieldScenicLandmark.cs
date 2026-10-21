using System.Collections;
using ShadowTheater.Data;
using ShadowTheater.Save;
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>지역마다 하나씩 놓이는 비전투 명소. 발견 여부와 당시 환경을 수첩용 플래그로 남긴다.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class FieldScenicLandmark : MonoBehaviour, IInteractable
    {
        [SerializeField] private string regionId;
        [SerializeField] private string landmarkName = "낯선 풍경";
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private Color accent = new Color(.75f,.65f,1f,1f);
        private bool _discovered;
        private bool _busy;
        public bool IsDiscovered => _discovered;

        private string Flag => "landmark_discovered_" + regionId;

        private IEnumerator Start()
        {
            while(SaveManager.Current==null)yield return null;
            if(target==null)target=GetComponent<SpriteRenderer>();
            _discovered=SaveManager.HasFlag(Flag);Refresh();
        }

        public void Interact(PlayerController player)
        {
            if(_busy)return;
            RegionData region=RegionRepository.Get(regionId);
            FieldWorldSnapshot world=FieldWorldState.Current(region);
            if(!_discovered)
            {
                _discovered=true;SaveManager.SetFlag(Flag);
                ShadowInstance companion=CompanionBondService.CurrentCompanion();
                if(companion!=null)CompanionBondService.AddExperience(companion.instanceId,3,out _,out _);
                SaveManager.Instance?.TrySave(false);
                SaveFeedbackController.Instance?.ShowMessage(
                    $"명소 발견 · {landmarkName} · {L10n.Text(FieldWorldState.TimeLabel(world.timeOfDay))}의 풍경 · 동행 유대 +3");
            }
            else SaveFeedbackController.Instance?.ShowMessage(
                $"{landmarkName} · {L10n.Text(FieldWorldState.WeatherLabel(world.weather))}의 기척이 머물러 있습니다.");
            GameSettings.TryVibrate();StartCoroutine(React());
        }

        private IEnumerator React()
        {
            _busy=true;
            for(float elapsed=0f;elapsed<.55f;elapsed+=Time.unscaledDeltaTime)
            {
                float pulse=Mathf.Sin(Mathf.Clamp01(elapsed/.55f)*Mathf.PI);
                transform.localScale=Vector3.one*(.72f+pulse*.16f);yield return null;
            }
            transform.localScale=Vector3.one*.72f;_busy=false;Refresh();
        }

        private void Refresh()
        {
            if(target==null)return;
            target.color=_discovered?Color.Lerp(accent,Color.white,.28f):Color.Lerp(accent,Color.black,.32f);
        }
    }
}
