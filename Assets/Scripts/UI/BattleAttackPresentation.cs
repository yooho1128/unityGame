using System;
using ShadowTheater.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>Portrait-only anticipation, travel, impact and recovery. HUD transforms remain stationary.</summary>
    public sealed class BattleAttackPresentation : IDisposable
    {
        public const float ImpactProgress = .52f;
        private readonly RectTransform layer, actor, victim;
        private readonly Vector3 actorHome, victimHome, actorScale;
        private readonly Quaternion actorRotation;
        private readonly Image portrait, projectile, slash, ring;
        private readonly Image[] sparks = new Image[8];
        private readonly Color portraitColor, color;
        private readonly Vector2 source, destination, direction;
        private readonly Vector3 advance, recoil;
        private readonly bool magical, missed;
        private readonly float angle;

        public BattleAttackPresentation(RectTransform layer, BattleUnitPanel attacker, BattleUnitPanel defender,
            SkillData skill, bool missed)
        {
            this.layer = layer; this.missed = missed;
            actor = attacker.MotionRoot; victim = defender != null ? defender.MotionRoot : null;
            actorHome = actor.localPosition; actorScale = actor.localScale; actorRotation = actor.localRotation;
            victimHome = victim != null ? victim.localPosition : Vector3.zero;
            portrait = defender != null ? defender.Portrait : null;
            portraitColor = portrait != null ? portrait.color : Color.white;
            var attackerImage = attacker.Portrait != null ? attacker.Portrait.rectTransform : actor;
            var defenderImage = defender != null && defender.Portrait != null ? defender.Portrait.rectTransform : victim;
            source = layer.InverseTransformPoint(attackerImage.TransformPoint(attackerImage.rect.center));
            destination = defenderImage != null ? (Vector2)layer.InverseTransformPoint(defenderImage.TransformPoint(defenderImage.rect.center)) : source + Vector2.right * 300f;
            direction = (destination - source).normalized;
            angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            magical = skill.damageType == DamageType.Magical;
            color = skill.element == ShadowElement.Flame ? new Color(1f,.4f,.12f) :
                skill.element == ShadowElement.Frost ? new Color(.35f,.85f,1f) :
                skill.element == ShadowElement.Shade ? new Color(.78f,.4f,1f) : new Color(1f,.87f,.55f);
            advance = actor.parent.InverseTransformVector(layer.TransformVector(direction * (magical ? 16f : 68f)));
            recoil = victim != null ? victim.parent.InverseTransformVector(layer.TransformVector(direction * 22f)) : Vector3.zero;
            projectile = Image("ElementProjectile", BattleFxSpriteLibrary.ForSkill(skill), new Vector2(88,88), color);
            slash = Image("ImpactSlash", BattleFxSpriteLibrary.Get(BattleFxGlyph.Slash), new Vector2(205,205), Color.white);
            ring = Image("ImpactRing", BattleFxSpriteLibrary.Get(BattleFxGlyph.Ring), new Vector2(150,150), color);
            for (int i = 0; i < sparks.Length; i++)
                sparks[i] = Image("ImpactSpark" + i, BattleFxSpriteLibrary.Get(BattleFxGlyph.Star), new Vector2(23,23), i % 2 == 0 ? color : Color.white);
            Sample(0f);
        }

        private Image Image(string name, Sprite sprite, Vector2 size, Color tint)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(layer, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite; image.color = tint; image.preserveAspect = true; image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f,.5f);
            image.rectTransform.sizeDelta = size;
            return image;
        }

        public void Sample(float progress)
        {
            float p = Mathf.Clamp01(progress);
            float windup = Mathf.Sin(Mathf.Clamp01(p / .22f) * Mathf.PI * .5f);
            float travel = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.22f, ImpactProgress, p));
            float recovery = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ImpactProgress, 1f, p));
            float motion = p < .22f ? -.22f * windup : Mathf.Lerp(-.22f, 1f, travel) * (1f - recovery);
            actor.localPosition = actorHome + advance * motion;
            actor.localScale = Vector3.Scale(actorScale, new Vector3(1f + motion * .08f, 1f - motion * .035f, 1f));
            actor.localRotation = actorRotation * Quaternion.Euler(0, 0, -direction.x * motion * 7f);
            bool flight = magical && p >= .22f && p < ImpactProgress;
            projectile.enabled = flight;
            projectile.rectTransform.anchoredPosition = Vector2.Lerp(source, destination, travel) + Vector2.up * (Mathf.Sin(travel * Mathf.PI) * 35f);
            projectile.rectTransform.localRotation = Quaternion.Euler(0,0, angle - 90f + (magical ? p * 160f : 0f));
            projectile.rectTransform.localScale = Vector3.one * (.7f + Mathf.Sin(travel * Mathf.PI) * .4f);
            float impact = Mathf.InverseLerp(ImpactProgress, .9f, p);
            bool striking = !missed && p >= ImpactProgress && p < .9f;
            slash.enabled = striking && !magical; ring.enabled = striking;
            slash.rectTransform.anchoredPosition = destination + direction * Mathf.Lerp(-35f, 35f, impact);
            slash.rectTransform.localRotation = Quaternion.Euler(0,0, angle - 35f + impact * 65f);
            slash.rectTransform.localScale = Vector3.one * (.75f + impact * .5f);
            slash.color = new Color(1,1,1,1f-impact);
            ring.rectTransform.anchoredPosition = destination;
            ring.rectTransform.localScale = Vector3.one * (.35f + impact * 1.3f);
            ring.color = new Color(color.r,color.g,color.b,1f-impact);
            for (int i = 0; i < sparks.Length; i++)
            {
                sparks[i].enabled = striking;
                float a = (i * 45f + angle) * Mathf.Deg2Rad;
                sparks[i].rectTransform.anchoredPosition = destination + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (12f + impact * 115f);
                sparks[i].rectTransform.localScale = Vector3.one * (1f - impact);
            }
            if (victim != null)
                victim.localPosition = victimHome + (missed ? -recoil * Mathf.Sin(travel * Mathf.PI) : recoil * (striking ? Mathf.Sin(impact * Mathf.PI * 3f) * (1f-impact) : 0f));
            if (portrait != null)
                portrait.color = striking ? Color.Lerp(portraitColor, new Color(1f,.35f,.4f), (1f-impact) * .65f) : portraitColor;
        }

        public void Dispose()
        {
            if (actor != null) { actor.localPosition = actorHome; actor.localScale = actorScale; actor.localRotation = actorRotation; }
            if (victim != null) victim.localPosition = victimHome;
            if (portrait != null) portrait.color = portraitColor;
            if (layer != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(layer.gameObject);
                else UnityEngine.Object.DestroyImmediate(layer.gameObject);
            }
        }
    }
}
