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

        private RegionData _selected;
        private WorldMapAtlasView _atlas;
        private bool _lockedPlayer;
        private bool _refreshPending;
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            StylePresentation();
            root?.SetActive(false);
            nodeTemplate?.gameObject.SetActive(false);
        }

        private void OnEnable() { L10n.Changed += RefreshIfOpen; SaveManager.CurrentChanged += RefreshIfOpen; QuestManager.GlobalQuestChanged += RefreshIfOpen; }

        private void OnDestroy() { if (Instance == this) Instance = null; ReleasePlayer(); }
        private void OnDisable() { L10n.Changed -= RefreshIfOpen; SaveManager.CurrentChanged -= RefreshIfOpen; QuestManager.GlobalQuestChanged -= RefreshIfOpen; if (IsOpen) Close(); }
        private void RefreshIfOpen() { if (IsOpen) _refreshPending = true; }
        private void LateUpdate() { if (IsOpen && (_refreshPending || (_atlas != null && _atlas.NeedsLayout))) Refresh(); }

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
            _atlas?.Open();
        }

        public void OpenForTrackedQuest()
            => OpenForObjective(QuestGuidanceService.For(QuestManager.Instance?.TrackedQuest).objective);

        public void OpenForObjective(QuestObjectiveDefinition objective)
        {
            Open();
            if (!IsOpen) return;
            var guidance = QuestGuidanceService.ForObjective(objective);
            if (guidance.destination != null)
            {
                Select(guidance.destination);
                _atlas?.Focus(guidance.destination);
            }
            if (feedbackText != null) feedbackText.text = guidance.text;
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
            if (!JourneyHubPolicy.CanFastTravel(_selected)) return;
            if (!Application.CanStreamedLevelBeLoaded(_selected.sceneName))
            {
                if (feedbackText != null) feedbackText.text = L10n.Get("map.scene_missing", "이 지역의 씬은 아직 제작 중입니다.");
                return;
            }
            var target = _selected;
            Close();
            if (MapLoader.Instance == null || !MapLoader.Instance.TravelTo(target.sceneName, JourneyHubPolicy.Arrival(target),
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
            string selectedId = _selected?.regionId;
            var ordered = RegionRepository.Ordered().ToList();
            if (_atlas == null)
                _atlas = new WorldMapAtlasView((RectTransform)contentRoot,
                    nameText != null ? nameText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), Select);
            _atlas.Refresh();
            int visited = ordered.Count(x => RegionProgress.GetState(x) == RegionMapState.Visited ||
                RegionProgress.GetState(x) == RegionMapState.Current);
            if (progressText != null)
            {
                int nextMilestone = ExplorationRewardService.NextMilestone(visited);
                progressText.text = nextMilestone > 0
                    ? L10n.Format("map.progress", "탐험 {0}/{1} · 다음 {2}", visited, ordered.Count, nextMilestone)
                    : L10n.Format("map.progress_complete", "탐험 {0}/{1} · 완료", visited, ordered.Count);
            }
            var current = RegionRepository.GetByScene(SaveManager.Current?.mapId);
            var selected = ordered.FirstOrDefault(x => x.regionId == selectedId &&
                RegionProgress.GetKnowledge(x) != RegionMapKnowledge.Hidden &&
                RegionProgress.GetKnowledge(x) != RegionMapKnowledge.Locked);
            Select(selected ?? current ?? ordered.FirstOrDefault(x => RegionProgress.GetState(x) != RegionMapState.Locked));
        }

        private void Select(RegionData region)
        {
            _selected = region;
            _atlas?.Select(region);
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
                if(detailed)
                {
                    var records=new List<string>();
                    if(SaveManager.HasFlag("landmark_discovered_"+region.regionId))records.Add("명소 기록");
                    if(SaveManager.HasFlag("ecology_recorded_"+region.regionId))records.Add("생태 기록");
                    if(SaveManager.HasFlag("field_lever_"+region.regionId+"_complete"))records.Add("길 복구");
                    if(SaveManager.HasFlag("secret_passage_"+region.regionId))records.Add("비밀 통로");
                    if(SaveManager.HasFlag("interior_visited_"+region.regionId))records.Add("실내 방문");
                    var regionalPuzzle=RegionalPuzzleCatalog.Get(region.regionId);
                    if(regionalPuzzle!=null)records.Add(regionalPuzzle.title+(SaveManager.HasFlag(regionalPuzzle.CompletionFlag)?" 복원":" · 단서판 조사"));
                    if(records.Count>0)summaryText.text+="\n탐험 기록 · "+string.Join(" · ",records);
                }
            }
            var hub=JourneyHubPolicy.Get(region);
            if(detailed&&hub!=null&&summaryText!=null)
                summaryText.text+="\n보급 거점 · "+hub.name+" · 상점/유료 휴식/귀환 지점";
            if (shadowsText != null) shadowsText.text = detailed
                ? L10n.Get("map.shadows", "출현 그림자 · ") + Join(region.featuredShadows) : L10n.Get("map.shadows_unknown", "출현 그림자 · ???");
            if (bossText != null) bossText.text = detailed && !string.IsNullOrEmpty(region.bossShadowName)
                ? L10n.Get("map.boss", "주요 배역 · ") + L10n.Text(region.bossShadowName) : "";
            if (detailAccent != null) detailAccent.color = detailed ? region.AccentColor
                : rumored ? new Color(.58f,.47f,.78f,1f) : new Color(.2f,.18f,.25f,1f);
            if (travelButton != null)
            {
                bool canFastTravel = JourneyHubPolicy.CanFastTravel(region);
                travelButton.interactable = canFastTravel;
                var label = travelButton.GetComponentInChildren<Text>();
                if (label != null) label.text = state == RegionMapState.Current ? L10n.Get("map.current", "현재 위치")
                    : canFastTravel ? L10n.Get("map.travel_to_hub", "거점으로 이동")
                    : state == RegionMapState.Visited ? L10n.Get("map.field_only", "빠른 이동 불가")
                    : state == RegionMapState.Unlocked ? L10n.Get("map.explore", "직접 탐험 필요") : L10n.Get("common.locked", "잠김");
            }
            if (feedbackText != null) feedbackText.text = rumored
                ? L10n.Get("map.rumor_feedback", "정확한 위치는 아직 모릅니다. 인접 지역에서 단서를 찾아보세요.")
                : state == RegionMapState.Unlocked
                ? L10n.Get("map.first_visit", "출구를 통해 직접 방문하면 상세 정보가 기록됩니다.")
                : state == RegionMapState.Visited && !JourneyHubPolicy.HasServices(region)
                    ? L10n.Get("map.hubs_only", "빠른 이동은 방문한 마을과 보급 거점에서 이용할 수 있습니다.") : "";
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

        private void StylePresentation()
        {
            Color ink = new Color(.035f, .055f, .085f, .99f);
            Color ivory = new Color(.95f, .92f, .85f);
            Color brass = new Color(.85f, .71f, .43f);
            if (root != null)
            {
                var background = root.GetComponent<Image>();
                if (background != null) background.color = ink;
                var heading = root.transform.Find("Header")?.GetComponent<Text>();
                if (heading != null) heading.color = ivory;
                var detail = root.transform.Find("Detail")?.GetComponent<Image>();
                if (detail != null) detail.color = new Color(.07f, .10f, .14f);
            }
            if (nameText != null) nameText.color = ivory;
            if (actText != null) actText.color = brass;
            if (progressText != null) progressText.color = brass;
            if (levelText != null) levelText.color = brass;
            if (summaryText != null)
            {
                summaryText.color = new Color(.79f, .83f, .86f);
                summaryText.lineSpacing = 1.08f;
            }
            if (environmentText != null) environmentText.color = new Color(.64f, .72f, .78f);
            if (shadowsText != null) shadowsText.color = new Color(.79f, .83f, .86f);
            if (feedbackText != null) feedbackText.color = new Color(.55f, .85f, .82f);
            if (travelButton != null)
            {
                var image = travelButton.targetGraphic as Image;
                if (image != null) image.color = brass;
                var label = travelButton.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.color = ink;
                    label.fontStyle = FontStyle.Bold;
                    var outline = label.GetComponent<Outline>();
                    if (outline != null) outline.enabled = false;
                }
                var colors = travelButton.colors;
                colors.highlightedColor = Color.white;
                colors.pressedColor = new Color(.78f, .78f, .78f);
                colors.disabledColor = new Color(.5f, .5f, .5f, .7f);
                colors.fadeDuration = .12f;
                travelButton.colors = colors;
            }
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
