using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowTheater.Story
{
    public static class EndingRepository
    {
        private const string ResourcePath = "Data/EndingCatalog";
        private static List<EndingDefinition> _all;

        public static IReadOnlyList<EndingDefinition> All
        {
            get { EnsureLoaded(); return _all; }
        }

        public static void Reload() => _all = null;

        private static void EnsureLoaded()
        {
            if (_all != null) return;
            _all = new List<EndingDefinition>();
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogError($"[Ending] Resources/{ResourcePath}.json을 찾지 못했습니다.");
                return;
            }

            try
            {
                var catalog = JsonUtility.FromJson<EndingCatalogData>(asset.text);
                if (catalog?.endings != null) _all.AddRange(catalog.endings);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Ending] JSON 파싱 실패: {e.Message}");
            }
        }
    }
}
