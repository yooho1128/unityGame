using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ShadowTheater.Story;

namespace ShadowTheater.Field
{
    /// <summary>바라보는 대상에 따라 대화/조사를 표시하는 터치 버튼.</summary>
    public class VirtualActionButton : MonoBehaviour, IPointerClickHandler
    {
        private Button _button;
        private Text _label;
        private void Awake() { _button = GetComponent<Button>(); _label = GetComponentInChildren<Text>(); }
        private void Update()
        {
            var player = PlayerController.Instance;
            bool ready = player != null && !player.IsLocked && !player.IsMoving;
            IInteractable target = null;
            bool available = ready && player.TryGetInteraction(out target);
            if (_button != null) _button.interactable = available;
            if (_label != null) _label.text = !available ? "대화 · 조사" : target is StoryNpc || target is TownHealerNpc ||
                target is TownRumorNpc || target is BossInvestigatorNpc || target is TownRecoveryNpc ? "대화" : "조사";
        }
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && (_button == null || _button.interactable))
                PlayerController.Instance?.TryInteract();
        }
    }
}
