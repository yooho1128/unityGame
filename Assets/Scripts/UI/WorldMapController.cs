using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>63개 지역의 발견 상태와 출현 그림자를 보여주고 방문한 마을 빠른 이동을 제공한다.</summary>
    public class WorldMapController : MonoBehaviour
    {
        public static WorldMapController Instance { get; private set; }

        [SerializeField] private GameObject root;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private RegionMapNodeView nodeTemplate;
        [SerializeField] private Text progressText;
        [SerializeField] private Text actText;
        [SerializeField] private Text nameText;
        [SerializeField] private Text environmentText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text summaryText;
        [SerializeField] private Text shadowsText;
        [SerializeField] private Text bossText;
        [SerializeField] private Text feedbackText;
        [SerializeField] private Image detailAccent;
        [SerializeField] private Button travelButton;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private RegionData _selected;
        private bool _lockedPlayer;
        private bool _refreshPending;
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            root?.SetActive(false);
            nodeTemplate?.gameObject.SetActive(false);
        }

        private void OnEnable() { L10n.Changed += RefreshIfOpen; SaveManager.CurrentChanged += RefreshIfOpen; }

        private void OnDestroy() { if (Instance == this) Instance = null; ReleasePlayer(); }
        private void OnDisable() { L10n.Changed -= RefreshIfOpen; SaveManager.CurrentChanged -= RefreshIfOpen; if (IsOpen) Close(); }
        private void RefreshIfOpen() { if (IsOpen) _refreshPending = true; }
        private void LateUpdate() { if (IsOpen && _refreshPending) Refresh(); }

        public void Toggle() { if (IsOpen) Close(); else Open(); }

        public void Open()
        {
            if (IsOpen || SaveManager.Current == null) return;
            if (!FieldUiModalState.CanOpen(FieldUiModal.WorldMap)) return;
            IsOpen = true;
            FieldUiModalState.Show(root);
            _selected = null;
            if (contentRoot is RectTransform content) content.anchoredPosition = Vector2.zero;
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.MoveInput = Vector2.zero;
                PlayerController.Instance.Lock();
                _lockedPlayer = true;
            }
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            root?.SetActive(false);
            ReleasePlayer();
        }

        public void TravelToSelected()
        {
            if (_selected == null || !_selected.isSettlement ||
                RegionProgress.GetState(_selected) != RegionMapState.Visited) return;
            if (!Application.CanStreamedLevelBeLoaded(_selected.sceneName))
            {
                if (feedbackText != null) feedbackText.text = L10n.Get("map.scene_missing", "이 지역의 씬은 아직 제작 중입니다.");
                return;
            }
            var target = _selected;
            Close();
            if (MapLoader.Instance == null || !MapLoader.Instance.TravelTo(target.sceneName, target.ArrivalCell,
                    (FacingDir)Mathf.Clamp(target.arrivalFacing, 0, 3)))
            {
                Open();
                if (feedbackText != null) feedbackText.text = L10n.Get("map.cannot_travel", "지금은 이동할 수 없습니다.");
            }
        }

        private void Refresh()
        {
            _refreshPending = false;
            if (SaveManager.Current == null) { Close(); return; }
            Vector2 position = UiListLayout.Position(contentRoot);
            string selectedId = _selected?.regionId;
            UiListLayout.Clear(_spawned);
            var ordered = RegionRepository.Ordered().ToList();
            foreach (var region in ordered)
            {
                RegionMapKnowledge knowledge = RegionProgress.GetKnowledge(region);
                if (knowledge == RegionMapKnowledge.Hidden) continue;
                var view = Instantiate(nodeTemplate, contentRoot);
                view.Bind(region, knowledge, Select);
                _spawned.Add(view.gameObject);
            }
            int visited = ordered.Count(x => RegionProgress.GetState(x) == RegionMapState.Visited ||
                RegionProgress.GetState(x) == RegionMapState.Current);
            if (progressText != null) progressText.text = L10n.Format("map.progress", "방문 {0} / {1}", visited, ordered.Count);
            var current = RegionRepository.GetByScene(SaveManager.Current?.mapId);
            var selected = ordered.FirstOrDefault(x => x.regionId == selectedId &&
                RegionProgress.GetKnowledge(x) != RegionMapKnowledge.Hidden &&
                RegionProgress.GetKnowledge(x) != RegionMapKnowledge.Locked);
            Select(selected ?? current ?? ordered.FirstOrDefault(x => RegionProgress.GetState(x) != RegionMapState.Locked));
            UiListLayout.Rebuild(contentRoot, position);
        }

        private void Select(RegionData region)
        {
            _selected = region;
            foreach (var row in _spawned) row.GetComponent<RegionMapNodeView>()?.SetSelected(region);
            if (region == null) { ShowEmpty(); return; }
            var state = RegionProgress.GetState(region);
            RegionMapKnowledge knowledge = RegionProgress.GetKnowledge(region);
            bool detailed = knowledge == RegionMapKnowledge.Visited || knowledge == RegionMapKnowledge.Current;
            bool rumored = knowledge == RegionMapKnowledge.Rumored;
            if (actText != null) actText.text = detailed
                ? L10n.Get($"region.{region.regionId}.act", L10n.Text(region.actTitle))
                : rumored ? L10n.Get("map.rumored_act", "소문 속 숨은 길")
                : L10n.Get("map.undiscovered_act", "아직 공개되지 않은 막");
            if (nameText != null) nameText.text = detailed || knowledge == RegionMapKnowledge.RouteKnown
                ? L10n.Get($"region.{region.regionId}.name", L10n.Text(region.displayName))
                : rumored ? "?" : L10n.Get("common.unknown_path", "아직 걷지 않은 길");
            if (environmentText != null)
            {
                FieldWorldSnapshot world = FieldWorldState.Current(region);
                environmentText.text = detailed
                    ? L10n.Get($"region.{region.regionId}.environment", L10n.Text(region.environment)) + " · " +
                      L10n.Text(FieldWorldState.TimeLabel(world.timeOfDay)) + " · " +
                      L10n.Text(FieldWorldState.WeatherLabel(world.weather))
                    : rumored ? L10n.Get("map.follow_clue", "소문과 환경 단서를 따라가야 합니다")
                    : L10n.Get("map.discover_route", "직접 방문해야 정보를 기록할 수 있습니다");
            }
            if (levelText != null) levelText.text = detailed
                ? L10n.Format("map.level", "권장 Lv.{0}–{1}", region.recommendedLevelMin, region.recommendedLevelMax) : L10n.Get("map.level_unknown", "권장 레벨 ???");
            if (summaryText != null) summaryText.text = detailed
                ? L10n.Get($"region.{region.regionId}.summary", L10n.Text(region.summary))
                : rumored ? L10n.Text(TownServicePolicy.HiddenMapHint(region))
                : L10n.Get("map.locked_hint", "출구를 찾아 직접 걸어가면 지역 정보가 기록됩니다.");
            if (summaryText != null && (detailed || knowledge == RegionMapKnowledge.RouteKnown))
            {
                var connections = new List<string>();
                foreach (string id in region.connectedRegionIds ?? System.Array.Empty<string>())
                {
                    var next = RegionRepository.Get(id);
                    var known = RegionProgress.GetKnowledge(next);
                    if (known == RegionMapKnowledge.Current || known == RegionMapKnowledge.Visited ||
                        known == RegionMapKnowledge.RouteKnown)
                        connections.Add(L10n.Get($"region.{next.regionId}.name", L10n.Text(next.displayName)));
                }
                if (connections.Count > 0) summaryText.text += "\n" +
                    L10n.Format("map.connections", "연결된 길 · {0}", string.Join(" · ", connections));
            }
            if (shadowsText != null) shadowsText.text = detailed
                ? L10n.Get("map.shadows", "출현 그림자 · ") + Join(region.featuredShadows) : L10n.Get("map.shadows_unknown", "출현 그림자 · ???");
            if (bossText != null) bossText.text = detailed && !string.IsNullOrEmpty(region.bossShadowName)
                ? L10n.Get("map.boss", "주요 배역 · ") + L10n.Text(region.bossShadowName) : "";
            if (detailAccent != null) detailAccent.color = detailed ? region.AccentColor
                : rumored ? new Color(.58f,.47f,.78f,1f) : new Color(.2f,.18f,.25f,1f);
            if (travelButton != null)
            {
                bool canFastTravel = state == RegionMapState.Visited && region.isSettlement;
                travelButton.interactable = canFastTravel;
                var label = travelButton.GetComponentInChildren<Text>();
                if (label != null) label.text = state == RegionMapState.Current ? L10n.Get("map.current", "현재 위치")
                    : canFastTravel ? L10n.Get("map.travel", "마을로 이동")
                    : state == RegionMapState.Visited ? L10n.Get("map.field_only", "빠른 이동 불가")
                    : state == RegionMapState.Unlocked ? L10n.Get("map.explore", "직접 탐험 필요") : L10n.Get("common.locked", "잠김");
            }
            if (feedbackText != null) feedbackText.text = rumored
                ? L10n.Get("map.rumor_feedback", "정확한 위치는 아직 모릅니다. 인접 지역에서 단서를 찾아보세요.")
                : state == RegionMapState.Unlocked
                ? L10n.Get("map.first_visit", "출구를 통해 직접 방문하면 상세 정보가 기록됩니다.")
                : state == RegionMapState.Visited && !region.isSettlement
                    ? L10n.Get("map.settlement_only", "빠른 이동은 방문한 극단 마을에서만 이용할 수 있습니다.") : "";
            if (feedbackText != null && !rumored && state != RegionMapState.Current && state != RegionMapState.Locked)
            {
                var route = RegionProgress.FindKnownRoute(region);
                string guidance = route.Count > 1
                    ? L10n.Format("map.walk_next", "도보 경로 · 먼저 {0} 방면 출구로 이동 ({1}개 지역 이동)",
                        L10n.Get($"region.{route[1].regionId}.name", L10n.Text(route[1].displayName)), route.Count - 1)
                    : L10n.Get("map.route_unavailable", "현재 위치에서 이어지는 알려진 경로가 없습니다.");
                feedbackText.text = guidance;
            }
        }

        private void ShowEmpty()
        {
            if (nameText != null) nameText.text = L10n.Get("map.no_info", "지역 정보 없음");
            foreach (var label in new[] { actText, environmentText, levelText, summaryText, shadowsText, bossText, feedbackText })
                if (label != null) label.text = string.Empty;
            if (travelButton != null) travelButton.interactable = false;
        }

        private static string Join(string[] values) => values == null || values.Length == 0
            ? L10n.Get("map.unidentified", "미확인") : string.Join(" · ", values.Select(L10n.Text));

        private void ReleasePlayer()
        {
            if (!_lockedPlayer) return;
            PlayerController.Instance?.Unlock();
            _lockedPlayer = false;
        }
    }
}
