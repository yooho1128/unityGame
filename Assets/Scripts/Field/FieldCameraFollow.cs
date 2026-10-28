using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>타일 이동을 부드럽게 따라가며 맵 바깥을 비추지 않는 2D 카메라.</summary>
    public class FieldCameraFollow : MonoBehaviour
    {
        public static FieldCameraFollow Instance { get; private set; }
        [SerializeField] private Transform target;
        [SerializeField, Min(0.01f)] private float smoothTime = 0.12f;
        [SerializeField] private bool clampToBounds = true;
        [SerializeField] private Vector2 minBounds = new Vector2(-10f, -8f);
        [SerializeField] private Vector2 maxBounds = new Vector2(10f, 8f);
        [SerializeField] private bool pixelSnap;
        [SerializeField, Min(1f)] private float pixelsPerUnit = 32f;
        [SerializeField, Range(0f, 1f)] private float lookAheadDistance = .38f;
        [SerializeField, Min(.01f)] private float lookAheadSmoothTime = .18f;
        private Vector3 _velocity;
        private Vector2 _lookAhead;
        private Vector2 _lookVelocity;
        private Vector2 _impulse;
        private Vector3 _focusPoint;
        private float _focusUntil;
        private float _focusWeight;
        private Camera _camera;
        private float _baseSize;

        private void Awake()
        {
            Instance = this;
            _camera = GetComponent<Camera>();
            _baseSize = _camera != null ? _camera.orthographicSize : 6.5f;
        }

        private void Start()
        {
            if (target == null && PlayerController.Instance != null) target = PlayerController.Instance.transform;
            // Legacy generated scenes clamp Y to ±2, hiding the north exit at Y=8 with a 5.5 camera.
            // Upgrade those known limits as well as generating corrected bounds for new scenes.
            if (clampToBounds && Mathf.Approximately(minBounds.y, -2f) && Mathf.Approximately(maxBounds.y, 2f))
            { minBounds.y = -4.5f; maxBounds.y = 4.5f; }
            Snap();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                if (PlayerController.Instance == null) return;
                target = PlayerController.Instance.transform;
            }
            UpdateLookAhead();
            Vector3 desired = TargetPosition();
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
            if (pixelSnap)
            {
                Vector3 snapped = transform.position;
                snapped.x = Mathf.Round(snapped.x * pixelsPerUnit) / pixelsPerUnit;
                snapped.y = Mathf.Round(snapped.y * pixelsPerUnit) / pixelsPerUnit;
                transform.position = snapped;
            }
            _impulse = Vector2.Lerp(_impulse, Vector2.zero, 1f-Mathf.Exp(-Time.unscaledDeltaTime*12f));
            _focusWeight = Mathf.MoveTowards(_focusWeight, Time.unscaledTime < _focusUntil ? 1f : 0f,
                Time.unscaledDeltaTime * (Time.unscaledTime < _focusUntil ? 5f : 3f));
            if (_camera != null)
            {
                float speedZoom = PlayerController.Instance != null && PlayerController.Instance.IsMoving ? .12f : 0f;
                float desiredSize = _baseSize - _focusWeight * .22f + speedZoom;
                _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, desiredSize,
                    1f-Mathf.Exp(-Time.unscaledDeltaTime*6f));
            }
        }

        public void FocusOn(Vector3 world, float duration = .45f)
        {
            _focusPoint = world; _focusUntil = Time.unscaledTime + Mathf.Max(.1f,duration);
        }

        public void Nudge(Vector2 amount) => _impulse += Vector2.ClampMagnitude(amount,.12f);

        public void Snap()
        {
            if (target != null)
            {
                var player = target.GetComponent<PlayerController>();
                _lookAhead = player != null
                    ? (Vector2)FieldGrid.DirToVector(player.Facing) * lookAheadDistance * .35f : Vector2.zero;
                transform.position = TargetPosition();
            }
        }

        private void UpdateLookAhead()
        {
            var player = target != null ? target.GetComponent<PlayerController>() : null;
            Vector2 desired = Vector2.zero;
            if (player != null)
            {
                float weight = player.IsMoving ? 1f : .35f;
                desired = (Vector2)FieldGrid.DirToVector(player.Facing) * lookAheadDistance * weight;
            }
            _lookAhead = Vector2.SmoothDamp(_lookAhead, desired, ref _lookVelocity, lookAheadSmoothTime);
        }

        private Vector3 TargetPosition()
        {
            Vector3 anchor = Vector3.Lerp(target.position, _focusPoint, _focusWeight * .34f);
            float x = anchor.x + _lookAhead.x + _impulse.x;
            float y = anchor.y + _lookAhead.y + _impulse.y;
            if (clampToBounds)
            {
                x = Mathf.Clamp(x, minBounds.x, maxBounds.x);
                y = Mathf.Clamp(y, minBounds.y, maxBounds.y);
            }
            return new Vector3(x, y, transform.position.z);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
