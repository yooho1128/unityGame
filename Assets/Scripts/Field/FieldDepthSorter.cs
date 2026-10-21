using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>화면 아래에 있는 물체를 앞으로 그려 캐릭터와 소품이 자연스럽게 서로 가리도록 한다.</summary>
    [DisallowMultipleComponent]
    public sealed class FieldDepthSorter : MonoBehaviour
    {
        private const int BaseOrder = 200;
        private const float OrdersPerWorldUnit = 4f;

        [SerializeField] private Renderer target;
        [SerializeField] private int orderOffset;
        [SerializeField, Min(.001f)] private float refreshDistance = .015f;
        private float _lastY = float.PositiveInfinity;

        public static int OrderAt(float worldY, int offset = 0) =>
            BaseOrder - Mathf.RoundToInt(worldY * OrdersPerWorldUnit) + offset;

        public void Configure(Renderer renderer, int offset = 0)
        {
            target = renderer;
            orderOffset = offset;
            Refresh(true);
        }

        private void Awake()
        {
            if (target == null) target = GetComponentInChildren<Renderer>();
            Refresh(true);
        }

        private void LateUpdate() => Refresh(false);

        private void Refresh(bool force)
        {
            if (target == null) return;
            float y = transform.position.y;
            if (!force && Mathf.Abs(y - _lastY) < refreshDistance) return;
            _lastY = y;
            target.sortingOrder = OrderAt(y, orderOffset);
        }
    }
}
