using System.Collections;
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>새, 잎, 물고기 파문과 반짝임을 플레이어 주변에 드물게 만들어 필드가 멈춰 보이지 않게 한다.</summary>
    public sealed class FieldAmbientLife:MonoBehaviour
    {
        [SerializeField]private string regionId;
        [SerializeField]private Vector2 interval=new Vector2(5f,10f);
        private Transform _root;
        private float _nextAt;
        public void Configure(string id)=>regionId=id;
        private void Awake(){var go=new GameObject("AmbientLifeMoments");go.transform.SetParent(transform,false);_root=go.transform;}
        private void Start()=>_nextAt=Time.time+Random.Range(interval.x,interval.y);
        private void Update()
        {
            if(Time.time<_nextAt||PlayerController.Instance==null||FieldGrid.Current==null)return;
            _nextAt=Time.time+Random.Range(interval.x,interval.y);
            PlayerController player=PlayerController.Instance;
            Vector2Int offset=new Vector2Int(Random.Range(-4,5),Random.Range(-3,4));
            Vector2Int cell=player.Cell+offset;
            FootstepSurface surface=FieldTraversalEffects.Instance!=null?
                FieldTraversalEffects.Instance.SurfaceAt(cell):FootstepSurface.Stone;
            if(FieldGrid.Current.IsBlocked(cell,null,false)&&surface!=FootstepSurface.Water)return;
            RegionData region=RegionRepository.Get(regionId);
            FieldWorldSnapshot state=FieldWorldState.Current(region);
            BattleFxGlyph glyph;Color color;Vector3 drift;float duration;
            if(surface==FootstepSurface.Water)
            {glyph=BattleFxGlyph.Ring;color=new Color(.65f,.92f,1f,.72f);drift=Vector3.zero;duration=.9f;}
            else if(state.weather==FieldWeather.MoonWind||state.weather==FieldWeather.Rain)
            {glyph=BattleFxGlyph.Petal;color=new Color(.58f,.82f,.48f,.75f);drift=new Vector3(1.1f,.25f,0f);duration=1.2f;}
            else if(surface==FootstepSurface.Grass)
            {glyph=BattleFxGlyph.Feather;color=new Color(.82f,.9f,.72f,.78f);drift=new Vector3(Random.value>.5f?1.4f:-1.4f,.75f,0f);duration=1.15f;}
            else
            {glyph=BattleFxGlyph.Star;color=new Color(1f,.86f,.46f,.62f);drift=Vector3.up*.18f;duration=.8f;}
            StartCoroutine(Animate(cell,glyph,color,drift,duration));
        }
        private IEnumerator Animate(Vector2Int cell,BattleFxGlyph glyph,Color color,Vector3 drift,float duration)
        {
            var go=new GameObject("AmbientMoment",typeof(SpriteRenderer));go.transform.SetParent(_root,false);
            var renderer=go.GetComponent<SpriteRenderer>();renderer.sprite=BattleFxSpriteLibrary.Get(glyph);
            renderer.color=color;
            Vector3 from=FieldGrid.Current.CellToWorld(cell)+Vector3.up*.18f;
            renderer.sortingOrder=FieldDepthSorter.OrderAt(from.y,1);
            for(float elapsed=0f;elapsed<duration;elapsed+=Time.deltaTime)
            {
                float t=Mathf.Clamp01(elapsed/duration),ease=t*t*(3f-2f*t);
                go.transform.position=from+drift*ease+Vector3.up*Mathf.Sin(t*Mathf.PI)*.14f;
                go.transform.localScale=Vector3.one*Mathf.Lerp(.08f,.22f,Mathf.Sin(t*Mathf.PI));
                Color c=color;c.a*=Mathf.Sin(t*Mathf.PI);renderer.color=c;yield return null;
            }
            Destroy(go);
        }
    }
}
