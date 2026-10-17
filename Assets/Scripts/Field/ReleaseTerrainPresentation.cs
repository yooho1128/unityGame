using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace ShadowTheater.Field
{
    /// <summary>기존 필드의 배치·충돌을 유지하면서 출시 타일 아트를 적용한다.</summary>
    public class ReleaseTerrainPresentation : MonoBehaviour
    {
        private readonly List<Tile> _copies = new List<Tile>();
        private static Dictionary<string, Sprite> _sprites;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            _sprites = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var portal in root.GetComponentsInChildren<MapPortal>(true)) StylePortal(portal);
                var maps = root.GetComponentsInChildren<Tilemap>(true);
                if (maps.Length == 0) continue;
                var owner = root.AddComponent<ReleaseTerrainPresentation>();
                foreach (var map in maps) owner.Apply(map);
            }
        }

        public static void StylePortal(MapPortal portal)
        {
            var original = portal.GetComponent<SpriteRenderer>();
            var ring = Resources.Load<Sprite>("Art/FX/Ring");
            if (original == null || ring == null || portal.transform.Find("ExitRing") != null) return;
            var child = new GameObject("ExitRing", typeof(SpriteRenderer));
            child.transform.SetParent(portal.transform, false);
            child.transform.localScale = new Vector3(1 / ring.bounds.size.x, 1 / ring.bounds.size.y, 1);
            var renderer = child.GetComponent<SpriteRenderer>();
            renderer.sprite = ring;
            renderer.sharedMaterial = original.sharedMaterial;
            renderer.color = new Color(.72f, .64f, 1, .8f);
            renderer.sortingLayerID = original.sortingLayerID;
            renderer.sortingOrder = original.sortingOrder;
            original.enabled = false;
        }

        public static string ArtFor(string name)
        {
            switch (name)
            {
                case "MeadowPixel_Grass": return "Grass";
                case "MeadowPixel_Path": return "Path";
                case "MeadowPixel_Water": return "Water";
                case "MeadowPixel_Cliff": return "Cliff";
                case "MeadowPixel_Bush": return "Bush";
                case "Theater_Ground": return "Wood";
                case "Village_Ground": case "Boss_Ground": return "Stone";
                case "Theater_Wall": case "Village_Wall": case "Boss_Wall": return "Brick";
                case "Theater_Accent": return "Tree";
                case "Village_Accent": return "Flowers";
                case "Boss_Accent": return "Cliff";
                default: return null;
            }
        }

        public int Apply(Tilemap map)
        {
            if (_sprites == null)
            {
                _sprites = new Dictionary<string, Sprite>();
                foreach (string atlas in new[] { "ReleaseTerrain", "ReleaseProps" })
                    foreach (var sprite in Resources.LoadAll<Sprite>("Art/Environment/" + atlas))
                        _sprites[sprite.name] = sprite;
            }
            var used = new TileBase[map.GetUsedTilesCount()];
            map.GetUsedTilesNonAlloc(used);
            int count = 0;
            foreach (var original in used)
            {
                if (!(original is Tile tile)) continue;
                string art = ArtFor(tile.name);
                if (art == null || !_sprites.TryGetValue(art, out var sprite)) continue;
                var copy = Instantiate(tile);
                copy.name = tile.name + "_Release";
                copy.sprite = sprite;
                _copies.Add(copy);
                map.SwapTile(tile, copy);
                count++;
            }
            return count;
        }

        private void OnDestroy()
        {
            foreach (var tile in _copies)
                if (tile != null)
                {
                    if (Application.isPlaying) Destroy(tile); else DestroyImmediate(tile);
                }
        }
    }
}
