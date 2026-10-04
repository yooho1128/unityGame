using System;
using ShadowTheater.Data;
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

        private void Awake()
        {
            buyButton?.onClick.AddListener(Buy);
            sellButton?.onClick.AddListener(Sell);
        }

        private void OnDestroy()
        {
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
            if (descriptionText != null) descriptionText.text = L10n.Text(item.description);
            if (ownedText != null) ownedText.text = L10n.Format("shop.owned", "보유 {0}", owned);
            if (buyPriceText != null) buyPriceText.text = L10n.Format("shop.buy_price", "구매 {0:N0}", item.buyPrice);
            if (sellPriceText != null) sellPriceText.text = L10n.Format("shop.sell_price", "판매 {0:N0}", item.sellPrice);
            if (buyButton != null) buyButton.interactable = item.buyPrice >= 0 && gold >= item.buyPrice;
            if (sellButton != null) sellButton.interactable = owned > 0 && item.sellPrice > 0;
            gameObject.SetActive(true);
        }

        private void Buy() => _onBuy?.Invoke(_item);
        private void Sell() => _onSell?.Invoke(_item);
    }
}
