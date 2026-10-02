using ShadowTheater.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShadowTheater.Field
{
    public class VirtualWorldMapButton : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData) => WorldMapController.Instance?.Toggle();
    }
}
