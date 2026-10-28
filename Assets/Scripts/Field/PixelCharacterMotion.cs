using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>
    /// 격자 좌표와 충돌체는 그대로 두고 시각 몸체에만 보폭, 체중 이동, 착지를 표현한다.
    /// 두 장짜리 픽셀 캐릭터도 판넬처럼 미끄러지지 않게 만드는 보조 모션이다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class PixelCharacterMotion : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField] private SpriteRenderer target;
        [SerializeField, Range(0f, .08f)] private float stepLift = .035f;
        [SerializeField, Range(0f, .08f)] private float weightShift = .022f;
        [SerializeField, Range(0f, 6f)] private float leanDegrees = 2.4f;
        [SerializeField, Range(0f, .08f)] private float landingSquash = .035f;

        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private float _gestureStarted;
        private float _gestureUntil;
        private int _gestureKind;
        private FacingDir _lastFacing;
        private float _turnStarted = -10f;

        public void PlayIdleGesture(int kind)
        {
            if(controller==null||controller.IsMoving)return;
            _gestureKind=Mathf.Abs(kind)%2;_gestureStarted=Time.time;_gestureUntil=Time.time+1.05f;
        }

        public void Configure(PlayerController owner, SpriteRenderer renderer)
        {
            controller = owner;
            target = renderer;
            CacheRestPose();
        }

        private void Awake()
        {
            if (controller == null) controller = GetComponentInParent<PlayerController>();
            if (target == null) target = GetComponent<SpriteRenderer>();
            if (controller != null) _lastFacing = controller.Facing;
            CacheRestPose();
        }

        private void CacheRestPose()
        {
            _restPosition = transform.localPosition;
            _restRotation = transform.localRotation;
        }

        private void LateUpdate()
        {
            if (controller == null || target == null) return;
            if (controller.Facing != _lastFacing)
            {
                _lastFacing = controller.Facing;
                _turnStarted = Time.time;
            }
            Vector3 fittedScale = transform.localScale; // PixelFieldAnimator가 먼저 맞춘 크기
            if (!controller.IsMoving)
            {
                if(Time.time<_gestureUntil)
                {
                    float g=Mathf.Clamp01((Time.time-_gestureStarted)/1.05f),wave=Mathf.Sin(g*Mathf.PI);
                    if(_gestureKind==0)
                    {
                        transform.localPosition=_restPosition+Vector3.right*Mathf.Sin(g*Mathf.PI*2f)*.018f;
                        transform.localRotation=_restRotation*Quaternion.Euler(0f,0f,Mathf.Sin(g*Mathf.PI*2f)*2.6f);
                        transform.localScale=Vector3.Scale(fittedScale,new Vector3(1f-wave*.025f,1f+wave*.035f,1f));
                    }
                    else
                    {
                        transform.localPosition=_restPosition+Vector3.up*(wave*.025f);
                        transform.localRotation=_restRotation*Quaternion.Euler(0f,0f,-wave*1.4f);
                        transform.localScale=Vector3.Scale(fittedScale,new Vector3(1f-wave*.035f,1f+wave*.065f,1f));
                    }
                    return;
                }
                float turnAge = Time.time - _turnStarted;
                if (turnAge < .16f)
                {
                    float turn = Mathf.Sin(Mathf.Clamp01(turnAge / .16f) * Mathf.PI);
                    float turnDirection = _lastFacing == FacingDir.Left ? -1f : _lastFacing == FacingDir.Right ? 1f : 0f;
                    transform.localPosition = _restPosition + Vector3.right * (turnDirection * turn * .014f);
                    transform.localRotation = _restRotation * Quaternion.Euler(0f, 0f, -turnDirection * turn * 2.2f);
                    transform.localScale = Vector3.Scale(fittedScale,
                        new Vector3(1f + turn * .045f, 1f - turn * .035f, 1f));
                    return;
                }
                float breath = Mathf.Sin(Time.time * 2.1f) * .004f;
                transform.localPosition = _restPosition;
                transform.localRotation = Quaternion.Slerp(transform.localRotation, _restRotation, .28f);
                transform.localScale = Vector3.Scale(fittedScale, new Vector3(1f - breath * .35f, 1f + breath, 1f));
                return;
            }

            float t = controller.StepProgress;
            float footArc = Mathf.Sin(t * Mathf.PI);
            float stride = Mathf.Sin(t * Mathf.PI * 2f + (controller.CompletedSteps & 1) * Mathf.PI);
            float contact = Mathf.Pow(Mathf.Abs(Mathf.Cos(t * Mathf.PI)), 7f);
            Vector2Int direction = FieldGrid.DirToVector(controller.Facing);
            Vector3 offset = Vector3.up * (footArc * stepLift);
            if (direction.y != 0) offset += Vector3.right * (stride * weightShift);

            float leanSign = direction.x != 0 ? direction.x : stride;
            transform.localPosition = _restPosition + offset;
            transform.localRotation = _restRotation * Quaternion.Euler(0f, 0f, -leanSign * leanDegrees * footArc);
            float squash = landingSquash * contact;
            transform.localScale = Vector3.Scale(fittedScale,
                new Vector3(1f + squash * .55f, 1f - squash, 1f));
        }

        private void OnDisable()
        {
            transform.localPosition = _restPosition;
            transform.localRotation = _restRotation;
            _gestureUntil=0f;
        }
    }
}
