using System;
using ShadowTheater.Data;
using ShadowTheater.Save;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    public class ShopItemEntryView : MonoBehaviour
    {
        [SerializeField] private Text nameText;
        [SerializeField] private Image icon;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text ownedText;
        [SerializeField] private Text buyPriceText;
        [SerializeField] private Text sellPriceText;
        [SerializeField] private Button buyButton;
        [SerializeField] private Button sellButton;

        private ItemData _item;
        private Action<ItemData> _onBuy;
        private Action<ItemData> _onSell;
        private bool _bound;

        private void Awake()
        {
            EnsureBinding();
        }

        private void EnsureBinding()
        {
            if (_bound) return;
            buyButton?.onClick.AddListener(Buy);
            sellButton?.onClick.AddListener(Sell);
            _bound = true;
        }

        private void OnDestroy()
        {
            EnsureBinding();
            buyButton?.onClick.RemoveListener(Buy);
            sellButton?.onClick.RemoveListener(Sell);
        }

        public void Bind(ItemData item, int owned, int gold, Action<ItemData> onBuy, Action<ItemData> onSell)
        {
            _item = item;
            _onBuy = onBuy;
            _onSell = onSell;
            if (icon != null) { icon.sprite = item.icon; icon.enabled = item.icon != null; icon.preserveAspect = true; }
            if (nameText != null) nameText.text = L10n.Text(item.displayName);
            if (descriptionText != null) descriptionText.text = InventoryDescription(item);
            if (ownedText != null) ownedText.text = L10n.Format("shop.owned", "보유 {0}", owned);
            if (buyPriceText != null) buyPriceText.text = L10n.Format("shop.buy_price", "구매 {0:N0}", item.buyPrice);
            if (sellPriceText != null) sellPriceText.text = L10n.Format("shop.sell_price", "판매 {0:N0}", item.sellPrice);
            if (buyButton != null) buyButton.interactable = SaveManager.CanBuyItem(item, out _);
            if (sellButton != null) sellButton.interactable = SaveManager.CanSellItem(item, out _);
            gameObject.SetActive(true);
        }

        private void Buy() { if (isActiveAndEnabled && buyButton != null && buyButton.interactable) _onBuy?.Invoke(_item); }
        private void Sell() { if (isActiveAndEnabled && sellButton != null && sellButton.interactable) _onSell?.Invoke(_item); }
        private static string InventoryDescription(ItemData item)
        {
            var rule = TrainingItemPolicy.Find(item.itemId);
            return rule != null ? $"경험치 {Mathf.RoundToInt(item.value)} · {TrainingItemPolicy.RangeLabel(rule)} · 상한 초과분은 지급하지 않습니다."
                : L10n.Text(item.description);
        }

    }
}
