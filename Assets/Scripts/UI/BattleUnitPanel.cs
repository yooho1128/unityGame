using ShadowTheater.Battle;
using ShadowTheater.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    public class BattleUnitPanel : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private Image accent;
        [SerializeField] private Image hpFill;
        [SerializeField] private Text nameText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text hpText;
        [SerializeField] private Text statusText;

        public void Bind(BattleUnit unit)
        {
            if (unit == null) return;
            if (portrait != null)
            {
                portrait.sprite = unit.Instance.Silhouette;
                portrait.color = Color.black;
                portrait.preserveAspect = true;
            }
            if (accent != null) accent.color = unit.Instance.AccentColor;
            if (hpFill != null)
            {
                hpFill.fillAmount = unit.HpRatio;
                hpFill.color = unit.HpRatio > 0.5f ? new Color(0.35f, 0.92f, 0.65f)
                    : unit.HpRatio > 0.2f ? new Color(1f, 0.72f, 0.25f)
                    : new Color(1f, 0.28f, 0.36f);
            }
            if (nameText != null) nameText.text = unit.Name;
            if (levelText != null) levelText.text = $"Lv.{unit.Level}";
            if (hpText != null) hpText.text = $"{unit.Hp} / {unit.MaxHp}";
            if (statusText != null) statusText.text = unit.MajorStatus != null
                ? StatusLabel(unit.MajorStatus.type) : string.Empty;
        }

        private static string StatusLabel(StatusEffectType status)
        {
            switch (status)
            {
                case StatusEffectType.Freeze: return "빙결";
                case StatusEffectType.Burn: return "화상";
                case StatusEffectType.Bleed: return "출혈";
                default: return status.ToString();
            }
        }
    }
}
