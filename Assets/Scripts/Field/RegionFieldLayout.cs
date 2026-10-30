using System;
using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ShadowTheater.Field
{
    /// <summary>Apply authored geography to generated and older scenes before player Start/restore.</summary>
    public sealed class RegionFieldLayout : MonoBehaviour
    {
        private readonly List<Tile> tiles = new List<Tile>();
        private static RegionLayoutCatalog catalog;
        public static void Apply(Transform root, RegionData region, Func<string,Tile,Tile> persist = null)
        {
            // The handcrafted meadow retains its own water, cliffs and winding path.
            if (root == null || region == null || region.regionId == "moonlit_meadow" || root.GetComponent<RegionFieldLayout>() != null) return;
            var maps=root.GetComponentsInChildren<Tilemap>(true);
            var ground=maps.FirstOrDefault(m=>m.name=="Ground");var collision=maps.FirstOrDefault(m=>m.name=="Collision");
            var grid=root.GetComponentInChildren<FieldGrid>(true);
            if(ground==null||collision==null||grid==null)return;
            if(catalog==null)
            {
                var asset=Resources.Load<TextAsset>("Data/RegionLayoutCatalog");
                catalog=asset!=null?JsonUtility.FromJson<RegionLayoutCatalog>(asset.text):new RegionLayoutCatalog();
            }
            var definition=catalog?.entries?.FirstOrDefault(e=>e.regionId==region.regionId);
            if(definition==null)return;
            var anchors=new List<LayoutCell>();
            // Preserve generated discovery/puzzle actors too; their exact cells can vary by content version.
            foreach(var collider in root.GetComponentsInChildren<Collider2D>(true))
            {
                if(collider is TilemapCollider2D || collider is CompositeCollider2D)continue;
                Vector2Int cell=grid.WorldToCell(collider.transform.position);anchors.Add(new LayoutCell(cell.x,cell.y));
            }
            foreach(var route in root.GetComponentsInChildren<TraversalRoute>(true))
                anchors.Add(new LayoutCell(route.DestinationCell.x,route.DestinationCell.y));
            // Keep the exact loaded save position safe, rather than teleporting the player when terrain changes.
            if(Application.isPlaying&&SaveManager.Current?.mapId==region.sceneName)
                anchors.Add(new LayoutCell(SaveManager.Current.tileX,SaveManager.Current.tileY));
            if(Application.isPlaying&&SaveManager.Current?.checkpointMapId==region.sceneName)
                anchors.Add(new LayoutCell(SaveManager.Current.checkpointX,SaveManager.Current.checkpointY));
            var owner=root.gameObject.AddComponent<RegionFieldLayout>();
            var plan=RegionLayoutPolicy.Build(definition,anchors);
            owner.Paint(ground,collision,plan,region,persist);
            if(persist!=null)DestroyImmediate(owner);
        }
        private void Paint(Tilemap ground,Tilemap collision,RegionLayoutPlan plan,RegionData region,Func<string,Tile,Tile> persist)
        {
            var art=new Dictionary<string,Sprite>();
            foreach(var sprite in Resources.LoadAll<Sprite>("Art/Environment/ReleaseTerrain"))art[sprite.name]=sprite;
            foreach(var sprite in Resources.LoadAll<Sprite>("Art/Environment/ReleaseProps"))art[sprite.name]=sprite;
            var fallback=ground.GetTile<Tile>(Vector3Int.zero);
            if(fallback==null)return;
            var palette=new Dictionary<FieldTileKind,Tile>();
            foreach(FieldTileKind kind in Enum.GetValues(typeof(FieldTileKind)))
            {
                var tile=Instantiate(fallback);tile.name="RegionalLayout_"+kind;
                if(art.TryGetValue(kind.ToString(),out var sprite))tile.sprite=sprite;
                tile.color=Color.Lerp(Color.white,region.AccentColor,.16f);
                tile.colliderType=Tile.ColliderType.Grid;
                if(persist!=null)tile=persist(kind.ToString(),tile);else tiles.Add(tile);
                palette[kind]=tile;
            }
            ground.ClearAllTiles();collision.ClearAllTiles();
            for(int i=0;i<RegionLayoutPlan.Count;i++)
            {
                var cell=RegionLayoutPlan.Cell(i);var position=new Vector3Int(cell.x,cell.y,0);
                ground.SetTile(position,palette[plan.ground[i]]);
                if(plan.obstacles[i].HasValue)collision.SetTile(position,palette[plan.obstacles[i].Value]);
            }
            ground.RefreshAllTiles();collision.RefreshAllTiles();
            var collider=collision.GetComponent<TilemapCollider2D>();
            if(collider!=null)collider.ProcessTilemapChanges();
        }
        private void OnDestroy()
        {
            foreach(var tile in tiles)if(tile!=null)
            {if(Application.isPlaying)Destroy(tile);else DestroyImmediate(tile);}
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCatalog()=>catalog=null;
    }
}
