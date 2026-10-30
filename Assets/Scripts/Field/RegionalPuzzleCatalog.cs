using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShadowTheater.Field
{
    public static class RegionalPuzzleCatalog
    {
        private static RegionalPuzzleCatalogData catalog;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()=>catalog=null;
        public static IReadOnlyList<RegionalPuzzleDefinition> All
        {
            get
            {
                if(catalog==null)
                {
                    var asset=Resources.Load<TextAsset>("Data/RegionalPuzzleCatalog");
                    catalog=asset!=null?JsonUtility.FromJson<RegionalPuzzleCatalogData>(asset.text):new RegionalPuzzleCatalogData();
                }
                return catalog?.entries??Array.Empty<RegionalPuzzleDefinition>();
            }
        }
        public static RegionalPuzzleDefinition Get(string regionId)=>All.FirstOrDefault(d=>d.regionId==regionId);
    }
}
