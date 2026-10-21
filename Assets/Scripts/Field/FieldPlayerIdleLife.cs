using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>조작이 없을 때 플레이어가 가끔 주변을 살피고 몸을 푸는 짧은 대기 행동을 요청한다.</summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class FieldPlayerIdleLife : MonoBehaviour
    {
        [SerializeField] private Vector2 interval=new Vector2(5.5f,9f);
        private PlayerController _player;
        private PixelCharacterMotion _motion;
        private float _nextAt;

        private void Awake(){_player=GetComponent<PlayerController>();_motion=GetComponentInChildren<PixelCharacterMotion>();}
        private void OnEnable()
        {
            if(_player==null)_player=GetComponent<PlayerController>();
            if(_player!=null){_player.OnStepStarted+=ResetTimer;_player.OnInteractionStarted+=ResetTimer;}
            Schedule();
        }
        private void OnDisable()
        {
            if(_player!=null){_player.OnStepStarted-=ResetTimer;_player.OnInteractionStarted-=ResetTimer;}
        }
        private void Update()
        {
            if(_player==null||_player.IsMoving||_player.IsLocked){Schedule();return;}
            if(Time.time<_nextAt)return;
            _motion?.PlayIdleGesture((_player.CompletedSteps+GetInstanceID())&1);Schedule();
        }
        private void ResetTimer(Vector2Int _,Vector2Int __)=>Schedule();
        private void ResetTimer(Transform _)=>Schedule();
        private void Schedule()=>_nextAt=Time.time+Random.Range(interval.x,interval.y);
    }
}
