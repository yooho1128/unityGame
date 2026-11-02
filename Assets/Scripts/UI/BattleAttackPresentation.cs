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
        private readonly Image portrait;
        private readonly SkillSignatureFx signature;
        private readonly bool self;
        private readonly Color portraitColor;
        private readonly Vector2 source, destination, direction;
        private readonly Vector3 advance, recoil;
        private readonly bool missed,damaging;
        private readonly SkillPresentationProfile profile;
        private readonly Action _onImpact;
        private bool _impactPresented;

        public BattleAttackPresentation(RectTransform layer, BattleUnitPanel attacker, BattleUnitPanel defender,
            SkillData skill, bool missed, Action onImpact = null)
        {
            _onImpact = onImpact;
            profile=SkillPresentationProfile.For(skill);damaging=skill.DealsDamage;
            this.layer = layer; this.missed = missed;
            self = skill.target == SkillTarget.Self;
            actor = attacker.MotionRoot; victim = !self && defender != null ? defender.MotionRoot : null;
            actorHome = actor.localPosition; actorScale = actor.localScale; actorRotation = actor.localRotation;
            victimHome = victim != null ? victim.localPosition : Vector3.zero;
            portrait = self && profile.IsRecovery ? attacker.Portrait : !self && defender != null ? defender.Portrait : null;
            portraitColor = portrait != null ? portrait.color : Color.white;
            var attackerImage = attacker.Portrait != null ? attacker.Portrait.rectTransform : actor;
            var defenderImage = self ? attackerImage : defender != null && defender.Portrait != null ? defender.Portrait.rectTransform : victim;
            source = layer.InverseTransformPoint(attackerImage.TransformPoint(attackerImage.rect.center));
            destination = defenderImage != null ? (Vector2)layer.InverseTransformPoint(defenderImage.TransformPoint(defenderImage.rect.center)) : source + Vector2.right * 300f;
            direction = (destination - source).normalized;
            advance = actor.parent.InverseTransformVector(layer.TransformVector(direction * (skill.damageType == DamageType.Physical ? 68f : 16f)));
            recoil = victim != null ? victim.parent.InverseTransformVector(layer.TransformVector(direction * 22f)) : Vector3.zero;
            signature = new SkillSignatureFx(layer, profile, source, destination, missed, self);
            Sample(0f);
        }

        public void Sample(float progress)
        {
            float p = Mathf.Clamp01(progress);
            if (!_impactPresented && p >= profile.impactProgress)
            { _impactPresented = true; _onImpact?.Invoke(); }
            float windup = Mathf.Sin(Mathf.Clamp01(p / profile.windupProgress) * Mathf.PI * .5f);
            float travel = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(profile.windupProgress, profile.impactProgress, p));
            float recovery = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(profile.impactProgress, 1f, p));
            float motion = p < profile.windupProgress ? -.22f * windup : Mathf.Lerp(-.22f, 1f, travel) * (1f - recovery);
            Vector3 displacement=Vector3.zero;float tilt=0;
            switch(profile.choreography.motion)
            {
                case SkillActorMotion.Cut:displacement=advance*motion;tilt=-direction.x*motion*(12+Mathf.Abs(profile.choreography.turn)*.08f);break;
                case SkillActorMotion.Pounce:displacement=advance*motion*1.5f+Vector3.up*Mathf.Sin(travel*Mathf.PI)*32*(1-recovery);tilt=-direction.x*motion*15;break;
                case SkillActorMotion.Charge:displacement=advance*motion*1.3f;tilt=-direction.x*motion*5;break;
                case SkillActorMotion.Hover:displacement=Vector3.up*Mathf.Sin(p*Mathf.PI)*22;tilt=Mathf.Sin(p*Mathf.PI)*3;break;
                case SkillActorMotion.Raise:displacement=Vector3.up*Mathf.Sin(p*Mathf.PI)*12;break;
                case SkillActorMotion.Dance:displacement=new Vector3(Mathf.Sin(p*Mathf.PI*2)*24,Mathf.Sin(p*Mathf.PI)*18,0);tilt=Mathf.Sin(p*Mathf.PI*2)*10;break;
                case SkillActorMotion.Recoil:displacement=-advance*Mathf.Sin(p*Mathf.PI)*.6f;tilt=direction.x*Mathf.Sin(p*Mathf.PI)*6;break;
            }
            actor.localPosition=actorHome+displacement;
            actor.localScale=Vector3.Scale(actorScale,new Vector3(1+motion*(profile.IsRecovery? .025f:.08f),1-motion*.035f,1));
            actor.localRotation=actorRotation*Quaternion.Euler(0,0,tilt);
            signature.Sample(p);
            float impact = Mathf.InverseLerp(profile.impactProgress, 1f, p);
            bool striking = damaging && !missed && p >= profile.impactProgress && p < .9f;
            if (victim != null)
                victim.localPosition = victimHome + (missed ? -recoil * Mathf.Sin(travel * Mathf.PI) : recoil * (striking ? Mathf.Sin(impact * Mathf.PI * 3f) * (1f-impact) : 0f));
            if (portrait != null)
                portrait.color = profile.IsRecovery ? Color.Lerp(portraitColor,profile.secondary,Mathf.Sin(p*Mathf.PI)*.3f) : striking ? Color.Lerp(portraitColor, new Color(1f,.35f,.4f), (1f-impact) * .65f) : portraitColor;
        }

        public void Dispose()
        {
            if (actor != null) { actor.localPosition = actorHome; actor.localScale = actorScale; actor.localRotation = actorRotation; }
            if (victim != null) victim.localPosition = victimHome;
            if (portrait != null) portrait.color = portraitColor;
            signature.Dispose();
            if (layer != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(layer.gameObject);
                else UnityEngine.Object.DestroyImmediate(layer.gameObject);
            }
        }
    }
}
