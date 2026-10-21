using System.Collections;
using ShadowTheater.Data;
using ShadowTheater.Save;
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>지역 시간 기준 하루에 한 번 도구를 얻는 비전투 채집 지점.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class FieldForageNode : MonoBehaviour, IInteractable
    {
        [SerializeField] private string regionId;
        [SerializeField] private string itemId = "salve";
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private Color readyColor = new Color(.45f,.85f,.48f,1f);
        private bool _busy;
        private int _day;
        private bool _available;
        private string Flag => "forage_day_" + regionId;

        private IEnumerator Start()
        {
            while(SaveManager.Current==null)yield return null;
            if(target==null)target=GetComponent<SpriteRenderer>();Refresh();
        }

        private void Refresh()
        {
            RegionData region=RegionRepository.Get(regionId);
            _day=FieldWorldState.Current(region).dayIndex;
            _available=SaveManager.GetFlag(Flag,0)!=_day+1;
            if(target!=null)target.color=_available?readyColor:Color.Lerp(readyColor,Color.gray,.72f);
        }

        public void Interact(PlayerController player)
        {
            if(_busy)return;Refresh();
            if(!_available)
            {
                SaveFeedbackController.Instance?.ShowMessage("오늘 채집할 수 있는 것은 모두 살펴보았습니다.");return;
            }
            StartCoroutine(Gather());
        }

        private IEnumerator Gather()
        {
            _busy=true;GameSettings.TryVibrate();Vector3 home=transform.localScale;
            for(float elapsed=0f;elapsed<.42f;elapsed+=Time.unscaledDeltaTime)
            {
                float t=Mathf.Clamp01(elapsed/.42f),wave=Mathf.Sin(t*Mathf.PI);
                transform.localScale=Vector3.Scale(home,new Vector3(1f+wave*.18f,1f-wave*.28f,1f));yield return null;
            }
            transform.localScale=home;SaveManager.AddItem(itemId,1);SaveManager.SetFlag(Flag,_day+1);
            SaveManager.Instance?.TrySave(false);Refresh();_busy=false;
            ItemData item=ShadowDatabase.Instance?.GetItem(itemId);
            SaveFeedbackController.Instance?.ShowMessage($"채집 완료 · {(item!=null?L10n.Text(item.displayName):itemId)} x1");
        }
    }
}
