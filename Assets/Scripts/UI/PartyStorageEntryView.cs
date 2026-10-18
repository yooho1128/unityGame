using System;
using ShadowTheater.Data;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    public class PartyStorageEntryView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image portrait;
        [SerializeField] private Image hpFill;
        [SerializeField] private Image expFill;
        [SerializeField] private Text nameText;
        [SerializeField] private Text infoText;
        [SerializeField] private Text stageText;
        [SerializeField] private Text expText;
        private ShadowInstance _instance;
        private Action<ShadowInstance> _onSelected;
        private bool _bound;

        private void Awake() => EnsureBinding();
        private void OnDestroy() => button?.onClick.RemoveListener(Select);

        public void Bind(ShadowInstance instance, Action<ShadowInstance> onSelected)
        {
            EnsureBinding();
            _instance = instance; _onSelected = onSelected;
            if (portrait != null) { portrait.sprite = instance.Silhouette; portrait.color = ShadowPortraitStyle.Tint(portrait.sprite); portrait.preserveAspect = true; }
            if (hpFill != null) hpFill.fillAmount = instance.MaxHp > 0 ? (float)instance.currentHp / instance.MaxHp : 0f;
            if (expFill != null) expFill.fillAmount = instance.ExpProgress;
            if (nameText != null) nameText.text = L10n.Text(instance.DisplayName);
            if (infoText != null) infoText.text = $"Lv.{instance.level}  HP {instance.currentHp}/{instance.MaxHp}";
            if (stageText != null) stageText.text = MemoryAwakeningService.StageLabel(instance);
            if (expText != null) expText.text = instance.IsMaxLevel ? "EXP MAX" : $"EXP {instance.exp} / {instance.ExpToNext}";
            gameObject.SetActive(true);
        }
        private void EnsureBinding()
        {
            if (button == null) button = GetComponent<Button>();
            if (_bound || button == null) return;
            button.onClick.AddListener(Select); _bound = true;
        }
        private void Select() => _onSelected?.Invoke(_instance);
        public void SetSelected(string instanceId, string secondaryId = null)
        {
            if (button?.targetGraphic != null)
                button.targetGraphic.color = _instance?.instanceId == instanceId || _instance?.instanceId == secondaryId
                    ? new Color(.24f, .15f, .38f) : new Color(.07f, .05f, .12f);
        }
    }
}
