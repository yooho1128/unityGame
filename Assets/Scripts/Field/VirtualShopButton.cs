using ShadowTheater.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShadowTheater.Field
{
    public class VirtualShopButton : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData) => SettlementShopController.Instance?.Toggle();
    }
}
