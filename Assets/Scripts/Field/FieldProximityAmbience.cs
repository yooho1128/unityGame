using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>물가에 가까워질수록 잔잔한 물소리를 올리고 멀어지면 자연스럽게 낮춘다.</summary>
    public sealed class FieldProximityAmbience:MonoBehaviour
    {
        private AudioSource _water;
        private AudioClip _clip;
        private float _targetVolume;
        private float _nextScan;
        private void Awake()
        {
            var go=new GameObject("NearbyWaterAmbience",typeof(AudioSource));go.transform.SetParent(transform,false);
            _water=go.GetComponent<AudioSource>();_water.loop=true;_water.playOnAwake=false;_water.spatialBlend=0f;
            _clip=CreateWaterClip();_water.clip=_clip;_water.volume=0f;_water.Play();
        }
        private void Update()
        {
            if(Time.unscaledTime>=_nextScan){_nextScan=Time.unscaledTime+.25f;Scan();}
            _water.volume=Mathf.MoveTowards(_water.volume,_targetVolume,Time.unscaledDeltaTime*.09f);
        }
        private void Scan()
        {
            if(TownInteriorController.Instance!=null&&TownInteriorController.Instance.IsInside){_targetVolume=0f;return;}
            if(PlayerController.Instance==null||FieldTraversalEffects.Instance==null){_targetVolume=0f;return;}
            Vector2Int origin=PlayerController.Instance.Cell;int nearest=99;
            for(int y=-5;y<=5;y++)for(int x=-5;x<=5;x++)
            {
                int distance=Mathf.Abs(x)+Mathf.Abs(y);if(distance>=nearest||distance>6)continue;
                if(FieldTraversalEffects.Instance.SurfaceAt(origin+new Vector2Int(x,y))==FootstepSurface.Water)nearest=distance;
            }
            float proximity=nearest>6?0f:1f-nearest/6f;
            _targetVolume=proximity*.13f*GameSettings.AmbienceVolume;
        }
        private static AudioClip CreateWaterClip()
        {
            const int rate=22050,count=rate*4;var samples=new float[count];uint noise=27584621u;float smooth=0f;
            for(int i=0;i<count;i++)
            {
                noise=noise*1664525u+1013904223u;float raw=((noise>>9)/8388607f*2f-1f);
                smooth=Mathf.Lerp(smooth,raw,.035f);float t=i/(float)rate;
                float ripple=Mathf.Sin(Mathf.PI*2f*73f*t)*(.15f+.1f*Mathf.Sin(Mathf.PI*2f*.7f*t));
                samples[i]=Mathf.Clamp((smooth*.7f+ripple)*.28f,-.6f,.6f);
            }
            var clip=AudioClip.Create("GeneratedNearbyWater",count,1,rate,false);clip.SetData(samples,0);return clip;
        }
        private void OnDestroy(){if(_clip!=null)Destroy(_clip);}
    }
}
