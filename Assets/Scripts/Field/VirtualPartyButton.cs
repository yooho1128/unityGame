using ShadowTheater.UI;
using UnityEngine;
using UnityEngine.EventSystems;
namespace ShadowTheater.Field
{
    public class VirtualPartyButton : MonoBehaviour, IPointerClickHandler
    { public void OnPointerClick(PointerEventData eventData) => PartyStorageController.Instance?.Toggle(); }
}
