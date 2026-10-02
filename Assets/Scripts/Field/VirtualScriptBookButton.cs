using ShadowTheater.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShadowTheater.Field
{
    /// <summary>필드 HUD의 각본집 버튼.</summary>
    public class VirtualScriptBookButton : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData) => ScriptBookController.Instance?.Toggle();
    }
}
