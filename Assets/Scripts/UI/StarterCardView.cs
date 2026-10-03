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
            if (nameText != null) nameText.text = data.displayName;
            if (titleText != null) titleText.text = data.title;
            if (roleText != null) roleText.text = $"{ElementName(data.element)} · {RoleName(data.role)}";
            if (statsText != null)
                statsText.text = $"HP {data.baseHp}   공격 {data.baseAtk}   속도 {data.baseSpd}";
        }

        private void Select()
        {
            if (_data != null) _onSelected?.Invoke(_data);
        }

        private static string ElementName(ShadowElement element)
        {
            switch (element)
            {
                case ShadowElement.Flame: return "붉은 불꽃";
                case ShadowElement.Frost: return "푸른 서리";
                case ShadowElement.Shade: return "자줏빛 그림자";
                default: return "무속성";
            }
        }

        private static string RoleName(ShadowRole role)
        {
            switch (role)
            {
                case ShadowRole.PhysicalDealer: return "물리 공격";
                case ShadowRole.MagicNuker: return "마법 공격";
                case ShadowRole.SpeedUtility: return "속도·유틸";
                case ShadowRole.Tank: return "수호";
                default: return "지원";
            }
        }
    }
}
