using ShadowTheater.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    public class BattlePartySlot : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image portrait;
        [SerializeField] private Image hpFill;
        [SerializeField] private Image faintOverlay;

        private static readonly Color Empty = new Color(.08f, .07f, .12f, .55f);
        private static readonly Color Occupied = new Color(.16f, .12f, .24f, .95f);
        private static readonly Color Active = new Color(.32f, .20f, .55f, 1f);

        public void Bind(BattleUnit unit, bool active)
        {
            bool occupied = unit != null;
            if (background != null) background.color = !occupied ? Empty : active ? Active : Occupied;
            if (portrait != null)
            {
                portrait.enabled = occupied;
                portrait.sprite = occupied ? unit.Instance.Silhouette : null;
                portrait.color = occupied ? ShadowPortraitStyle.Tint(portrait.sprite) : Color.clear;
                portrait.preserveAspect = true;
            }
            if (hpFill != null)
            {
                hpFill.enabled = occupied;
                hpFill.fillAmount = occupied ? unit.HpRatio : 0f;
                hpFill.color = unit == null || unit.IsFainted ? new Color(.35f, .32f, .4f)
                    : unit.HpRatio > .5f ? new Color(.35f, .92f, .65f)
                    : unit.HpRatio > .2f ? new Color(1f, .72f, .25f)
                    : new Color(1f, .28f, .36f);
            }
            if (faintOverlay != null)
            {
                faintOverlay.enabled = occupied && unit.IsFainted;
                faintOverlay.color = new Color(.03f, .02f, .06f, .72f);
            }
        }
    }
}
