using System;
using System.Collections.Generic;
using ShadowTheater.Data;
using UnityEngine;
namespace ShadowTheater.UI
{
    public enum SkillDelivery { Sweep, Cross, Thrust, Volley, Arc, Breath, Descend, Rush, Ripple, Orbit, Seal, Tether, Gate, Column, Fan, Siphon, Lift, Weave, Shelter, Bloom }
    public enum SkillFormation { Edge, Jaw, Crown, Prism, Sigil, Rain, Tide, Cage, Void, Rays, Suture, Blossom, Stars, Shield, Bell, Wings, Pages, Clock, Mask, Eclipse }
    public enum SkillFinish { Scatter, Embers, Shatter, Collapse, Ascend, Return, Knit, Fade }
    public enum SkillActorMotion { Cut, Pounce, Charge, Hover, Still, Raise, Dance, Recoil }
    public enum SkillPresentationKind { Basic, Technique, Debuff, Ultimate, Recovery, UltimateRecovery }
    public readonly struct SkillChoreography
    {
        public readonly SkillMotif motif;
        public readonly SkillArtSet art;
        public readonly SkillDelivery delivery;
        public readonly SkillFormation formation;
        public readonly SkillFinish finish;
        public readonly SkillActorMotion motion;
        public readonly int beats;
        public readonly float turn;
        public readonly bool authored;
        public string VisualKey => $"{motif}/{delivery}/{formation}/{finish}/{motion}/{beats}/{turn}";
        public SkillChoreography(SkillMotif motif,SkillDelivery delivery,SkillFormation formation,SkillFinish finish,
            SkillActorMotion motion,int beats,float turn,bool authored,SkillArtSet art=default(SkillArtSet))
        {
            this.art=art;this.motif=motif;this.delivery=delivery;this.formation=formation;this.finish=finish;
            this.motion=motion;this.beats=beats;this.turn=turn;this.authored=authored;
        }
    }
    /// <summary>Explicit direction for each shipped skill. Reusable primitives, individually authored combinations.</summary>
    public static class SkillChoreographyCatalog
    {
        [Serializable] private class Catalog { public Entry[] entries; }
        [Serializable] private class Entry
        { public string skillId,motif,delivery,formation,finish,motion,charge,travel,impact,debris,crest;public int beats;public float turn; }
        private static Dictionary<string,SkillChoreography> entries;
        public static SkillChoreography Resolve(SkillData skill,SkillMotif fallback)
        {
            if(entries==null)
            {
                entries=new Dictionary<string,SkillChoreography>(StringComparer.Ordinal);
                var resource=Resources.Load<TextAsset>("Data/SkillPresentationCatalog");
                if(resource!=null)
                    foreach(var e in JsonUtility.FromJson<Catalog>(resource.text).entries)
                        entries.Add(e.skillId,new SkillChoreography(Parse<SkillMotif>(e.motif),Parse<SkillDelivery>(e.delivery),
                            Parse<SkillFormation>(e.formation),Parse<SkillFinish>(e.finish),Parse<SkillActorMotion>(e.motion),
                            Mathf.Clamp(e.beats,1,5),e.turn,true,new SkillArtSet(e.charge,e.travel,e.impact,e.debris,e.crest)));
            }
            if(skill.skillId!=null && entries.TryGetValue(skill.skillId,out var value))return value;
            bool heal=skill.target==SkillTarget.Self && skill.healRatio>0;
            return new SkillChoreography(fallback,heal?SkillDelivery.Lift:SkillDelivery.Arc,
                heal?SkillFormation.Shield:SkillFormation.Sigil,heal?SkillFinish.Ascend:SkillFinish.Scatter,
                heal?SkillActorMotion.Raise:SkillActorMotion.Hover,1,0,false);
        }
        private static T Parse<T>(string value) where T:struct => (T)Enum.Parse(typeof(T),value);
    }
}
