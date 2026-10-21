using System.Collections;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class FieldCampfire:MonoBehaviour,IInteractable
    {
        [SerializeField]private string regionId;[SerializeField]private SpriteRenderer target;private bool _busy;
        public void Interact(PlayerController player)
        {
            if(_busy)return;bool rested=FieldRestService.TryRest(regionId,true,out string message);
            SaveFeedbackController.Instance?.ShowMessage(message);if(rested)StartCoroutine(Glow());
        }
        private IEnumerator Glow()
        {
            _busy=true;GameSettings.TryVibrate();Color home=target!=null?target.color:Color.white;
            for(float elapsed=0f;elapsed<.8f;elapsed+=Time.unscaledDeltaTime)
            {float p=Mathf.Sin(Mathf.Clamp01(elapsed/.8f)*Mathf.PI);if(target!=null)target.color=Color.Lerp(home,new Color(1f,.82f,.3f),p);yield return null;}
            if(target!=null)target.color=home;_busy=false;
        }
    }
}
