using System.Collections;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>플레이어가 바라보는 방향으로 한 칸씩 밀어 길과 간단한 환경 퍼즐을 만드는 상자.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class FieldPushableCrate:MonoBehaviour,IInteractable
    {
        private Vector2Int _cell;private bool _moving;private Collider2D _collider;
        private void Start(){_collider=GetComponent<Collider2D>();if(FieldGrid.Current!=null){_cell=FieldGrid.Current.WorldToCell(transform.position);transform.position=FieldGrid.Current.CellToWorld(_cell);}}
        public void Interact(PlayerController player)
        {
            if(_moving||player==null||FieldGrid.Current==null)return;
            Vector2Int direction=FieldGrid.DirToVector(player.Facing),target=_cell+direction;
            if(FieldGrid.Current.IsBlocked(target,_collider))
            {FieldCameraFollow.Instance?.Nudge(-(Vector2)direction*.035f);SaveFeedbackController.Instance?.ShowMessage("더는 밀 수 없습니다.");return;}
            StartCoroutine(Push(player,target));
        }
        private IEnumerator Push(PlayerController player,Vector2Int target)
        {
            _moving=true;player.Lock();GameSettings.TryVibrate();Vector3 from=transform.position,to=FieldGrid.Current.CellToWorld(target);
            for(float elapsed=0f;elapsed<.22f;elapsed+=Time.deltaTime)
            {float t=Mathf.Clamp01(elapsed/.22f),ease=t*t*(3f-2f*t);transform.position=Vector3.Lerp(from,to,ease);yield return null;}
            transform.position=to;_cell=target;FieldTraversalEffects.Instance?.PlayWorldStep(target,.72f);
            player.Unlock();_moving=false;
        }
    }
}
