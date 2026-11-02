using System;
using ShadowTheater.Data;
using UnityEngine;
namespace ShadowTheater.UI
{
    public enum SkillMotif { Slash, Claw, Thrust, Flame, Frost, Moon, Thread, Feather, Petal, Rune, Wave, Vine, Bolt, Rock, Wind, Mind, Star, Heal }
    /// <summary>One identity shared by visual choreography and synthesized sound.</summary>
    public readonly struct SkillPresentationProfile
    {
        public readonly SkillMotif motif;
        public readonly SkillChoreography choreography;
        public readonly SkillPresentationKind kind;
        public readonly float impactProgress,windupProgress;
        public bool IsRecovery => kind==SkillPresentationKind.Recovery || kind==SkillPresentationKind.UltimateRecovery;
        public bool IsUltimate => kind==SkillPresentationKind.Ultimate || kind==SkillPresentationKind.UltimateRecovery;
        public string VisualKey => kind+"/"+choreography.VisualKey;
        public readonly BattleFxGlyph glyph;
        public readonly Color primary, secondary;
        public readonly float strength, duration;
        public readonly int seed;
        private SkillPresentationProfile(SkillData skill, SkillMotif value)
        {
            choreography=SkillChoreographyCatalog.Resolve(skill,value);
            motif=choreography.motif; glyph=Glyph(motif);
            bool recovery=skill.target==SkillTarget.Self && skill.healRatio>0;
            kind=recovery?(skill.isUltimate?SkillPresentationKind.UltimateRecovery:SkillPresentationKind.Recovery):
                skill.isUltimate?SkillPresentationKind.Ultimate:!skill.DealsDamage?SkillPresentationKind.Debuff:
                skill.fpCost==0?SkillPresentationKind.Basic:SkillPresentationKind.Technique;
            windupProgress=recovery? .3f:skill.isUltimate? .26f:.16f;
            impactProgress=recovery? .64f:skill.isUltimate? .62f:.48f;
            if(choreography.delivery==SkillDelivery.Descend || choreography.delivery==SkillDelivery.Gate)impactProgress+=.04f;
            if(choreography.delivery==SkillDelivery.Thrust || choreography.delivery==SkillDelivery.Rush)impactProgress-=.07f;
            primary=skill.isUltimate?skill.primaryFxColor:ElementChart.ColorFor(skill.element);
            if(!skill.isUltimate && skill.element==ShadowElement.None) primary=new Color(.67f,.73f,1f);
            if(skill.target==SkillTarget.Self) primary=Color.Lerp(primary,new Color(.45f,1f,.78f),.55f);
            secondary=skill.isUltimate?skill.secondaryFxColor:Color.Lerp(primary,Color.white,.65f);
            strength=Mathf.Clamp(.8f+skill.fpCost*.12f+(skill.isUltimate? .3f:0f),.8f,1.6f);
            duration=(recovery?(skill.isUltimate?1.3f:1f):skill.isUltimate?1.06f:skill.fpCost==0? .52f:.76f)+choreography.beats*.035f;
            int hash=17; foreach(char c in skill.skillId??"") hash=unchecked(hash*31+c); seed=hash&0x7fffffff;
        }
        public static SkillPresentationProfile For(SkillData skill)
        {
            string id=(skill.skillId+" "+skill.displayName).ToLowerInvariant(); SkillMotif motif;
            if(Has(id,"petal","widow","\uaf43\uc78e","\ubd80\ucc44")) motif=SkillMotif.Petal;
            else if(Has(id,"thread","string","stitch","seam","wire","puppet","marionette","\uaf30\ub9e4","\ucc9c\uac1c\uc758 \uc2e4")) motif=SkillMotif.Thread;
            else if(Has(id,"claw","fang","bite","pounce","\ubc1c\ud1b1","\ud560\ud034","\uc1a1\uacf3\ub2c8")) motif=SkillMotif.Claw;
            else if(Has(id,"peck","thrust","arrow","needle","spear","\ucc0c\ub974","\ucabc\uae30")) motif=SkillMotif.Thrust;
            else if(Has(id,"wing","crow","feather","bird","\uae43\ud138","\ub0a0\uac1c")) motif=SkillMotif.Feather;
            else if(Has(id,"moon","crescent","howl","eclipse","\ub2ec\ube5b","\uc6b8\uc74c")) motif=SkillMotif.Moon;
            else if(Has(id,"sigil","script","ink","page","chapter","catalog","reading","cens","erasure","\ubb38\uc7a5","\uba39\uce60")) motif=SkillMotif.Rune;
            else if(Has(id,"tide","wave","sea","undertow","\ud30c\ub3c4")) motif=SkillMotif.Wave;
            else if(Has(id,"storm","spark","thunder","lightning","\ubc88\uac1c")) motif=SkillMotif.Bolt;
            else if(Has(id,"star","cosmic","constellation","audience","ovation","\ubcc4\ube5b")) motif=SkillMotif.Star;
            else if(skill.target==SkillTarget.Self && skill.healRatio>0) motif=SkillMotif.Heal;
            else if(skill.ultimateFxStyle!=UltimateFxStyle.None) motif=FromStyle(skill.ultimateFxStyle);
            else motif=FromElement(skill.element,skill.damageType);
            return new SkillPresentationProfile(skill,motif);
        }
        private static bool Has(string id,params string[] words)
        { foreach(string word in words) if(id.IndexOf(word,StringComparison.Ordinal)>=0)return true; return false; }
        private static SkillMotif FromStyle(UltimateFxStyle style)
        {
            switch(style)
            {
                case UltimateFxStyle.FlameCrown:return SkillMotif.Flame;
                case UltimateFxStyle.FrozenArchive:case UltimateFxStyle.FrozenMask:return SkillMotif.Frost;
                case UltimateFxStyle.MoonBeast:return SkillMotif.Moon;
                case UltimateFxStyle.PuppetThreads:return SkillMotif.Thread;
                case UltimateFxStyle.AshBird:return SkillMotif.Feather;
                case UltimateFxStyle.MoonPetals:return SkillMotif.Petal;
                case UltimateFxStyle.LivingScript:return SkillMotif.Rune;
                default:return SkillMotif.Star;
            }
        }
        private static SkillMotif FromElement(ShadowElement e,DamageType damage)
        {
            switch(e)
            {
                case ShadowElement.Flame:return SkillMotif.Flame;
                case ShadowElement.Frost:return SkillMotif.Frost;
                case ShadowElement.Shade:return SkillMotif.Moon;
                case ShadowElement.Water:return SkillMotif.Wave;
                case ShadowElement.Grass:return SkillMotif.Vine;
                case ShadowElement.Light:return SkillMotif.Star;
                case ShadowElement.Earth:return SkillMotif.Rock;
                case ShadowElement.Electric:return SkillMotif.Bolt;
                case ShadowElement.Wind:return SkillMotif.Wind;
                case ShadowElement.Psychic:return SkillMotif.Mind;
                default:return damage==DamageType.Physical?SkillMotif.Slash:SkillMotif.Rune;
            }
        }
        public static BattleFxGlyph Glyph(SkillMotif m)
        {
            switch(m)
            {
                case SkillMotif.Flame:return BattleFxGlyph.Flame;
                case SkillMotif.Frost:case SkillMotif.Rock:return BattleFxGlyph.Crystal;
                case SkillMotif.Moon:case SkillMotif.Wind:case SkillMotif.Wave:return BattleFxGlyph.Crescent;
                case SkillMotif.Thread:return BattleFxGlyph.Thread;
                case SkillMotif.Feather:return BattleFxGlyph.Feather;
                case SkillMotif.Petal:case SkillMotif.Vine:return BattleFxGlyph.Petal;
                case SkillMotif.Rune:return BattleFxGlyph.Rune;
                case SkillMotif.Slash:case SkillMotif.Claw:case SkillMotif.Thrust:case SkillMotif.Bolt:return BattleFxGlyph.Slash;
                case SkillMotif.Mind:case SkillMotif.Heal:return BattleFxGlyph.Ring;
                default:return BattleFxGlyph.Star;
            }
        }
    }
}
