using System.Collections.Generic;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>필드에서 보유 도구를 확인하고 그림자를 지정해 사용하는 가방 화면.</summary>
    public class FieldInventoryController : MonoBehaviour
    {
        public static FieldInventoryController Instance { get; private set; }
        [SerializeField] private GameObject root;
        [SerializeField] private Transform itemContent;
        [SerializeField] private Transform targetContent;
        [SerializeField] private InventoryItemEntryView itemTemplate;
        [SerializeField] private InventoryTargetEntryView targetTemplate;
        [SerializeField] private Text goldText;
        [SerializeField] private Text selectedItemText;
        [SerializeField] private Text selectedTargetText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text feedbackText;
        [SerializeField] private Button useButton;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private ItemData _selectedItem;
        private ShadowInstance _selectedTarget;
        private bool _lockedPlayer;
        private bool _refreshPending;
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; root?.SetActive(false);
            itemTemplate?.gameObject.SetActive(false); targetTemplate?.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            SaveManager.InventoryChanged += RefreshIfOpen;
            SaveManager.PartyChanged += RefreshIfOpen;
            SaveManager.EconomyChanged += RefreshIfOpen;
            L10n.Changed += RefreshIfOpen;
        }

        private void OnDisable()
        {
            SaveManager.InventoryChanged -= RefreshIfOpen;
            SaveManager.PartyChanged -= RefreshIfOpen;
            SaveManager.EconomyChanged -= RefreshIfOpen;
            L10n.Changed -= RefreshIfOpen;
            if (IsOpen) Close();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; ReleasePlayer(); }

        public void Toggle() { if (IsOpen) Close(); else Open(); }
        public void Open()
        {
            if (IsOpen || SaveManager.Current == null || !CanOpen()) return;
            IsOpen = true; FieldUiModalState.Show(root);
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.MoveInput = Vector2.zero;
                PlayerController.Instance.Lock(); _lockedPlayer = true;
            }
            _selectedItem = null; _selectedTarget = null;
            if (itemContent is RectTransform itemRect) itemRect.anchoredPosition = Vector2.zero;
            if (targetContent is RectTransform targetRect) targetRect.anchoredPosition = Vector2.zero;
            if (feedbackText != null) feedbackText.text = string.Empty;
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false; root?.SetActive(false); ReleasePlayer();
        }

        public void UseSelected()
        {
            bool success = SaveManager.TryUseFieldItem(_selectedItem, _selectedTarget, out string message);
            if (feedbackText != null)
            {
                feedbackText.text = message;
                feedbackText.color = success ? new Color(.55f,1f,.72f) : new Color(1f,.58f,.67f);
            }
            GameFeedbackAudio.Play(success ? FeedbackCue.Heal : FeedbackCue.Denied);
            if (success) SaveManager.Instance.Save();
            Refresh(false);
        }

        private void Refresh(bool clearFeedback = false)
        {
            _refreshPending = false;
            Vector2 itemPosition = UiListLayout.Position(itemContent), targetPosition = UiListLayout.Position(targetContent);
            UiListLayout.Clear(_spawned);
            var save = SaveManager.Current; if (save == null || ShadowDatabase.Instance == null) return;
            if (goldText != null) goldText.text = L10n.Format("menu.gold_value", "보유 금화 · {0:N0}", save.gold);
            if (clearFeedback && feedbackText != null) feedbackText.text = string.Empty;
            foreach (ItemStack stack in save.inventory)
            {
                ItemData item = ShadowDatabase.Instance.GetItem(stack.itemId);
                if (item == null || stack.count <= 0) continue;
                var view = Instantiate(itemTemplate, itemContent); view.Bind(item, stack.count, SelectItem); _spawned.Add(view.gameObject);
            }
            foreach (ShadowInstance target in save.party)
            {
                var view = Instantiate(targetTemplate, targetContent); view.Bind(target, SelectTarget); _spawned.Add(view.gameObject);
            }
            UiListLayout.Rebuild(itemContent, itemPosition); UiListLayout.Rebuild(targetContent, targetPosition);
            if (_selectedItem != null && SaveManager.GetItemCount(_selectedItem.itemId) <= 0) _selectedItem = null;
            if (_selectedTarget != null && !save.party.Contains(_selectedTarget)) _selectedTarget = null;
            RefreshSelection();
        }

        private void SelectItem(ItemData item) { _selectedItem = item; RefreshSelection(); }
        private void SelectTarget(ShadowInstance target) { _selectedTarget = target; RefreshSelection(); }

        private void RefreshSelection()
        {
            bool usable = SaveManager.TryPreviewFieldItem(_selectedItem, _selectedTarget, out _, out _, out string preview);
            if (selectedItemText != null) selectedItemText.text = _selectedItem != null
                ? L10n.Text(_selectedItem.displayName) + $"  x{SaveManager.GetItemCount(_selectedItem.itemId)}"
                : L10n.Get("inventory.select_item", "도구를 선택하세요");
            if (descriptionText != null) descriptionText.text = _selectedItem != null ? L10n.Text(_selectedItem.description) : string.Empty;
            if (descriptionText != null && _selectedItem != null)
                descriptionText.text += "\n" + preview;
            if (selectedTargetText != null) selectedTargetText.text = _selectedTarget != null
                ? L10n.Text(_selectedTarget.DisplayName) + (_selectedTarget.IsMaxLevel
                    ? $" · Lv.{_selectedTarget.level} MAX · HP {_selectedTarget.currentHp}/{_selectedTarget.MaxHp}"
                    : $" · Lv.{_selectedTarget.level} · EXP {_selectedTarget.exp}/{_selectedTarget.ExpToNext} · HP {_selectedTarget.currentHp}/{_selectedTarget.MaxHp}")
                : L10n.Get("inventory.select_target", "사용할 그림자를 선택하세요");
            if (useButton != null) useButton.interactable = usable;
            foreach (var row in _spawned)
            {
                row.GetComponent<InventoryItemEntryView>()?.SetSelected(_selectedItem?.itemId);
                row.GetComponent<InventoryTargetEntryView>()?.SetSelected(_selectedTarget?.instanceId);
            }
        }

        private bool CanOpen()
        {
            return FieldUiModalState.CanOpen(FieldUiModal.Inventory);
        }

        private void RefreshIfOpen() { if (IsOpen) _refreshPending = true; }
        private void LateUpdate() { if (IsOpen && _refreshPending) Refresh(); }
        private void ReleasePlayer() { if (!_lockedPlayer) return; PlayerController.Instance?.Unlock(); _lockedPlayer = false; }
    }
}
