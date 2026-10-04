using System.Collections.Generic;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
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
        [SerializeField] private Text feedbackText;
        [SerializeField] private Button toPartyButton;
        [SerializeField] private Button toStorageButton;
        [SerializeField] private Button upButton;
        [SerializeField] private Button downButton;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private ShadowInstance _selected;
        private bool _selectedInParty;
        private bool _lockedPlayer;
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
        private void RefreshIfOpen() { if (IsOpen) Refresh(); }
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
            if (GameFlowController.Instance != null && GameFlowController.Instance.IsInBattle) return;
            if (DialogueController.Instance != null && DialogueController.Instance.IsPlaying) return;
            IsOpen = true; root?.SetActive(true);
            if (PlayerController.Instance != null) { PlayerController.Instance.MoveInput = Vector2.zero; PlayerController.Instance.Lock(); _lockedPlayer = true; }
            _selected = null; Refresh();
        }
        public void Close() { if (!IsOpen) return; IsOpen = false; root?.SetActive(false); ReleasePlayer(); }

        public void MoveSelectedToParty() => Apply(SaveManager.MoveToParty(_selected?.instanceId), L10n.Get("party.moved", "파티로 이동했습니다."), L10n.Get("party.full", "파티가 가득 찼습니다."));
        public void MoveSelectedToStorage() => Apply(SaveManager.MoveToStorage(_selected?.instanceId), L10n.Get("party.stored", "각본 서고에 보관했습니다."), L10n.Get("party.minimum", "파티에는 전투 가능한 그림자가 최소 1명 필요합니다."));
        public void MoveSelectedUp() => Apply(SaveManager.MovePartySlot(_selected?.instanceId, -1), L10n.Get("party.reordered", "파티 순서를 변경했습니다."), L10n.Get("party.top", "더 위로 이동할 수 없습니다."));
        public void MoveSelectedDown() => Apply(SaveManager.MovePartySlot(_selected?.instanceId, 1), L10n.Get("party.reordered", "파티 순서를 변경했습니다."), L10n.Get("party.bottom", "더 아래로 이동할 수 없습니다."));

        private void Apply(bool success, string ok, string fail)
        {
            if (feedbackText != null) { feedbackText.text = success ? ok : fail; feedbackText.color = success ? new Color(.55f,1f,.72f) : new Color(1f,.6f,.68f); }
            if (success) { SaveManager.Instance.Save(); Refresh(); }
        }

        private void Refresh()
        {
            foreach (var go in _spawned) Destroy(go); _spawned.Clear();
            var save = SaveManager.Current; if (save == null) return;
            foreach (var instance in save.party) Spawn(partyTemplate, partyContent, instance);
            foreach (var instance in save.storage) Spawn(storageTemplate, storageContent, instance);
            RebuildLayout(partyContent);
            RebuildLayout(storageContent);
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

        private static void RebuildLayout(Transform parent)
        {
            if (!(parent is RectTransform content)) return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
        }

        private void Select(ShadowInstance instance)
        {
            _selected = instance; _selectedInParty = SaveManager.Current.party.Contains(instance); RefreshSelection();
        }

        private void RefreshSelection()
        {
            var save = SaveManager.Current;
            if (save == null) return;
            if (selectedText != null) selectedText.text = _selected == null ? L10n.Get("party.select", "그림자를 선택하세요")
                : $"{L10n.Text(_selected.DisplayName)} · Lv.{_selected.level}";
            if (toPartyButton != null) toPartyButton.interactable = _selected != null && !_selectedInParty && save.party.Count < SaveData.MaxPartySize;
            if (toStorageButton != null) toStorageButton.interactable = _selected != null && _selectedInParty && save.party.Count > 1;
            int index = _selectedInParty && _selected != null ? save.party.IndexOf(_selected) : -1;
            if (upButton != null) upButton.interactable = index > 0;
            if (downButton != null) downButton.interactable = index >= 0 && index < save.party.Count - 1;
        }

        private void ReleasePlayer() { if (!_lockedPlayer) return; PlayerController.Instance?.Unlock(); _lockedPlayer = false; }
    }
}
