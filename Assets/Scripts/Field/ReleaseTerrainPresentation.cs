using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using ShadowTheater.Story;

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
                var region=RegionRepository.GetByScene(scene.name);
                JourneyHubStation.Ensure(root.transform,region);
                RegionalPuzzleController.Ensure(root.transform,region);
                RegionFieldLayout.Apply(root.transform, region);
                foreach (var portal in root.GetComponentsInChildren<MapPortal>(true)) StylePortal(portal);
                var maps = root.GetComponentsInChildren<Tilemap>(true);
                if (maps.Length == 0) continue;
                var owner = root.AddComponent<ReleaseTerrainPresentation>();
                foreach (var map in maps) owner.Apply(map);
            }
        }

        public static void StylePortal(MapPortal portal)
        {
            if (portal.GetComponent<PortalExitMarker>() == null) portal.gameObject.AddComponent<PortalExitMarker>();
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
            if (string.IsNullOrEmpty(name)) return null;
            switch (name)
            {
                case "MeadowPixel_Grass": return "Grass";
                case "MeadowPixel_Path": return "Path";
                case "MeadowPixel_Water": return "Water";
                case "MeadowPixel_Cliff": return "Cliff";
                case "MeadowPixel_Bush": return "Bush";
            }
            int separator = name.LastIndexOf('_');
            if (separator < 0) return null;
            string theme = name.Substring(0, separator);
            string layer = name.Substring(separator + 1);
            string ground, wall, accent;
            switch (theme)
            {
                case "Theater": ground = "Wood"; wall = "Brick"; accent = "Tree"; break;
                case "Village": case "HowlVillage": case "ThreadMarket": case "FrostPort":
                    ground = "Stone"; wall = "Brick"; accent = "Flowers"; break;
                case "Meadow": case "MoonfangForest":
                    ground = "Grass"; wall = "Cliff"; accent = "Tree"; break;
                case "VioletMarsh":
                    ground = "Grass"; wall = "Cliff"; accent = "Bush"; break;
                case "AshWastes": case "BloodmoonRidge": case "BeastDen":
                    ground = "Path"; wall = "Cliff"; accent = "Cliff"; break;
                case "MarionetteOpera": case "PuppeteerStage":
                    ground = "Wood"; wall = "Brick"; accent = "Flowers"; break;
                case "Archive": case "BlackArchive": case "ForbiddenStacks": case "MirrorVault":
                case "Catacombs": case "ClockworkAlley": case "EmberCity": case "SeveredWorkshop":
                case "Censor": case "BlueAbyss": case "MemorySea": case "MoonPalace": case "AshThrone": case "Boss":
                    ground = "Stone"; wall = "Brick"; accent = "Cliff"; break;
                default: return null;
            }
            return layer == "Ground" ? ground : layer == "Wall" ? wall : layer == "Accent" ? accent : null;
        }

        public static Color TintFor(string name)
        {
            // Keep the chapter palette without recoloring or editing the source atlases.
            if (string.IsNullOrEmpty(name)) return Color.white;
            if (name.StartsWith("Ash") || name.StartsWith("Ember")) return new Color(1f, .66f, .48f);
            if (name.StartsWith("Bloodmoon") || name.StartsWith("Marionette") || name.StartsWith("Puppeteer")) return new Color(1f, .60f, .77f);
            if (name.StartsWith("Frost") || name.StartsWith("Archive") || name.StartsWith("BlueAbyss") || name.StartsWith("MemorySea")) return new Color(.63f, .85f, 1f);
            if (name.StartsWith("Moon") || name.StartsWith("Violet") || name.StartsWith("BeastDen") || name.StartsWith("Forbidden") || name.StartsWith("Mirror")) return new Color(.79f, .70f, 1f);
            if (name.StartsWith("BlackArchive") || name.StartsWith("Censor")) return new Color(.66f, .61f, .76f);
            return Color.white;
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
                copy.color = tile.color * TintFor(tile.name);
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
