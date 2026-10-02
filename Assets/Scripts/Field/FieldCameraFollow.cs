using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>타일 이동을 부드럽게 따라가며 맵 바깥을 비추지 않는 2D 카메라.</summary>
    public class FieldCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0.01f)] private float smoothTime = 0.12f;
        [SerializeField] private bool clampToBounds = true;
        [SerializeField] private Vector2 minBounds = new Vector2(-10f, -8f);
        [SerializeField] private Vector2 maxBounds = new Vector2(10f, 8f);
        [SerializeField] private bool pixelSnap;
        [SerializeField, Min(1f)] private float pixelsPerUnit = 32f;
        private Vector3 _velocity;

        private void Start()
        {
            if (target == null && PlayerController.Instance != null) target = PlayerController.Instance.transform;
            Snap();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                if (PlayerController.Instance == null) return;
                target = PlayerController.Instance.transform;
            }
            Vector3 desired = TargetPosition();
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
            if (pixelSnap)
            {
                Vector3 snapped = transform.position;
                snapped.x = Mathf.Round(snapped.x * pixelsPerUnit) / pixelsPerUnit;
                snapped.y = Mathf.Round(snapped.y * pixelsPerUnit) / pixelsPerUnit;
                transform.position = snapped;
            }
        }

        public void Snap()
        {
            if (target != null) transform.position = TargetPosition();
        }

        private Vector3 TargetPosition()
        {
            float x = target.position.x;
            float y = target.position.y;
            if (clampToBounds)
            {
                x = Mathf.Clamp(x, minBounds.x, maxBounds.x);
                y = Mathf.Clamp(y, minBounds.y, maxBounds.y);
            }
            return new Vector3(x, y, transform.position.z);
        }
    }
}
