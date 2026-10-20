using System;
using ShadowTheater.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    public class InventoryItemEntryView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private Text nameText;
        [SerializeField] private Text countText;
        [SerializeField] private Text useText;
        private ItemData _item;
        private Action<ItemData> _onSelect;
        private bool _bound;

        private void Awake() => EnsureBinding();
        private void OnDestroy() => button?.onClick.RemoveListener(Select);

        public void Bind(ItemData item, int count, Action<ItemData> onSelect)
        {
            EnsureBinding();
            _item = item; _onSelect = onSelect;
            if (icon != null) { icon.sprite = item.icon; icon.enabled = item.icon != null; icon.preserveAspect = true; }
            if (nameText != null) nameText.text = L10n.Text(item.displayName);
            if (countText != null) countText.text = $"x{count}";
            if (useText != null) useText.text = item.isKeyItem
                ? L10n.Get("inventory.key_item", "핵심 이동 도구")
                : item.usableInField ? L10n.Get("inventory.field", "필드 사용")
                : L10n.Get("inventory.battle", "전투 전용");
            gameObject.SetActive(true);
        }

        private void Select() => _onSelect?.Invoke(_item);
        private void EnsureBinding()
        {
            if (button == null) button = GetComponent<Button>();
            if (_bound || button == null) return;
            button.onClick.AddListener(Select); _bound = true;
        }
        public void SetSelected(string itemId)
        {
            if (button?.targetGraphic != null) button.targetGraphic.color = _item != null && _item.itemId == itemId
                ? new Color(1f,.78f,.20f) : new Color(1f,.995f,.96f);
        }
    }
}
