using System.Collections;
using System.Collections.Generic;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>정착지에서만 열리는 공용 도구 상점. 거래 직후 안전 저장한다.</summary>
    public class SettlementShopController : MonoBehaviour
    {
        public static SettlementShopController Instance { get; private set; }

        [SerializeField] private GameObject root;
        [SerializeField] private Button openButton;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private ShopItemEntryView itemTemplate;
        [SerializeField] private Text regionText;
        [SerializeField] private Text goldText;
        [SerializeField] private Text feedbackText;
        [SerializeField] private Button restButton;
        [SerializeField, Min(0)] private int restCost = 30;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private bool _lockedPlayer;
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            root?.SetActive(false);
            itemTemplate?.gameObject.SetActive(false);
            RefreshAvailability();
        }

        private void OnEnable()
        {
            L10n.Changed += RefreshIfOpen;
            SaveManager.InventoryChanged += RefreshIfOpen;
            SaveManager.EconomyChanged += RefreshIfOpen;
            RefreshAvailability();
        }

        private IEnumerator Start()
        {
            if (SaveManager.Current == null) yield return new WaitUntil(() => SaveManager.Current != null);
            RefreshAvailability();
        }

        private void OnDisable()
        {
            L10n.Changed -= RefreshIfOpen;
            SaveManager.InventoryChanged -= RefreshIfOpen;
            SaveManager.EconomyChanged -= RefreshIfOpen;
            if (IsOpen) Close();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ReleasePlayer();
        }

        public void Toggle() { if (IsOpen) Close(); else Open(); }

        public void Open()
        {
            if (IsOpen || SaveManager.Current == null || !IsSettlement() || !CanOpen()) return;
            IsOpen = true;
            root?.SetActive(true);
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.MoveInput = Vector2.zero;
                PlayerController.Instance.Lock();
                _lockedPlayer = true;
            }
            if (feedbackText != null) feedbackText.text = string.Empty;
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            root?.SetActive(false);
            ReleasePlayer();
        }

        public void RestParty()
        {
            bool success = SaveManager.TryRestParty(restCost, out string message);
            Transact(success, message);
        }

        private void Buy(ItemData item)
        {
            bool success = SaveManager.TryBuyItem(item, out string message);
            Transact(success, message);
        }

        private void Sell(ItemData item)
        {
            bool success = SaveManager.TrySellItem(item, out string message);
            Transact(success, message);
        }

        private void Transact(bool success, string message)
        {
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
            foreach (GameObject entry in _spawned) Destroy(entry);
            _spawned.Clear();
            if (SaveManager.Current == null || ShadowDatabase.Instance == null) return;
            RegionData region = RegionRepository.GetByScene(SaveManager.Current.mapId);
            if (regionText != null) regionText.text = region != null ? L10n.Text(region.displayName) + " · 기억 상점" : "기억 상점";
            if (goldText != null) goldText.text = L10n.Format("shop.gold", "보유 금화  {0:N0}", SaveManager.Current.gold);
            if (restButton != null)
            {
                bool damaged = SaveManager.Current.party.Exists(x => x.currentHp < x.MaxHp);
                restButton.interactable = damaged && SaveManager.Current.gold >= restCost;
            }
            if (clearFeedback && feedbackText != null) feedbackText.text = string.Empty;
            foreach (ItemData item in ShadowDatabase.Instance.items)
            {
                if (item == null || item.shopUnlockAct > Mathf.Max(1, region?.act ?? 1)) continue;
                ShopItemEntryView view = Instantiate(itemTemplate, contentRoot);
                view.Bind(item, SaveManager.GetItemCount(item.itemId), SaveManager.Current.gold, Buy, Sell);
                _spawned.Add(view.gameObject);
            }
            if (contentRoot is RectTransform content)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
            }
        }

        private bool CanOpen()
        {
            if (GameFlowController.Instance != null && GameFlowController.Instance.IsInBattle) return false;
            if (DialogueController.Instance != null && DialogueController.Instance.IsPlaying) return false;
            if (ScriptBookController.Instance != null && ScriptBookController.Instance.IsOpen) return false;
            if (PartyStorageController.Instance != null && PartyStorageController.Instance.IsOpen) return false;
            if (WorldMapController.Instance != null && WorldMapController.Instance.IsOpen) return false;
            if (FieldPauseMenuController.Instance != null && FieldPauseMenuController.Instance.IsOpen) return false;
            if (FieldInventoryController.Instance != null && FieldInventoryController.Instance.IsOpen) return false;
            if (QuestLogController.Instance != null && QuestLogController.Instance.IsOpen) return false;
            return true;
        }

        private static bool IsSettlement()
        {
            if (SaveManager.Current == null) return false;
            return RegionRepository.GetByScene(SaveManager.Current.mapId)?.isSettlement == true;
        }

        private void RefreshAvailability()
        {
            if (openButton != null) openButton.gameObject.SetActive(IsSettlement());
        }

        private void RefreshIfOpen() { if (IsOpen) Refresh(); else RefreshAvailability(); }
        private void ReleasePlayer() { if (!_lockedPlayer) return; PlayerController.Instance?.Unlock(); _lockedPlayer = false; }
    }
}
