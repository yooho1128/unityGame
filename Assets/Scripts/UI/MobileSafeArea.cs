using UnityEngine;

namespace ShadowTheater.UI
{
    /// <summary>
    /// 전체 화면 Canvas의 자식 RectTransform을 기기의 노치/둥근 모서리 안전영역에 맞춘다.
    /// Game 뷰 해상도를 바꾸는 경우에도 즉시 다시 계산한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobileSafeArea : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        private void Awake()
        {
            _rect = transform as RectTransform;
            Apply();
        }

        private void OnEnable() => Apply();

        private void Update()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea != _lastSafeArea || size != _lastScreenSize) Apply();
        }

        private void Apply()
        {
            if (_rect == null) _rect = transform as RectTransform;
            if (_rect == null || Screen.width <= 0 || Screen.height <= 0) return;

            Rect area = Screen.safeArea;
            _lastSafeArea = area;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);

            Vector2 min = area.position;
            Vector2 max = area.position + area.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;
            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
