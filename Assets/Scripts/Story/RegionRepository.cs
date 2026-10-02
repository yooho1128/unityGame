using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>Resources/Data/RegionCatalog.json을 읽는 40개 지역 단일 데이터 창구.</summary>
    public static class RegionRepository
    {
        private const string ResourcePath = "Data/RegionCatalog";
        private static RegionCatalogData _catalog;

        public static IReadOnlyList<RegionData> All
        {
            get
            {
                EnsureLoaded();
                return _catalog.regions;
            }
        }

        public static RegionData Get(string regionId)
        {
            EnsureLoaded();
            return _catalog.regions.Find(x => x.regionId == regionId);
        }

        public static RegionData GetByScene(string sceneName)
        {
            EnsureLoaded();
            return _catalog.regions.Find(x => x.sceneName == sceneName);
        }

        public static IEnumerable<RegionData> Ordered()
        {
            EnsureLoaded();
            return _catalog.regions.OrderBy(x => x.order);
        }

        public static void Reload()
        {
            _catalog = null;
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            if (_catalog != null) return;
            var asset = Resources.Load<TextAsset>(ResourcePath);
            _catalog = asset != null ? JsonUtility.FromJson<RegionCatalogData>(asset.text) : null;
            if (_catalog == null) _catalog = new RegionCatalogData();
            _catalog.regions ??= new List<RegionData>();
            if (asset == null) Debug.LogError($"[RegionRepository] Resources/{ResourcePath}.json이 없습니다.");
        }
    }
}
