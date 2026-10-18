using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ShadowTheater.Story;
using ShadowTheater.UI;

namespace ShadowTheater.Field
{
    /// <summary>바라보는 대상에 따라 대화/조사를 표시하는 터치 버튼.</summary>
    public class VirtualActionButton : MonoBehaviour, IPointerClickHandler
    {
        private Button _button;
        private Text _label;
        private GameObject _destinationHint;
        private Text _destinationText;
        private void Awake() { _button = GetComponent<Button>(); _label = GetComponentInChildren<Text>(); }
        private void Update()
        {
            var player = PlayerController.Instance;
            bool ready = player != null && !player.IsLocked && !player.IsMoving;
            IInteractable target = null;
            bool available = ready && player.TryGetInteraction(out target);
            if (_button != null) _button.interactable = available;
            if (_label != null) _label.text = !available ? L10n.Get("field.action_idle", "대화 · 조사") :
                target is MapPortal ? L10n.Get("field.action_travel", "이동") :
                target is StoryNpc || target is TownHealerNpc || target is TownRumorNpc || target is BossInvestigatorNpc || target is TownRecoveryNpc
                    ? L10n.Get("field.action_talk", "대화") : L10n.Get("field.action_inspect", "조사");
            if (available && target is MapPortal portal)
            {
                EnsureDestinationHint();
                _destinationText.text = L10n.Format("field.exit_destination", "{0} 방면", portal.DestinationName);
                _destinationHint.SetActive(true);
            }
            else if (_destinationHint != null) _destinationHint.SetActive(false);
        }

        private void EnsureDestinationHint()
        {
            if (_destinationHint != null) return;
            _destinationHint = new GameObject("ExitDestination", typeof(RectTransform), typeof(Image));
            var rect = _destinationHint.GetComponent<RectTransform>(); rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = Vector2.one; rect.pivot = new Vector2(1,0);
            rect.sizeDelta = new Vector2(320,64); rect.anchoredPosition = new Vector2(0,8);
            var background = _destinationHint.GetComponent<Image>();
            background.sprite = Resources.Load<Sprite>("Art/UI/ControlPlate");
            background.color = new Color(.12f,.07f,.22f,.94f); background.raycastTarget = false;
            var textObject = new GameObject("Destination", typeof(RectTransform), typeof(Text));
            var textRect = textObject.GetComponent<RectTransform>(); textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12,6); textRect.offsetMax = new Vector2(-12,-6);
            _destinationText = textObject.GetComponent<Text>();
            _destinationText.font = _label != null ? _label.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _destinationText.fontSize = 22; _destinationText.resizeTextForBestFit = true;
            _destinationText.resizeTextMinSize = 14; _destinationText.resizeTextMaxSize = 22;
            _destinationText.alignment = TextAnchor.MiddleRight; _destinationText.color = new Color(.9f,.86f,1);
            _destinationText.raycastTarget = false;
        }
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && (_button == null || _button.interactable))
                PlayerController.Instance?.TryInteract();
        }
    }
}
