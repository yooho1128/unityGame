using System.Collections;
using System.Linq;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>빈 곳에서 조사하면 가장 가까운 명소나 출구 방향을 짧게 알려 주는 길 감각.</summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class FieldTrailGuide : MonoBehaviour
    {
        private SpriteRenderer _arrow;
        private Coroutine _routine;

        private void Awake()
        {
            var go=new GameObject("TrailGuideArrow",typeof(SpriteRenderer));go.transform.SetParent(transform,false);
            go.transform.localPosition=new Vector3(0f,1.18f,0f);_arrow=go.GetComponent<SpriteRenderer>();
            _arrow.sprite=BattleFxSpriteLibrary.Get(BattleFxGlyph.Rune);_arrow.color=new Color(.68f,.92f,1f,0f);
            _arrow.sortingOrder=315;
        }

        public void ShowHint()
        {
            if(TownInteriorController.Instance?.IsInside==true)
            {
                SaveFeedbackController.Instance?.ShowMessage("방 아래쪽에 바깥으로 돌아가는 문이 있습니다.");
                if(_routine!=null)StopCoroutine(_routine);_routine=StartCoroutine(ShowArrow(Vector2.down));return;
            }
            Transform target=FindObjectsOfType<FieldScenicLandmark>().Where(x=>!x.IsDiscovered).Select(x=>x.transform)
                .Concat(FindObjectsOfType<MapPortal>().Select(x=>x.transform))
                .OrderBy(x=>(x.position-transform.position).sqrMagnitude).FirstOrDefault();
            if(target==null)return;
            Vector2 delta=target.position-transform.position;
            string direction=Mathf.Abs(delta.x)>Mathf.Abs(delta.y)?(delta.x>0?"동쪽":"서쪽"):(delta.y>0?"북쪽":"남쪽");
            string kind=target.GetComponent<FieldScenicLandmark>()!=null?"기록하지 않은 풍경":"지역 출구";
            string distance=delta.sqrMagnitude<16f?"가까운 곳":"조금 떨어진 곳";
            SaveFeedbackController.Instance?.ShowMessage($"{distance} {direction}에 {kind}의 기척이 있습니다.");
            FieldCameraFollow.Instance?.Nudge(delta.normalized*.04f);
            if(_routine!=null)StopCoroutine(_routine);_routine=StartCoroutine(ShowArrow(delta));
        }

        private IEnumerator ShowArrow(Vector2 direction)
        {
            float angle=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg-90f;
            _arrow.transform.localRotation=Quaternion.Euler(0f,0f,angle);
            for(float elapsed=0f;elapsed<.9f;elapsed+=Time.unscaledDeltaTime)
            {
                float t=Mathf.Clamp01(elapsed/.9f);Color c=_arrow.color;c.a=Mathf.Sin(t*Mathf.PI);_arrow.color=c;
                _arrow.transform.localScale=Vector3.one*(.16f+Mathf.Sin(t*Mathf.PI)*.05f);yield return null;
            }
            Color hidden=_arrow.color;hidden.a=0f;_arrow.color=hidden;_routine=null;
        }
    }
}
