using System.Linq;
using System.Collections.Generic;
using ShadowTheater.Save;
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;

namespace ShadowTheater.Field
{
    public sealed class JourneyHubStation : MonoBehaviour,IInteractable
    {
        [SerializeField] private string regionId;
        public static void Ensure(Transform root,RegionData region)
        {
            var hub=JourneyHubPolicy.Get(region);
            var grid=root.GetComponentInChildren<FieldGrid>(true);
            if(hub==null||grid==null||root.GetComponentInChildren<JourneyHubStation>(true)!=null)return;
            var occupied=new List<LayoutCell>();
            foreach(var collider in root.GetComponentsInChildren<Collider2D>(true))
            {
                if(collider is TilemapCollider2D||collider is CompositeCollider2D)continue;
                var cell=grid.WorldToCell(collider.transform.position);occupied.Add(new LayoutCell(cell.x,cell.y));
            }
            var placement=JourneyHubPolicy.StationCell(hub,occupied);
            if(!placement.HasValue){Debug.LogWarning("[JourneyHub] 보급대를 놓을 빈 칸이 없습니다: "+region.regionId);return;}
            var go=new GameObject("JourneySupplyStation",typeof(SpriteRenderer),typeof(BoxCollider2D),typeof(JourneyHubStation));
            go.transform.SetParent(root,false);go.transform.position=grid.CellToWorld(new Vector2Int(placement.Value.x,placement.Value.y));
            go.GetComponent<JourneyHubStation>().regionId=region.regionId;
            go.GetComponent<BoxCollider2D>().size=new Vector2(.7f,.6f);
            var renderer=go.GetComponent<SpriteRenderer>();
            renderer.sprite=Resources.LoadAll<Sprite>("Art/Environment/ReleaseTerrain").FirstOrDefault(s=>s.name=="Wood");
            renderer.color=new Color(.75f,1f,.88f);renderer.sortingOrder=12;
        }
        private void Start()
        {
            var region=RegionRepository.Get(regionId);var hub=JourneyHubPolicy.Get(region);if(hub==null)return;
            var plate=new GameObject("SupplyLabel",typeof(RectTransform),typeof(Canvas));plate.transform.SetParent(transform,false);
            plate.transform.localPosition=Vector3.up*.85f;plate.transform.localScale=Vector3.one*.0125f;
            var canvas=plate.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=340;
            ((RectTransform)plate.transform).sizeDelta=new Vector2(300,66);
            var go=new GameObject("Label",typeof(RectTransform),typeof(Image));go.transform.SetParent(plate.transform,false);
            Stretch((RectTransform)go.transform);go.GetComponent<Image>().color=new Color(.03f,.05f,.09f,.94f);go.GetComponent<Image>().raycastTarget=false;
            var labelGo=new GameObject("Text",typeof(RectTransform),typeof(Text));labelGo.transform.SetParent(go.transform,false);Stretch((RectTransform)labelGo.transform);
            var label=labelGo.GetComponent<Text>();label.font=LocalizationRuntime.CurrentFont;label.fontSize=21;label.alignment=TextAnchor.MiddleCenter;
            label.text=hub.name+"\n보급 · 휴식 · 귀환 거점";label.color=new Color(.75f,1f,.88f);label.raycastTarget=false;
        }
        public void Interact(PlayerController player)
        {
            var current=RegionRepository.GetByScene(SaveManager.Current?.mapId);
            if(current?.regionId!=regionId||JourneyHubPolicy.Get(current)==null)return;
            SettlementShopController.Instance?.Open();
        }
        private static void Stretch(RectTransform r)
        {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    }
}
