using System;
using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    [Serializable] public class WorldMapAtlasData
    {
        public WorldMapAtlasPage[] pages;
        public WorldMapAtlasLocation[] locations;
    }
    [Serializable] public class WorldMapAtlasPage { public string resource; public string title; }
    [Serializable] public class WorldMapAtlasLocation
    {
        public string regionId;
        public int page;
        public float x;
        public float y;
    }

    /// <summary>Illustrated atlas with data-bound pins. Coordinates never depend on discovery order.</summary>
    public sealed class WorldMapAtlasView
    {
        private readonly RectTransform content;
        private readonly ScrollRect scroll;
        private readonly Font font;
        private readonly Action<RegionData> select;
        private readonly WorldMapAtlasData data;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<GameObject> trails = new List<GameObject>();
        private readonly Dictionary<string, Text> markers = new Dictionary<string, Text>();
        private readonly Dictionary<string, Vector2> positions = new Dictionary<string, Vector2>();
        private readonly Text title;
        private readonly Button previous;
        private readonly Button next;
        private readonly Button questButton;
        private readonly Button zoomIn;
        private readonly Button zoomOut;
        private int page;
        private float zoom = 1f;
        private RegionData selected;
        private Texture2D artwork;
        private float width;
        private float height;
        private static readonly Color Gold = new Color(1f, .79f, .32f);
        private static readonly Color Cyan = new Color(.3f, .9f, 1f);

        public WorldMapAtlasView(RectTransform parent, Font labelFont, Action<RegionData> onSelect)
        {
            content = parent;
            font = labelFont;
            select = onSelect;
            data = JsonUtility.FromJson<WorldMapAtlasData>(Resources.Load<TextAsset>("Data/WorldMapAtlas").text);
            scroll = parent.GetComponentInParent<ScrollRect>();
            var layout = parent.GetComponent<LayoutGroup>();
            if (layout != null) layout.enabled = false;
            var fitter = parent.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0f, 1f);
            scroll.horizontal = scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            // Reserve toolbar space without modifying serialized prefabs.
            scroll.viewport.offsetMax = new Vector2(scroll.viewport.offsetMax.x, -100f);
            var toolbar = Rect("AtlasToolbar", scroll.transform, Vector2.zero, Vector2.zero);
            toolbar.anchorMin = new Vector2(0, 1);
            toolbar.anchorMax = Vector2.one;
            toolbar.pivot = new Vector2(.5f, 1);
            toolbar.sizeDelta = new Vector2(0, 100);
            toolbar.anchoredPosition = Vector2.zero;
            var backdrop = toolbar.gameObject.AddComponent<Image>();
            backdrop.color = new Color(.035f, .04f, .07f, .98f);
            title = Label(toolbar, "AtlasTitle", "", new Vector2(0, -4), new Vector2(900, 34), 21);
            title.rectTransform.anchorMin = new Vector2(0, 1);
            title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.pivot = new Vector2(.5f, 1);
            title.rectTransform.sizeDelta = new Vector2(0, 34);
            previous = Control(toolbar, "‹", 0, () => ChangePage(-1));
            next = Control(toolbar, "›", 1, () => ChangePage(1));
            zoomOut = Control(toolbar, "−", 2, () => Zoom(-.5f));
            zoomIn = Control(toolbar, "+", 3, () => Zoom(.5f));
            Control(toolbar, L10n.Get("map.current", "현재 위치"), 4, () => Focus(Current()));
            questButton = Control(toolbar, L10n.Get("atlas.quest", "퀘스트"), 5, () => Focus(QuestDestination()));
        }

        public static RegionData QuestDestination()
        {
            var manager = QuestManager.Instance;
            var quest = manager?.TrackedQuest;
            if (quest == null) return null;
            foreach (var objective in quest.objectives)
            {
                if ((manager.GetObjectiveProgress(quest.questId, objective.objectiveId)?.current ?? 0) >=
                    Mathf.Max(1, objective.requiredCount)) continue;
                if (!objective.TryGetType(out var type) || type != QuestObjectiveType.Reach) continue;
                string id = objective.targetId ?? "";
                var region = RegionRepository.Get(id.StartsWith("area_", StringComparison.Ordinal) ? id.Substring(5) : id)
                    ?? RegionRepository.GetByScene(id);
                if (region != null && IsKnown(region)) return region;
            }
            return null;
        }

        private static RegionData Current() => RegionRepository.GetByScene(SaveManager.Current?.mapId);
        private static bool IsKnown(RegionData region)
        {
            var knowledge = RegionProgress.GetKnowledge(region);
            return knowledge == RegionMapKnowledge.Current || knowledge == RegionMapKnowledge.Visited ||
                knowledge == RegionMapKnowledge.RouteKnown;
        }

        public void Open() { zoom = 1f; Focus(Current()); }

        public void Refresh()
        {
            if (selected != null && !IsKnown(selected)) selected = null;
            Draw();
        }

        public bool NeedsLayout => Mathf.Abs(Mathf.Max(300, scroll.viewport.rect.width) * zoom - width) > 1f;

        private void ChangePage(int direction)
        {
            page = Mathf.Clamp(page + direction, 0, data.pages.Length - 1);
            selected = null;
            zoom = 1f;
            content.anchoredPosition = Vector2.zero;
            Draw();
            select(null);
        }

        private void Focus(RegionData region)
        {
            if (region == null) { Draw(); return; }
            var location = Array.Find(data.locations, x => x.regionId == region.regionId);
            if (location == null) return;
            page = location.page;
            selected = region;
            Draw();
            Center(location.x, location.y);
            select(region);
        }

        private void Center(float x, float y)
        {
            scroll.StopMovement();
            content.anchoredPosition = new Vector2(
                -Mathf.Clamp(x * width - scroll.viewport.rect.width / 2, 0, Mathf.Max(0, width - scroll.viewport.rect.width)),
                Mathf.Clamp(y * height - scroll.viewport.rect.height / 2, 0, Mathf.Max(0, height - scroll.viewport.rect.height)));
        }

        private void Zoom(float delta)
        {
            float x = (-content.anchoredPosition.x + scroll.viewport.rect.width / 2) / width;
            float y = (content.anchoredPosition.y + scroll.viewport.rect.height / 2) / height;
            zoom = Mathf.Clamp(zoom + delta, 1f, 3f);
            Draw();
            Center(x, y);
        }

        public void Select(RegionData region)
        {
            selected = region;
            DrawTrail();
            foreach (var entry in markers)
            {
                var r = RegionRepository.Get(entry.Key);
                bool current = RegionProgress.GetState(r) == RegionMapState.Current;
                bool quest = QuestDestination()?.regionId == entry.Key;
                entry.Value.text = current ? "●" : quest ? "◆" : "○";
                entry.Value.color = current ? Cyan : quest || region?.regionId == entry.Key ? Gold : Color.white;
            }
        }

        private void Draw()
        {
            UiListLayout.Clear(trails);
            UiListLayout.Clear(spawned);
            markers.Clear(); positions.Clear();
            var newArtwork = Resources.Load<Texture2D>(data.pages[page].resource);
            if (artwork != null && artwork != newArtwork) Resources.UnloadAsset(artwork);
            artwork = newArtwork;
            Canvas.ForceUpdateCanvases();
            width = Mathf.Max(300, scroll.viewport.rect.width) * zoom;
            height = width * (artwork != null ? (float)artwork.height / artwork.width : 2f / 3f);
            content.sizeDelta = new Vector2(width, height);
            var background = Rect("PaintedLandscape", content, new Vector2(width / 2, -height / 2), new Vector2(width, height));
            var image = background.gameObject.AddComponent<RawImage>();
            background.SetAsFirstSibling();
            image.texture = artwork;
            image.color = artwork != null ? Color.white : new Color(.12f, .15f, .2f);
            // The background receives drag gestures through its parent ScrollRect.
            spawned.Add(background.gameObject);
            if (artwork == null) Debug.LogError("Missing world map artwork: " + data.pages[page].resource);
            var quest = QuestDestination();
            foreach (var location in data.locations.Where(x => x.page == page))
            {
                var region = RegionRepository.Get(location.regionId);
                var knowledge = RegionProgress.GetKnowledge(region);
                if (region == null || knowledge == RegionMapKnowledge.Hidden) continue;
                var point = new Vector2(location.x * width, -location.y * height);
                positions[location.regionId] = point;
                var pin = Rect("Pin_" + region.regionId, content, point, new Vector2(148, 72));
                pin.pivot = new Vector2(.5f, 57f / 72f);
                spawned.Add(pin.gameObject);
                var hitArea = pin.gameObject.AddComponent<Image>();
                hitArea.color = Color.clear;
                var button = pin.gameObject.AddComponent<Button>();
                button.targetGraphic = hitArea;
                button.interactable = IsKnown(region) || knowledge == RegionMapKnowledge.Rumored;
                button.onClick.AddListener(() => select(region));
                var marker = Label(pin, "Marker", "○", new Vector2(74, -15), new Vector2(44, 38), 34);
                markers[region.regionId] = marker;
                bool known = IsKnown(region);
                string name = known ? L10n.Get($"region.{region.regionId}.name", L10n.Text(region.displayName)) : "???";
                var plate = Rect("NameRibbon", pin, new Vector2(74, location.y > .84f ? 24 : -51), new Vector2(148, 30));
                plate.gameObject.AddComponent<Image>().color = new Color(.025f, .03f, .05f, .85f);
                Label(plate, "Name", name, new Vector2(74, -15), new Vector2(144, 30), 18);
                if (RegionProgress.GetState(region) == RegionMapState.Current || quest?.regionId == region.regionId)
                    Label(pin, "Status", RegionProgress.GetState(region) == RegionMapState.Current
                        ? L10n.Get("map.current", "현재 위치") : L10n.Get("atlas.destination", "퀘스트 목적지"),
                        new Vector2(74, 14), new Vector2(144, 28), 17).color = quest?.regionId == region.regionId ? Gold : Cyan;
            }
            title.text = $"{page + 1}/{data.pages.Length} · {L10n.Text(data.pages[page].title)}";
            previous.interactable = page > 0;
            next.interactable = page < data.pages.Length - 1;
            questButton.interactable = quest != null;
            zoomIn.interactable = zoom < 3f;
            zoomOut.interactable = zoom > 1f;
            Select(selected);
        }

        private void DrawTrail()
        {
            UiListLayout.Clear(trails);
            var target = selected != null && selected.regionId != Current()?.regionId ? selected : QuestDestination();
            if (target == null || !IsKnown(target)) return;
            var route = RegionProgress.FindKnownRoute(target);
            var points = new List<Vector2>();
            for (int i = 1; i < route.Count; i++)
            {
                // Cross-page destinations use the detail route text and page controls, not a false local road.
                if (!positions.TryGetValue(route[i - 1].regionId, out var a) || !positions.TryGetValue(route[i].regionId, out var b)) continue;
                Vector2 bend = (a + b) / 2 + new Vector2(-(b - a).y, (b - a).x) * .12f;
                int steps = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(a, b) / 18), 2, 90);
                for (int step = 0; step <= steps; step++)
                {
                    float t = (float)step / steps;
                    Vector2 p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * bend + t * t * b;
                    points.Add(p);
                }
            }
            if (points.Count == 0) return;
            var trail = Rect("QuestGuidance", content, Vector2.zero, new Vector2(width, height));
            trail.pivot = new Vector2(0, 1);
            trail.SetSiblingIndex(1);
            var graphic = trail.gameObject.AddComponent<WorldMapRouteGraphic>();
            graphic.color = Gold;
            graphic.SetPoints(points);
            trails.Add(trail.gameObject);
        }

        private Button Control(RectTransform toolbar, string text, int index, Action action)
        {
            var rect = Rect("Control_" + index, toolbar, Vector2.zero, Vector2.zero);
            rect.anchorMin = new Vector2(index / 6f, 0);
            rect.anchorMax = new Vector2((index + 1) / 6f, 0);
            rect.pivot = new Vector2(.5f, 0);
            rect.sizeDelta = new Vector2(-8, 54);
            rect.anchoredPosition = new Vector2(0, 4);
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.13f, .16f, .22f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            var label = Label(rect, "Label", text, Vector2.zero, Vector2.zero, 22);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private Text Label(Transform parent, string name, string text, Vector2 point, Vector2 size, int fontSize)
        {
            var label = Rect(name, parent, point, size).gameObject.AddComponent<Text>();
            label.font = font; label.text = text; label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter; label.color = Color.white; label.raycastTarget = false;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 14; label.resizeTextMaxSize = fontSize;
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, .9f); outline.effectDistance = new Vector2(1, -1);
            return label;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 point, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = size; rect.anchoredPosition = point;
            return rect;
        }
    }
}
