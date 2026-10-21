using System.Collections;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>한 번 발견하면 영구 기록되고 양방향 지름길로 사용하는 숨은 통로.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class FieldSecretPassage:MonoBehaviour,IInteractable
    {
        [SerializeField]private string regionId;[SerializeField]private Vector2Int destination;
        [SerializeField]private SpriteRenderer target;private bool _revealed;private bool _busy;
        private string Flag=>"secret_passage_"+regionId;
        private IEnumerator Start(){while(SaveManager.Current==null)yield return null;_revealed=SaveManager.HasFlag(Flag);Refresh();}
        public void Interact(PlayerController player)
        {
            if(_busy||player==null)return;
            _revealed=SaveManager.HasFlag(Flag);Refresh();
            if(!_revealed)
            {
                _revealed=true;SaveManager.SetFlag(Flag);SaveManager.Instance?.TrySave(false);Refresh();GameSettings.TryVibrate();
                SaveFeedbackController.Instance?.ShowMessage("숨겨진 통로를 발견했습니다 · 월드 탐험 기록에 영구 표시됩니다.");return;
            }
            StartCoroutine(Travel(player));
        }
        private IEnumerator Travel(PlayerController player)
        {
            _busy=true;player.Lock();ScreenFader fader=ScreenFader.Instance;if(fader!=null)yield return fader.FadeOut(.18f);
            if(!player.SnapToSafeCell(destination,player.Cell,out _))SaveFeedbackController.Instance?.ShowMessage("통로 반대편이 막혀 있습니다.");
            FieldCameraFollow.Instance?.Snap();if(fader!=null)yield return fader.FadeIn(.2f);player.Unlock();_busy=false;
        }
        private void Refresh(){if(target!=null)target.color=_revealed?new Color(.7f,.82f,1f,.86f):new Color(.24f,.2f,.3f,.62f);}
    }
}
