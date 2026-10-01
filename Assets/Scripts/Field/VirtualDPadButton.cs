using UnityEngine;
using UnityEngine.EventSystems;

namespace ShadowTheater.Field
{
    /// <summary>
    /// 모바일용 4방향 가상 패드 버튼. 상/하/좌/우 UI Image 4개에 각각 붙이고 direction만 지정.
    /// 누르고 있는 동안 PlayerController.MoveInput에 방향을 넣는다.
    /// (조이스틱 에셋으로 바꿀 경우 MoveInput에 값만 넣어주면 됨)
    /// </summary>
    public class VirtualDPadButton : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Vector2 direction = Vector2.up;

        private static VirtualDPadButton _pressed;

        public void OnPointerDown(PointerEventData e) => Press();
        public void OnPointerUp(PointerEventData e) => Release();
        public void OnPointerExit(PointerEventData e) => Release();

        // 손가락을 떼지 않고 다른 방향 버튼으로 미끄러질 때
        public void OnPointerEnter(PointerEventData e)
        {
            if (e.dragging || e.pointerPress != null) Press();
        }

        private void Press()
        {
            _pressed = this;
            if (PlayerController.Instance != null) PlayerController.Instance.MoveInput = direction;
        }

        private void Release()
        {
            if (_pressed != this) return;
            _pressed = null;
            if (PlayerController.Instance != null) PlayerController.Instance.MoveInput = Vector2.zero;
        }

        private void OnDisable() => Release();
    }
}
