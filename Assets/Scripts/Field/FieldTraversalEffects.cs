using System.Collections;
using ShadowTheater.UI;
using ShadowTheater.Story;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>발자국이 풀, 물, 눈, 재에 반응하도록 월드 좌표에 짧은 절차적 효과를 만든다.</summary>
    [DisallowMultipleComponent]
    public sealed class FieldTraversalEffects : MonoBehaviour
    {
        public static FieldTraversalEffects Instance { get; private set; }

        [SerializeField] private PlayerController controller;
        [SerializeField] private FootstepSurface defaultSurface = FootstepSurface.Stone;

        private Transform _effectRoot;
        private FootstepSurface _stepSurface;

        private void Awake()
        {
            Instance = this;
            if (controller == null) controller = GetComponent<PlayerController>();
            var root = new GameObject("TraversalEffects");
            root.transform.SetParent(transform.parent, false);
            _effectRoot = root.transform;
        }

        private void OnEnable()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            if (controller == null) return;
            controller.OnStepStarted += OnStepStarted;
            controller.OnStepFinished += OnStepFinished;
        }

        private void OnDisable()
        {
            if (controller == null) return;
            controller.OnStepStarted -= OnStepStarted;
            controller.OnStepFinished -= OnStepFinished;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_effectRoot != null) Destroy(_effectRoot.gameObject);
        }

        public void Configure(PlayerController owner, FootstepSurface surface)
        {
            controller = owner;
            defaultSurface = surface;
        }

        private void OnStepStarted(Vector2Int from, Vector2Int to)
        {
            _stepSurface = SurfaceAt(to);
            Vector2Int direction = to - from;
            if (_stepSurface == FootstepSurface.Grass) SpawnGrass(to, direction, 1f);
            else if (_stepSurface == FootstepSurface.Water) SpawnWaterWake(from, direction, 1f);
        }

        private void OnStepFinished(Vector2Int cell)
        {
            if (_stepSurface == FootstepSurface.Water) SpawnRipple(cell, 1f);
            else if (_stepSurface == FootstepSurface.Snow || _stepSurface == FootstepSurface.Ash)
                SpawnSoftFootprint(cell, _stepSurface, 1f);
            RegionData region = RegionRepository.GetByScene(ShadowTheater.Save.SaveManager.Current?.mapId);
            if (_stepSurface != FootstepSurface.Water &&
                FieldWorldState.Current(region).weather == FieldWeather.Rain) SpawnRipple(cell, .38f);
        }

        public void PlayWorldStep(Vector2Int cell, float size = .72f)
        {
            FootstepSurface surface = SurfaceAt(cell);
            if (surface == FootstepSurface.Grass) SpawnGrass(cell, Vector2Int.up, size);
            else if (surface == FootstepSurface.Water) SpawnRipple(cell, size);
            else if (surface == FootstepSurface.Snow || surface == FootstepSurface.Ash)
                SpawnSoftFootprint(cell, surface, size);
        }

        public FootstepSurface SurfaceAt(Vector2Int cell)
        {
            string tileName = FieldGrid.Current != null ? FieldGrid.Current.GroundTileName(cell) : string.Empty;
            string collisionName=FieldGrid.Current!=null?FieldGrid.Current.CollisionTileName(cell):string.Empty;
            if(!string.IsNullOrEmpty(collisionName)&&ResolveSurface(collisionName,FootstepSurface.Stone)==FootstepSurface.Water)
                return FootstepSurface.Water;
            // A region-wide fallback is only safe when no tile exists. Generic village/marsh/forest
            // ground must not emit grass blades merely because the surrounding theme contains foliage.
            return string.IsNullOrEmpty(tileName) ? defaultSurface : ResolveSurface(tileName,FootstepSurface.Stone);
        }

        public static FootstepSurface ResolveSurface(string tileName, FootstepSurface fallback)
        {
            string value = (tileName ?? string.Empty).ToLowerInvariant();
            if (value.Contains("water") || value.Contains("sea")) return FootstepSurface.Water;
            if (value.Contains("path") || value.Contains("stone")) return FootstepSurface.Stone;
            if (value.Contains("grass")) return FootstepSurface.Grass;
            if (value.Contains("snow") || value.Contains("frost") || value.Contains("ice")) return FootstepSurface.Snow;
            if (value.Contains("ash")) return FootstepSurface.Ash;
            if (value.Contains("metal") || value.Contains("clockwork")) return FootstepSurface.Metal;
            return fallback;
        }

        private void SpawnGrass(Vector2Int cell, Vector2Int direction, float size)
        {
            Vector3 center = World(cell) + Vector3.down * .22f;
            Color back = new Color(.16f,.48f,.28f,.92f), front = new Color(.34f,.74f,.38f,.96f);
            for (int i = 0; i < 4; i++)
            {
                bool left = i % 2 == 0;
                bool foreground = i >= 2;
                Vector3 start = center + new Vector3(left ? -.13f : .13f, foreground ? -.03f : .08f, 0f);
                Vector3 outward = new Vector3(left ? -.18f : .18f, .06f, 0f);
                float directional = direction.x * (left ? -.035f : .035f);
                StartCoroutine(AnimateSprite("GrassPart", BattleFxGlyph.Petal, start,
                    start + outward + Vector3.right * directional, foreground ? front : back,
                    new Vector3(.20f,.34f,1f) * size, new Vector3(.27f,.25f,1f) * size,
                    left ? 18f : -18f, left ? 38f : -38f, .46f, foreground ? 11 : 8, .58f));
            }
        }

        private void SpawnWaterWake(Vector2Int cell, Vector2Int direction, float size)
        {
            Vector3 center = World(cell) + Vector3.down * .27f;
            float angle = direction.x != 0 ? 0f : 90f;
            Vector3 drift = new Vector3(-direction.x, -direction.y, 0f) * .22f;
            StartCoroutine(AnimateSprite("WaterWake", BattleFxGlyph.Slash, center, center + drift,
                new Color(.60f,.90f,1f,.72f), new Vector3(.34f,.10f,1f) * size,
                new Vector3(.64f,.055f,1f) * size, angle, angle, .38f, 9, .18f));
        }

        private void SpawnRipple(Vector2Int cell, float size)
        {
            Vector3 center = World(cell) + Vector3.down * .28f;
            StartCoroutine(AnimateSprite("WaterRipple", BattleFxGlyph.Ring, center, center,
                new Color(.62f,.91f,1f,.76f), new Vector3(.18f,.07f,1f) * size,
                new Vector3(.88f,.30f,1f) * size, 0f, 0f, .58f, 9, .05f));
            StartCoroutine(DelayedRipple(center, size * .72f));
        }

        private IEnumerator DelayedRipple(Vector3 center, float size)
        {
            yield return new WaitForSeconds(.09f);
            yield return AnimateSprite("WaterRippleSmall", BattleFxGlyph.Ring, center, center,
                new Color(.78f,.96f,1f,.58f), new Vector3(.12f,.05f,1f) * size,
                new Vector3(.66f,.22f,1f) * size, 0f, 0f, .48f, 9, .05f);
        }

        private void SpawnSoftFootprint(Vector2Int cell, FootstepSurface surface, float size)
        {
            Color color = surface == FootstepSurface.Snow
                ? new Color(.86f,.94f,1f,.58f) : new Color(.52f,.30f,.22f,.48f);
            Vector3 center = World(cell) + Vector3.down * .26f;
            StartCoroutine(AnimateSprite("SoftFootprint", BattleFxGlyph.ContactShadow, center,
                center + Vector3.up * .045f, color, new Vector3(.16f,.07f,1f) * size,
                new Vector3(.38f,.16f,1f) * size, 0f, 0f, .52f, 8, .30f));
        }

        private IEnumerator AnimateSprite(string name, BattleFxGlyph glyph, Vector3 from, Vector3 to,
            Color color, Vector3 fromScale, Vector3 toScale, float fromAngle, float toAngle,
            float duration, int sortingOrder, float fadeStart)
        {
            if (_effectRoot == null) yield break;
            var go = new GameObject(name, typeof(SpriteRenderer)); go.transform.SetParent(_effectRoot, false);
            var renderer = go.GetComponent<SpriteRenderer>(); renderer.sprite = BattleFxSpriteLibrary.Get(glyph);
            renderer.color = color;
            renderer.sortingOrder = FieldDepthSorter.OrderAt(from.y, sortingOrder - 9);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = Mathf.Clamp01(elapsed / Mathf.Max(.01f, duration));
                float eased = t * t * (3f - 2f * t);
                go.transform.position = Vector3.LerpUnclamped(from, to, eased);
                go.transform.localScale = Vector3.LerpUnclamped(fromScale, toScale, eased);
                go.transform.rotation = Quaternion.Euler(0f,0f,Mathf.Lerp(fromAngle,toAngle,eased));
                Color current = color; current.a *= 1f - Mathf.InverseLerp(fadeStart, 1f, t); renderer.color = current;
                yield return null;
            }
            Destroy(go);
        }

        private static Vector3 World(Vector2Int cell) => FieldGrid.Current != null
            ? FieldGrid.Current.CellToWorld(cell) : new Vector3(cell.x,cell.y,0f);
    }
}
