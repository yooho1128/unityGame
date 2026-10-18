using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        private bool _refreshPending;
        private int _lastTransactionFrame = -1;
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
            SaveManager.PartyChanged += RefreshIfOpen;
            SaveManager.CurrentChanged += OnCurrentChanged;
            TownRestorationService.StageChanged += OnTownStageChanged;
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
            SaveManager.PartyChanged -= RefreshIfOpen;
            SaveManager.CurrentChanged -= OnCurrentChanged;
            TownRestorationService.StageChanged -= OnTownStageChanged;
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
            if (contentRoot is RectTransform content) content.anchoredPosition = Vector2.zero;
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
            if (!BeginTransaction()) return;
            var region = RegionRepository.GetByScene(SaveManager.Current.mapId);
            int cost = TownServicePolicy.RestCost(region.regionId, restCost);
            bool success = SaveManager.TryRestParty(cost, out string message);
            GameFeedbackAudio.Play(success ? FeedbackCue.Heal : FeedbackCue.Denied);
            Transact(success, message);
        }

        private void Buy(ItemData item)
        {
            if (!BeginTransaction()) return;
            var region = RegionRepository.GetByScene(SaveManager.Current.mapId);
            if (!TownServicePolicy.AllowsItem(region, item)) return;
            bool success = SaveManager.TryBuyItem(item, out string message);
            Transact(success, message);
        }

        private void Sell(ItemData item)
        {
            if (!BeginTransaction()) return;
            bool success = SaveManager.TrySellItem(item, out string message);
            Transact(success, message);
        }

        private void Transact(bool success, string message)
        {
            bool saved = true;
            if (success)
            {
                RegionData region = RegionRepository.GetByScene(SaveManager.Current?.mapId);
                if (region != null) TownRestorationService.AddActivity(region.regionId, 1);
                saved = SaveManager.Instance.TrySave();
                if (!saved) message += "\n" + L10n.Get("save.failed", "저장하지 못했습니다. 현재 게임을 유지하고 다시 저장해 주세요.");
            }
            if (feedbackText != null)
            {
                feedbackText.text = message;
                feedbackText.color = success && saved ? new Color(.55f,1f,.72f) : new Color(1f,.58f,.67f);
            }
            Refresh(false);
        }

        private void Refresh(bool clearFeedback = false)
        {
            _refreshPending = false;
            Vector2 position = UiListLayout.Position(contentRoot);
            UiListLayout.Clear(_spawned);
            if (SaveManager.Current == null || !IsSettlement()) { Close(); RefreshAvailability(); return; }
            if (ShadowDatabase.Instance == null) return;
            RegionData region = RegionRepository.GetByScene(SaveManager.Current.mapId);
            if (regionText != null)
            {
                string title = region != null ? L10n.Text(region.displayName) + " · 기억 상점" : "기억 상점";
                string[] specialtyNames = TownServicePolicy.SpecialtyItemIds(region)
                    .Select(id => ShadowDatabase.Instance.GetItem(id)?.displayName)
                    .Where(x => !string.IsNullOrEmpty(x)).Select(L10n.Text).ToArray();
                regionText.text = specialtyNames.Length > 0
                    ? title + " · 전문 " + string.Join("·", specialtyNames)
                    : title;
            }
            if (goldText != null) goldText.text = L10n.Format("shop.gold", "보유 금화  {0:N0}", SaveManager.Current.gold);
            if (restButton != null)
            {
                int cost = TownServicePolicy.RestCost(region.regionId, restCost);
                restButton.interactable = SaveManager.CanRestParty(cost, out _);
                var label = restButton.GetComponentInChildren<Text>(true);
                if (label != null) label.text = L10n.Format("shop.rest_price", "막간 휴식 {0:N0}", cost);
            }
            if (clearFeedback && feedbackText != null) feedbackText.text = string.Empty;
            foreach (ItemData item in ShadowDatabase.Instance.items
                         .Where(x => TownServicePolicy.AllowsItem(region, x))
                         .OrderByDescending(x => TownServicePolicy.IsSpecialty(region, x.itemId))
                         .ThenBy(x => x.shopUnlockAct).ThenBy(x => x.buyPrice))
            {
                ShopItemEntryView view = Instantiate(itemTemplate, contentRoot);
                view.Bind(item, SaveManager.GetItemCount(item.itemId), SaveManager.Current.gold, Buy, Sell);
                _spawned.Add(view.gameObject);
            }
            UiListLayout.Rebuild(contentRoot, position);
        }

        private bool CanOpen()
        {
            return FieldUiModalState.CanOpen(FieldUiModal.Shop);
        }

        private bool BeginTransaction()
        {
            if (!IsOpen || !IsSettlement() || !CanOpen() || _lastTransactionFrame == Time.frameCount) return false;
            _lastTransactionFrame = Time.frameCount;
            return true;
        }

        private void OnTownStageChanged(string regionId, int stage) => RefreshIfOpen();
        private void OnCurrentChanged() { if (IsOpen) Close(); RefreshAvailability(); }

        private static bool IsSettlement()
        {
            if (SaveManager.Current == null) return false;
            return RegionRepository.GetByScene(SaveManager.Current.mapId)?.isSettlement == true;
        }

        private void RefreshAvailability()
        {
            if (openButton != null) openButton.gameObject.SetActive(IsSettlement());
        }

        private void RefreshIfOpen() { if (IsOpen) _refreshPending = true; else RefreshAvailability(); }
        private void LateUpdate() { if (IsOpen && _refreshPending) Refresh(); }
        private void ReleasePlayer() { if (!_lockedPlayer) return; PlayerController.Instance?.Unlock(); _lockedPlayer = false; }
    }
}
