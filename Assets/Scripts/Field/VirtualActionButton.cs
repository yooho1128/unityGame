using UnityEngine;
using UnityEngine.EventSystems;

namespace ShadowTheater.Field
{
    /// <summary>[A] 상호작용 버튼 (NPC 대화, 조사)</summary>
    public class VirtualActionButton : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData e)
        {
            if (PlayerController.Instance != null) PlayerController.Instance.TryInteract();
        }
    }
}
