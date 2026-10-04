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
            IsOpen = true; root?.SetActive(true);
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.MoveInput = Vector2.zero;
                PlayerController.Instance.Lock(); _lockedPlayer = true;
            }
            _selectedItem = null; _selectedTarget = null;
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
            if (success) SaveManager.Instance.Save();
            Refresh(false);
        }

        private void Refresh(bool clearFeedback = false)
        {
            foreach (GameObject go in _spawned) Destroy(go); _spawned.Clear();
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
            Rebuild(itemContent); Rebuild(targetContent);
            if (_selectedItem != null && SaveManager.GetItemCount(_selectedItem.itemId) <= 0) _selectedItem = null;
            RefreshSelection();
        }

        private void SelectItem(ItemData item) { _selectedItem = item; RefreshSelection(); }
        private void SelectTarget(ShadowInstance target) { _selectedTarget = target; RefreshSelection(); }

        private void RefreshSelection()
        {
            if (selectedItemText != null) selectedItemText.text = _selectedItem != null
                ? L10n.Text(_selectedItem.displayName) + $"  x{SaveManager.GetItemCount(_selectedItem.itemId)}"
                : L10n.Get("inventory.select_item", "도구를 선택하세요");
            if (descriptionText != null) descriptionText.text = _selectedItem != null ? L10n.Text(_selectedItem.description) : string.Empty;
            if (selectedTargetText != null) selectedTargetText.text = _selectedTarget != null
                ? L10n.Text(_selectedTarget.DisplayName) + $" · HP {_selectedTarget.currentHp}/{_selectedTarget.MaxHp}"
                : L10n.Get("inventory.select_target", "사용할 그림자를 선택하세요");
            if (useButton != null) useButton.interactable = _selectedItem != null && _selectedTarget != null &&
                _selectedItem.usableInField && !_selectedTarget.IsFainted && _selectedTarget.currentHp < _selectedTarget.MaxHp;
        }

        private bool CanOpen()
        {
            if (GameFlowController.Instance != null && GameFlowController.Instance.IsInBattle) return false;
            if (DialogueController.Instance != null && DialogueController.Instance.IsPlaying) return false;
            if (ScriptBookController.Instance != null && ScriptBookController.Instance.IsOpen) return false;
            if (PartyStorageController.Instance != null && PartyStorageController.Instance.IsOpen) return false;
            if (WorldMapController.Instance != null && WorldMapController.Instance.IsOpen) return false;
            if (SettlementShopController.Instance != null && SettlementShopController.Instance.IsOpen) return false;
            if (QuestLogController.Instance != null && QuestLogController.Instance.IsOpen) return false;
            return true;
        }

        private static void Rebuild(Transform parent)
        {
            if (!(parent is RectTransform content)) return;
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
        }
        private void RefreshIfOpen() { if (IsOpen) Refresh(); }
        private void ReleasePlayer() { if (!_lockedPlayer) return; PlayerController.Instance?.Unlock(); _lockedPlayer = false; }
    }
}
