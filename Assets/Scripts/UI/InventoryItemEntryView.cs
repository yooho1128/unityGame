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

        private void Awake() { if (button == null) button = GetComponent<Button>(); button?.onClick.AddListener(Select); }
        private void OnDestroy() => button?.onClick.RemoveListener(Select);

        public void Bind(ItemData item, int count, Action<ItemData> onSelect)
        {
            _item = item; _onSelect = onSelect;
            if (icon != null) { icon.sprite = item.icon; icon.enabled = item.icon != null; icon.preserveAspect = true; }
            if (nameText != null) nameText.text = L10n.Text(item.displayName);
            if (countText != null) countText.text = $"x{count}";
            if (useText != null) useText.text = item.usableInField
                ? L10n.Get("inventory.field", "필드 사용") : L10n.Get("inventory.battle", "전투 전용");
            gameObject.SetActive(true);
        }

        private void Select() => _onSelect?.Invoke(_item);
    }
}
