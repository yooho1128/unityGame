using System;
using ShadowTheater.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    public class StarterCardView : MonoBehaviour
    {
        [SerializeField] private Button selectButton;
        [SerializeField] private Image silhouette;
        [SerializeField] private Image accentGlow;
        [SerializeField] private Text nameText;
        [SerializeField] private Text titleText;
        [SerializeField] private Text roleText;
        [SerializeField] private Text statsText;

        private ShadowData _data;
        private Action<ShadowData> _onSelected;

        private void Awake()
        {
            if (selectButton == null) selectButton = GetComponent<Button>();
            selectButton?.onClick.AddListener(Select);
        }

        private void OnDestroy() => selectButton?.onClick.RemoveListener(Select);

        public void Bind(ShadowData data, Action<ShadowData> onSelected)
        {
            _data = data;
            _onSelected = onSelected;
            gameObject.SetActive(data != null);
            if (data == null) return;

            if (silhouette != null)
            {
                silhouette.sprite = data.silhouetteSprite;
                silhouette.color = ShadowPortraitStyle.Tint(silhouette.sprite);
                silhouette.preserveAspect = true;
            }
            if (accentGlow != null) accentGlow.color = data.accentColor;
            if (nameText != null) nameText.text = L10n.Text(data.displayName);
            if (titleText != null) titleText.text = L10n.Text(data.title);
            if (roleText != null) roleText.text = $"{ElementName(data.element)} · {RoleName(data.role)}";
            if (statsText != null)
                statsText.text = L10n.Format("stats.starter", "HP {0}   공격 {1}   속도 {2}",
                    data.baseHp, data.baseAtk, data.baseSpd);
        }

        private void Select()
        {
            if (_data != null) _onSelected?.Invoke(_data);
        }

        private static string ElementName(ShadowElement element)
        {
            switch (element)
            {
                case ShadowElement.Flame: return L10n.Get("element.flame", "붉은 불꽃");
                case ShadowElement.Frost: return L10n.Get("element.frost", "푸른 서리");
                case ShadowElement.Shade: return L10n.Get("element.shade", "자줏빛 그림자");
                default: return L10n.Get("element.none", "무속성");
            }
        }

        private static string RoleName(ShadowRole role)
        {
            switch (role)
            {
                case ShadowRole.PhysicalDealer: return L10n.Get("role.physical", "물리 공격");
                case ShadowRole.MagicNuker: return L10n.Get("role.magic", "마법 공격");
                case ShadowRole.SpeedUtility: return L10n.Get("role.speed", "속도·유틸");
                case ShadowRole.Tank: return L10n.Get("role.tank", "수호");
                default: return L10n.Get("role.support", "지원");
            }
        }
    }
}
