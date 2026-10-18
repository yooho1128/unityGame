using System;
using ShadowTheater.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    public class InventoryTargetEntryView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image portrait;
        [SerializeField] private Image hpFill;
        [SerializeField] private Text nameText;
        [SerializeField] private Text hpText;
        private ShadowInstance _target;
        private Action<ShadowInstance> _onSelect;
        private bool _bound;

        private void Awake() => EnsureBinding();
        private void OnDestroy() => button?.onClick.RemoveListener(Select);

        public void Bind(ShadowInstance target, Action<ShadowInstance> onSelect)
        {
            EnsureBinding();
            _target = target; _onSelect = onSelect;
            if (portrait != null) { portrait.sprite = target.Silhouette; portrait.color = ShadowPortraitStyle.Tint(portrait.sprite); portrait.preserveAspect = true; }
            if (hpFill != null) hpFill.fillAmount = target.MaxHp > 0 ? (float)target.currentHp / target.MaxHp : 0f;
            if (nameText != null) nameText.text = L10n.Text(target.DisplayName);
            if (hpText != null) hpText.text = target.IsFainted ? $"Lv.{target.level} · 기절" : $"Lv.{target.level} · HP {target.currentHp}/{target.MaxHp}";
            if (button != null) button.interactable = true;
            gameObject.SetActive(true);
        }

        private void Select() => _onSelect?.Invoke(_target);
        private void EnsureBinding()
        {
            if (button == null) button = GetComponent<Button>();
            if (_bound || button == null) return;
            button.onClick.AddListener(Select); _bound = true;
        }
        public void SetSelected(string instanceId)
        {
            if (button?.targetGraphic != null) button.targetGraphic.color = _target != null && _target.instanceId == instanceId
                ? new Color(.24f,.15f,.38f) : new Color(.08f,.055f,.14f);
        }
    }
}
