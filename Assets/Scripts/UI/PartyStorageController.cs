using System.Collections.Generic;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    public class PartyStorageController : MonoBehaviour
    {
        public static PartyStorageController Instance { get; private set; }
        [SerializeField] private GameObject root;
        [SerializeField] private Transform partyContent;
        [SerializeField] private Transform storageContent;
        [SerializeField] private PartyStorageEntryView partyTemplate;
        [SerializeField] private PartyStorageEntryView storageTemplate;
        [SerializeField] private Text partyCountText;
        [SerializeField] private Text storageCountText;
        [SerializeField] private Text selectedText;
        [SerializeField] private Text companionPassiveText;
        [SerializeField] private Text feedbackText;
        [SerializeField] private Button toPartyButton;
        [SerializeField] private Button toStorageButton;
        [SerializeField] private Button upButton;
        [SerializeField] private Button downButton;
        [SerializeField] private Button healButton;
        [SerializeField] private Button companionButton;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private ShadowInstance _selected;
        private bool _selectedInParty;
        private bool _lockedPlayer;
        private ShadowInstance _exchangeIncoming;
        private bool _refreshPending;
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; root?.SetActive(false);
            partyTemplate?.gameObject.SetActive(false); storageTemplate?.gameObject.SetActive(false);
        }
        private void OnEnable()
        {
            L10n.Changed += RefreshIfOpen;
            SaveManager.PartyChanged += RefreshIfOpen;
        }
        private void RefreshIfOpen() { if (IsOpen) _refreshPending = true; }
        private void LateUpdate() { if (IsOpen && _refreshPending) Refresh(); }
        private void OnDestroy() { if (Instance == this) Instance = null; ReleasePlayer(); }
        private void OnDisable()
        {
            L10n.Changed -= RefreshIfOpen;
            SaveManager.PartyChanged -= RefreshIfOpen;
            if (IsOpen) Close();
        }

        public void Toggle() { if (IsOpen) Close(); else Open(); }
        public void Open()
        {
            if (IsOpen || SaveManager.Current == null) return;
            if (!FieldUiModalState.CanOpen(FieldUiModal.Party)) return;
            IsOpen = true; root?.SetActive(true);
            if (PlayerController.Instance != null) { PlayerController.Instance.MoveInput = Vector2.zero; PlayerController.Instance.Lock(); _lockedPlayer = true; }
            _selected = _exchangeIncoming = null;
            if (feedbackText != null) feedbackText.text = string.Empty;
            if (partyContent is RectTransform partyRect) partyRect.anchoredPosition = Vector2.zero;
            if (storageContent is RectTransform storageRect) storageRect.anchoredPosition = Vector2.zero;
            Refresh();
        }
        public void Close() { if (!IsOpen) return; IsOpen = false; _exchangeIncoming = null; _refreshPending = false; root?.SetActive(false); ReleasePlayer(); }

        public void MoveSelectedToParty()
        {
            if (_exchangeIncoming != null)
            {
                if (!_selectedInParty) return;
                bool success = SaveManager.TrySwapPartyStorage(_selected?.instanceId, _exchangeIncoming.instanceId, out string message);
                if (success) _exchangeIncoming = null;
                Apply(success, message, message);
                return;
            }
            if (_selected != null && !_selectedInParty && SaveManager.Current?.party.Count >= SaveData.MaxPartySize)
            {
                _exchangeIncoming = _selected;
                if (feedbackText != null) feedbackText.text = L10n.Get("party.swap_choose", "교체할 파티 그림자를 고르고 교체 확정을 누르세요.");
                RefreshSelection();
                return;
            }
            Apply(SaveManager.MoveToParty(_selected?.instanceId), L10n.Get("party.moved", "파티로 이동했습니다."), L10n.Get("party.full", "파티가 가득 찼습니다."));
        }
        public void MoveSelectedToStorage()
        {
            if (_exchangeIncoming != null)
            {
                _exchangeIncoming = null;
                if (feedbackText != null) feedbackText.text = L10n.Get("party.swap_cancelled", "교체를 취소했습니다.");
                RefreshSelection(); return;
            }
            Apply(SaveManager.MoveToStorage(_selected?.instanceId), L10n.Get("party.stored", "각본 서고에 보관했습니다."), L10n.Get("party.minimum", "파티에는 전투 가능한 그림자가 최소 1명 필요합니다."));
        }
        public void MoveSelectedUp() => Apply(SaveManager.MovePartySlot(_selected?.instanceId, -1), L10n.Get("party.reordered", "파티 순서를 변경했습니다."), L10n.Get("party.top", "더 위로 이동할 수 없습니다."));
        public void MoveSelectedDown() => Apply(SaveManager.MovePartySlot(_selected?.instanceId, 1), L10n.Get("party.reordered", "파티 순서를 변경했습니다."), L10n.Get("party.bottom", "더 아래로 이동할 수 없습니다."));
        public void SetSelectedCompanion() => Apply(SaveManager.SetCompanion(_selected?.instanceId),
            L10n.Get("party.companion_set", "동행 그림자로 지정했습니다."),
            L10n.Get("party.companion_party_only", "파티에 있는 그림자만 동행할 수 있습니다."));

        public void HealSelected()
        {
            bool success = SaveManager.TryUseBestHealingItem(_selected, out string message);
            GameFeedbackAudio.Play(success ? FeedbackCue.Heal : FeedbackCue.Denied);
            if (feedbackText != null) { feedbackText.text = message; feedbackText.color = success ? new Color(.55f,1f,.72f) : new Color(1f,.6f,.68f); }
            if (success) { SaveManager.Instance.Save(); Refresh(); }
        }

        private void Apply(bool success, string ok, string fail)
        {
            if (feedbackText != null) { feedbackText.text = success ? ok : fail; feedbackText.color = success ? new Color(.55f,1f,.72f) : new Color(1f,.6f,.68f); }
            if (success) { SaveManager.Instance.Save(); Refresh(); }
        }

        private void Refresh()
        {
            _refreshPending = false;
            Vector2 partyPosition = UiListLayout.Position(partyContent), storagePosition = UiListLayout.Position(storageContent);
            UiListLayout.Clear(_spawned);
            var save = SaveManager.Current; if (save == null) return;
            if (_exchangeIncoming != null && !save.storage.Contains(_exchangeIncoming)) _exchangeIncoming = null;
            foreach (var instance in save.party) Spawn(partyTemplate, partyContent, instance);
            foreach (var instance in save.storage) Spawn(storageTemplate, storageContent, instance);
            UiListLayout.Rebuild(partyContent, partyPosition);
            UiListLayout.Rebuild(storageContent, storagePosition);
            if (partyCountText != null) partyCountText.text = L10n.Format("party.count", "파티 {0} / {1}", save.party.Count, SaveData.MaxPartySize);
            if (storageCountText != null) storageCountText.text = L10n.Format("party.storage_count", "각본 서고 {0}", save.storage.Count);
            if (_selected != null)
            {
                _selectedInParty = save.party.Contains(_selected);
                if (!_selectedInParty && !save.storage.Contains(_selected)) _selected = null;
            }
            RefreshSelection();
        }

        private void Spawn(PartyStorageEntryView template, Transform parent, ShadowInstance instance)
        {
            var view = Instantiate(template, parent);
            view.gameObject.SetActive(true);
            view.Bind(instance, Select);
            _spawned.Add(view.gameObject);
        }

        private void Select(ShadowInstance instance)
        {
            _selected = instance; _selectedInParty = SaveManager.Current.party.Contains(instance); RefreshSelection();
            if (_exchangeIncoming != null && !_selectedInParty) { _exchangeIncoming = instance; RefreshSelection(); }
            if (_exchangeIncoming != null && _selectedInParty && feedbackText != null)
                feedbackText.text = SaveManager.CanSwapPartyStorage(instance.instanceId, _exchangeIncoming.instanceId)
                    ? L10n.Get("party.swap_choose", "교체할 파티 그림자를 고르고 교체 확정을 누르세요.")
                    : L10n.Get("party.swap_invalid", "교체할 두 그림자를 선택하세요. 파티에는 전투 가능한 그림자가 한 명 이상 필요합니다.");
        }

        private void RefreshSelection()
        {
            var save = SaveManager.Current;
            if (save == null) return;
            bool isCompanion = _selected != null && save.companionInstanceId == _selected.instanceId;
            bool exchanging = _exchangeIncoming != null;
            if (selectedText != null) selectedText.text = _selected == null ? L10n.Get("party.select", "그림자를 선택하세요")
                : $"{L10n.Text(_selected.DisplayName)} · Lv.{_selected.level}" +
                  (isCompanion ? L10n.Get("party.companion_badge", " · 동행 중") : "");
            if (exchanging && selectedText != null)
                selectedText.text = L10n.Format("party.swap_pair", "서고 {0} ↔ 파티 {1}", L10n.Text(_exchangeIncoming.DisplayName),
                    _selectedInParty ? L10n.Text(_selected.DisplayName) : L10n.Get("party.swap_waiting", "선택 대기"));
            if (companionPassiveText != null)
            {
                ShadowInstance current = save.party.Find(x => x.instanceId == save.companionInstanceId) ??
                                         (save.party.Count > 0 ? save.party[0] : null);
                string currentLine = current?.Data == null ? "현재 동행: 없음"
                    : $"현재 {L10n.Text(current.DisplayName)} · 유대 Lv.{CompanionBondService.BondLevel(current.instanceId)}: " +
                      CompanionBondService.Describe(current.Data);
                string selectedLine = _selected?.Data == null ? "선택한 그림자의 동행 효과를 확인할 수 있습니다."
                    : $"선택 {L10n.Text(_selected.DisplayName)} · 유대 Lv.{CompanionBondService.BondLevel(_selected.instanceId)}: " +
                      CompanionBondService.Describe(_selected.Data);
                companionPassiveText.text = currentLine + "\n" + selectedLine;
            }
            if (toPartyButton != null)
            {
                toPartyButton.interactable = exchanging ? _selectedInParty && SaveManager.CanSwapPartyStorage(_selected?.instanceId, _exchangeIncoming.instanceId)
                    : _selected != null && !_selectedInParty;
                SetButtonLabel(toPartyButton, exchanging ? L10n.Get("party.swap_confirm", "교체 확정") :
                    _selected != null && !_selectedInParty && save.party.Count >= SaveData.MaxPartySize ? L10n.Get("party.swap_begin", "교체 선택") : L10n.Get("party.to_party", "파티로"));
            }
            if (toStorageButton != null)
            {
                toStorageButton.interactable = exchanging || _selected != null && _selectedInParty && save.party.Count > 1 &&
                    (_selected.IsFainted || save.party.Exists(x => x != _selected && !x.IsFainted));
                SetButtonLabel(toStorageButton, exchanging ? L10n.Get("party.swap_cancel", "교체 취소") : L10n.Get("party.to_storage", "서고로"));
            }
            int index = _selectedInParty && _selected != null ? save.party.IndexOf(_selected) : -1;
            if (upButton != null) upButton.interactable = !exchanging && index > 0;
            if (downButton != null) downButton.interactable = !exchanging && index >= 0 && index < save.party.Count - 1;
            if (healButton != null) healButton.interactable = !exchanging && _selected != null && !_selected.IsFainted && _selected.currentHp < _selected.MaxHp;
            if (companionButton != null) companionButton.interactable = !exchanging && _selected != null && _selectedInParty && !isCompanion;
            foreach (var row in _spawned) row.GetComponent<PartyStorageEntryView>()?.SetSelected(_selected?.instanceId, _exchangeIncoming?.instanceId);
        }

        private static void SetButtonLabel(Button button, string value)
        {
            var label = button.GetComponentInChildren<Text>();
            if (label == null) return;
            label.text = value; label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14; label.resizeTextMaxSize = label.fontSize;
        }

        private void ReleasePlayer() { if (!_lockedPlayer) return; PlayerController.Instance?.Unlock(); _lockedPlayer = false; }
    }
}
