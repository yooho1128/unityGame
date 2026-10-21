using System.Collections;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>레버를 당겨 무너진 길이나 장애물을 영구적으로 복구하는 짧은 환경 퍼즐.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class FieldLeverBridgePuzzle : MonoBehaviour, IInteractable
    {
        [SerializeField] private string puzzleId;
        [SerializeField] private SpriteRenderer leverRenderer;
        [SerializeField] private Transform barrier;
        [SerializeField] private SpriteRenderer barrierRenderer;
        [SerializeField] private Collider2D barrierCollider;
        [SerializeField] private Color accent = new Color(.75f,.55f,.25f,1f);
        private bool _solved;
        private bool _busy;
        private Vector3 _barrierHome;
        private string Flag => "field_lever_" + puzzleId + "_complete";

        private IEnumerator Start()
        {
            while(SaveManager.Current==null)yield return null;
            if(barrier!=null)_barrierHome=barrier.localPosition;
            _solved=SaveManager.HasFlag(Flag);ApplySolvedState(true);
        }

        public void Interact(PlayerController player)
        {
            if(_busy)return;
            if(_solved)
            {
                SaveFeedbackController.Instance?.ShowMessage("레버가 내려가 있습니다. 복구된 길은 단단합니다.");
                return;
            }
            StartCoroutine(Solve(player));
        }

        private IEnumerator Solve(PlayerController player)
        {
            _busy=true;player?.Lock();GameSettings.TryVibrate();
            Quaternion from=transform.localRotation,to=Quaternion.Euler(0f,0f,-28f);
            Color barrierColor=barrierRenderer!=null?barrierRenderer.color:Color.white;
            for(float elapsed=0f;elapsed<.52f;elapsed+=Time.unscaledDeltaTime)
            {
                float t=Mathf.Clamp01(elapsed/.52f),ease=t*t*(3f-2f*t);
                transform.localRotation=Quaternion.Lerp(from,to,ease);
                if(barrier!=null)barrier.localPosition=_barrierHome+Vector3.down*(ease*.42f);
                if(barrierRenderer!=null){Color c=barrierColor;c.a=1f-ease*.78f;barrierRenderer.color=c;}
                yield return null;
            }
            _solved=true;SaveManager.SetFlag(Flag);ApplySolvedState(false);
            SaveManager.Instance?.TrySave(false);player?.Unlock();_busy=false;
            FieldCameraFollow.Instance?.Nudge(Vector2.up*.035f);
            SaveFeedbackController.Instance?.ShowMessage("레버를 당겨 막힌 길을 복구했습니다.");
        }

        private void ApplySolvedState(bool immediate)
        {
            if(leverRenderer!=null)leverRenderer.color=_solved?Color.Lerp(accent,Color.white,.35f):accent;
            if(!_solved)return;
            transform.localRotation=Quaternion.Euler(0f,0f,-28f);
            if(barrierCollider!=null)barrierCollider.enabled=false;
            if(barrierRenderer!=null)
            {
                Color c=barrierRenderer.color;c.a=.18f;barrierRenderer.color=c;
            }
            if(barrier!=null&&immediate)barrier.localPosition=_barrierHome+Vector3.down*.42f;
        }
    }
}
