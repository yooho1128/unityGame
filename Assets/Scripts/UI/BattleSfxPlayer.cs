using System.Collections.Generic;
using ShadowTheater.Data;
using UnityEngine;
namespace ShadowTheater.UI
{
    public class BattleSfxPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource skillSource;
        [SerializeField] private AudioSource impactSource;
        private readonly Dictionary<string,AudioClip> generated=new Dictionary<string,AudioClip>();
        private readonly Queue<string> cacheOrder=new Queue<string>();
        private const int CacheLimit=48;
        public void PlaySkill(SkillData skill)=>Play(skill,false);
        public void PlayImpact(SkillData skill)=>Play(skill,true);
        private void Play(SkillData skill,bool impact)
        {
            if(skill==null)return;
            AudioSource source=impact?EnsureSource(ref impactSource,"ImpactAudio"):EnsureSource(ref skillSource,"SkillAudio");
            AudioClip custom=impact?skill.impactSfxClip:skill.sfxClip;
            string suffix=impact?"Impact":"Cast";
            if(custom!=null && (custom.name==skill.ultimateFxStyle+"_"+suffix || custom.name==skill.element+"_"+suffix))custom=null;
            source.Stop();
            source.clip=custom!=null?custom:Clip(skill,impact);
            source.pitch=Mathf.Clamp(skill.sfxPitch*(impact && skill.target!=SkillTarget.Self? .94f:1f),.5f,2f);
            source.volume=skill.sfxVolume*GameSettings.SfxVolume*(impact? .88f:.7f);source.Play();
            if(skill.isUltimate)AdaptiveMusicDirector.Instance?.DuckForSkill(impact? .38f:.8f,impact? .66f:.78f);
        }
        private AudioClip Clip(SkillData skill,bool impact)
        {
            string key=skill.skillId+"_"+impact;
            if(generated.TryGetValue(key,out AudioClip cached))return cached;
            if(generated.Count>=CacheLimit)
            {
                int attempts=cacheOrder.Count;
                while(attempts-->0)
                {
                    string old=cacheOrder.Dequeue();AudioClip candidate=generated[old];
                    if((skillSource!=null && skillSource.isPlaying && skillSource.clip==candidate) ||
                       (impactSource!=null && impactSource.isPlaying && impactSource.clip==candidate)) {cacheOrder.Enqueue(old);continue;}
                    generated.Remove(old);Destroy(candidate);break;
                }
            }
            float[] samples=SkillSoundSynthesis.Samples(SkillPresentationProfile.For(skill),impact,skill.target==SkillTarget.Self && skill.healRatio>0);
            var clip=AudioClip.Create("Signature_"+key,samples.Length,1,SkillSoundSynthesis.Rate,false);
            clip.SetData(samples,0);generated.Add(key,clip);cacheOrder.Enqueue(key);return clip;
        }
        private AudioSource EnsureSource(ref AudioSource source,string sourceName)
        {
            if(source!=null)return source;
            var child=new GameObject(sourceName);child.transform.SetParent(transform,false);
            source=child.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;
            source.priority=32;source.ignoreListenerPause=true;return source;
        }
        private void OnDestroy()
        {foreach(AudioClip clip in generated.Values)if(clip!=null)Destroy(clip);generated.Clear();cacheOrder.Clear();}
    }
}
