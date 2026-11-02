using ShadowTheater.Data;
using UnityEngine;
namespace ShadowTheater.UI
{
    public static class SkillSoundSynthesis
    {
        public const int Rate=22050;
        public static float[] Samples(SkillPresentationProfile profile,bool impact,bool healing)
        {
            float duration=impact?(healing? .65f:profile.IsUltimate? .5f:.32f):profile.IsUltimate? .65f:.4f;
            int count=Mathf.CeilToInt(duration*Rate);var data=new float[count];
            uint noise=unchecked((uint)profile.seed+1u);float phase=0,low=0;
            float frequency=healing?523.25f:130f+(int)profile.motif*27f+profile.seed%53;
            for(int i=0;i<count;i++)
            {
                float t=i/(float)Rate,p=i/(float)(count-1);
                noise=unchecked(noise*1664525u+1013904223u);
                float grit=(noise>>9)/8388607f*2-1;low=Mathf.Lerp(low,grit,.12f);
                float sweep=impact?Mathf.Lerp(1.8f,.55f,p):Mathf.Lerp(.6f,1.5f,p);
                phase+=frequency*sweep*Mathf.PI*2/Rate;
                float tone=Mathf.Sin(phase),air=grit-low,body;
                switch(profile.motif)
                {
                    case SkillMotif.Slash:case SkillMotif.Claw:case SkillMotif.Thrust:
                        body=air*.6f+Mathf.Sin(phase*.35f)*.3f;
                        if(profile.motif==SkillMotif.Claw)body*=.45f+.55f*Mathf.Pow(Mathf.Cos(p*3*Mathf.PI),8);break;
                    case SkillMotif.Flame:body=low*1.3f+Mathf.Sin(phase*.4f)*.4f+air*.15f;break;
                    case SkillMotif.Frost:case SkillMotif.Rock:body=tone*.4f+Mathf.Sin(phase*2.76f)*.22f+Mathf.Sin(phase*4.07f)*.13f+air*.12f;break;
                    case SkillMotif.Thread:body=tone*Mathf.Exp(-p*8)*.6f+Mathf.Sin(phase*2.01f)*.2f+air*.1f;break;
                    case SkillMotif.Moon:body=Mathf.Sin(phase*.5f+Mathf.Sin(t*29)*1.3f)*.65f+tone*.15f;break;
                    case SkillMotif.Feather:case SkillMotif.Wind:body=air*(.4f+.25f*Mathf.Sin(t*44))+tone*.16f;break;
                    case SkillMotif.Bolt:body=air*.6f*(.4f+.6f*Mathf.Pow(Mathf.Cos(t*110),6))+Mathf.Sin(phase*.4f)*.25f;break;
                    case SkillMotif.Wave:body=low*.8f+tone*.25f+Mathf.Sin(phase*1.5f)*.13f;break;
                    case SkillMotif.Rune:body=tone*.4f+Mathf.Sin(phase*1.498f)*.25f+low*.2f;break;
                    case SkillMotif.Mind:body=Mathf.Sin(phase+Mathf.Sin(t*18)*2)*.5f+Mathf.Sin(phase*1.005f)*.2f;break;
                    default:body=tone*.4f+Mathf.Sin(phase*1.25f)*.22f+Mathf.Sin(phase*1.5f)*.15f;break;
                }
                if(healing)body=tone*.38f+Mathf.Sin(phase*1.25f)*.23f+Mathf.Sin(phase*1.5f)*.15f;
                float attack=Mathf.Clamp01(t/(impact? .003f:.02f)),release=Mathf.Clamp01((duration-t)/.035f);
                float cadence=.55f+.45f*Mathf.Pow(Mathf.Cos(p*profile.choreography.beats*Mathf.PI),4);
                if(profile.choreography.delivery==SkillDelivery.Breath)cadence=.7f+.3f*Mathf.Sin(p*Mathf.PI);
                if(profile.choreography.delivery==SkillDelivery.Weave)body+=Mathf.Sin(phase*2)*.15f*Mathf.Sin(p*Mathf.PI*profile.choreography.beats);
                float envelope=cadence*attack*release*(impact?Mathf.Exp(-p*(healing?3f:6f)):Mathf.Sin(p*Mathf.PI));
                data[i]=Mathf.Clamp(body*envelope*.75f,-.8f,.8f);
            }
            int delay=(int)(Rate*.047f);
            for(int i=count-1;i>=delay;i--)data[i]+=data[i-delay]*.16f;
            for(int i=count-(int)(Rate*.02f);i<count;i++)data[i]*=Mathf.Clamp01((count-1-i)/(Rate*.02f));
            return data;
        }
    }
}
