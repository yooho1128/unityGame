using ShadowTheater.UI;
using UnityEngine.EventSystems;

namespace ShadowTheater.Field
{
    /// <summary>모바일 필드 메뉴 버튼을 런타임 컨트롤러에 연결한다.</summary>
    public class VirtualPauseButton : UIBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData) => FieldPauseMenuController.Instance?.Toggle();
    }
}
