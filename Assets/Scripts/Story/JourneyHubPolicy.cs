using System;
using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Data;
using ShadowTheater.Field;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>Late-act service outposts preserve the region's existing encounter/story rules.</summary>
    public static class JourneyHubPolicy
    {
        [Serializable] public sealed class Definition
        { public string regionId, name; public int x,y,checkpointX,checkpointY; public string[] itemIds; }
        [Serializable] public sealed class Catalog { public Definition[] entries; }
        private static Catalog catalog;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()=>catalog=null;
        public static IReadOnlyList<Definition> All
        {
            get
            {
                if(catalog==null)
                {
                    var asset=Resources.Load<TextAsset>("Data/JourneyHubCatalog");
                    catalog=asset!=null?JsonUtility.FromJson<Catalog>(asset.text):new Catalog();
                }
                return catalog?.entries??Array.Empty<Definition>();
            }
        }
        public static Definition Get(RegionData region)=>region==null?null:All.FirstOrDefault(d=>d.regionId==region.regionId);
        public static bool HasServices(RegionData region)=>region!=null&&(region.isSettlement||Get(region)!=null);
        public static Vector2Int Arrival(RegionData region)
        {
            var hub=Get(region);
            return hub!=null?new Vector2Int(hub.checkpointX,hub.checkpointY):region!=null?region.ArrivalCell:Vector2Int.zero;
        }
        public static bool CanFastTravel(RegionData region)=>HasServices(region)&&
            RegionProgress.GetState(region)==RegionMapState.Visited&&RegionProgress.CanEnter(region);
        public static bool AllowsItem(RegionData region,ItemData item)
        {
            var hub=Get(region);
            return hub!=null&&item!=null&&!item.isEventOnly&&!item.isKeyItem&&item.shopUnlockAct<=region.act&&
                Array.IndexOf(hub.itemIds??Array.Empty<string>(),item.itemId)>=0;
        }
        public static LayoutCell? StationCell(Definition hub,IEnumerable<LayoutCell> occupied)
        {
            if(hub==null)return null;
            var used=new HashSet<int>();
            foreach(var cell in occupied??Array.Empty<LayoutCell>())
                if(RegionLayoutPlan.Interior(cell.x,cell.y))used.Add(RegionLayoutPlan.Index(cell.x,cell.y));
            LayoutCell? best=null;int distance=int.MaxValue;
            for(int y=-8;y<=8;y++)for(int x=-10;x<=10;x++)
            {
                if(used.Contains(RegionLayoutPlan.Index(x,y))||(x==hub.checkpointX&&y==hub.checkpointY))continue;
                int next=Math.Abs(x-hub.x)+Math.Abs(y-hub.y);
                if(next<distance){best=new LayoutCell(x,y);distance=next;}
            }
            return best;
        }
    }
}
