using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>40개 지역의 발견 상태와 출현 그림자를 보여주고 방문 지역 빠른 이동을 제공한다.</summary>
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
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            root?.SetActive(false);
            nodeTemplate?.gameObject.SetActive(false);
        }

        private void OnEnable() => L10n.Changed += RefreshIfOpen;

        private void OnDestroy() { if (Instance == this) Instance = null; ReleasePlayer(); }
        private void OnDisable() { L10n.Changed -= RefreshIfOpen; if (IsOpen) Close(); }
        private void RefreshIfOpen() { if (IsOpen) Refresh(); }

        public void Toggle() { if (IsOpen) Close(); else Open(); }

        public void Open()
        {
            if (IsOpen || SaveManager.Current == null) return;
            if (GameFlowController.Instance != null && GameFlowController.Instance.IsInBattle) return;
            if (DialogueController.Instance != null && DialogueController.Instance.IsPlaying) return;
            if (ScriptBookController.Instance != null && ScriptBookController.Instance.IsOpen) return;
            if (PartyStorageController.Instance != null && PartyStorageController.Instance.IsOpen) return;
            IsOpen = true;
            root?.SetActive(true);
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
            if (_selected == null || RegionProgress.GetState(_selected) != RegionMapState.Visited) return;
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
            foreach (var go in _spawned) Destroy(go);
            _spawned.Clear();
            var ordered = RegionRepository.Ordered().ToList();
            foreach (var region in ordered)
            {
                var view = Instantiate(nodeTemplate, contentRoot);
                view.Bind(region, RegionProgress.GetState(region), Select);
                _spawned.Add(view.gameObject);
            }
            int visited = SaveManager.Current?.visitedRegionIds?.Count ?? 0;
            if (progressText != null) progressText.text = L10n.Format("map.progress", "방문 {0} / {1}", visited, ordered.Count);
            var current = RegionRepository.GetByScene(SaveManager.Current?.mapId);
            Select(current ?? ordered.FirstOrDefault(x => RegionProgress.GetState(x) != RegionMapState.Locked));
        }

        private void Select(RegionData region)
        {
            _selected = region;
            if (region == null) { ShowEmpty(); return; }
            var state = RegionProgress.GetState(region);
            bool known = state != RegionMapState.Locked;
            if (actText != null) actText.text = known ? L10n.Get($"region.{region.regionId}.act", L10n.Text(region.actTitle)) : L10n.Get("map.undiscovered_act", "아직 공개되지 않은 막");
            if (nameText != null) nameText.text = known ? L10n.Get($"region.{region.regionId}.name", L10n.Text(region.displayName)) : "???";
            if (environmentText != null) environmentText.text = known ? L10n.Get($"region.{region.regionId}.environment", L10n.Text(region.environment)) : L10n.Get("map.discover_route", "경로를 발견해야 합니다");
            if (levelText != null) levelText.text = known
                ? L10n.Format("map.level", "권장 Lv.{0}–{1}", region.recommendedLevelMin, region.recommendedLevelMax) : L10n.Get("map.level_unknown", "권장 레벨 ???");
            if (summaryText != null) summaryText.text = known ? L10n.Get($"region.{region.regionId}.summary", L10n.Text(region.summary)) : L10n.Get("map.locked_hint", "인접 지역을 탐험하면 길이 열립니다.");
            if (shadowsText != null) shadowsText.text = known
                ? L10n.Get("map.shadows", "출현 그림자 · ") + Join(region.featuredShadows) : L10n.Get("map.shadows_unknown", "출현 그림자 · ???");
            if (bossText != null) bossText.text = known && !string.IsNullOrEmpty(region.bossShadowName)
                ? L10n.Get("map.boss", "주요 배역 · ") + L10n.Text(region.bossShadowName) : "";
            if (detailAccent != null) detailAccent.color = known ? region.AccentColor : new Color(.2f,.18f,.25f,1f);
            if (travelButton != null)
            {
                travelButton.interactable = state == RegionMapState.Visited;
                var label = travelButton.GetComponentInChildren<Text>();
                if (label != null) label.text = state == RegionMapState.Current ? L10n.Get("map.current", "현재 위치")
                    : state == RegionMapState.Visited ? L10n.Get("map.travel", "빠른 이동") : state == RegionMapState.Unlocked ? L10n.Get("map.explore", "직접 탐험 필요") : L10n.Get("common.locked", "잠김");
            }
            if (feedbackText != null) feedbackText.text = state == RegionMapState.Unlocked
                ? L10n.Get("map.first_visit", "인접 지역의 출구를 통해 처음 방문할 수 있습니다.") : "";
        }

        private void ShowEmpty()
        {
            if (nameText != null) nameText.text = L10n.Get("map.no_info", "지역 정보 없음");
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
