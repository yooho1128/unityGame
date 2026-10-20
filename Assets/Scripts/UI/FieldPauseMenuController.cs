using System.Collections;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>필드 탐색 중 저장, 음량 설정, 타이틀 복귀를 제공하는 모바일 메뉴.</summary>
    public class FieldPauseMenuController : MonoBehaviour
    {
        public static FieldPauseMenuController Instance { get; private set; }

        [SerializeField] private GameObject root;
        [SerializeField] private GameObject titleConfirmRoot;
        [SerializeField] private Text regionText;
        [SerializeField] private Text playTimeText;
        [SerializeField] private Text goldText;
        [SerializeField] private Text journeyText;
        [SerializeField] private Text feedbackText;
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider ambienceSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Text masterValue;
        [SerializeField] private Text musicValue;
        [SerializeField] private Text ambienceValue;
        [SerializeField] private Text sfxValue;
        [SerializeField] private Button recoverPositionButton;

        private bool _lockedPlayer;
        private bool _refreshing;
        private bool _returning;
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            root?.SetActive(false);
            titleConfirmRoot?.SetActive(false);
        }

        private void OnEnable()
        {
            L10n.Changed += RefreshIfOpen;
            GameSettings.Changed += RefreshAudioIfOpen;
        }

        private void OnDisable()
        {
            L10n.Changed -= RefreshIfOpen;
            GameSettings.Changed -= RefreshAudioIfOpen;
            ReleasePlayer();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ReleasePlayer();
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER && (UNITY_EDITOR || UNITY_STANDALONE)
            if (Input.GetKeyDown(KeyCode.Escape)) HandleBack();
#elif UNITY_ANDROID
            if (Input.GetKeyDown(KeyCode.Escape)) HandleBack();
#endif
        }

        private void HandleBack()
        {
            if (QuestLogController.Instance != null && QuestLogController.Instance.IsOpen)
            { QuestLogController.Instance.Close(); return; }
            if (BossInvestigationJournalController.Instance != null && BossInvestigationJournalController.Instance.IsOpen)
            { BossInvestigationJournalController.Instance.Close(); return; }
            if (FieldInventoryController.Instance != null && FieldInventoryController.Instance.IsOpen)
            { FieldInventoryController.Instance.Close(); return; }
            if (SettlementShopController.Instance != null && SettlementShopController.Instance.IsOpen)
            { SettlementShopController.Instance.Close(); return; }
            if (WorldMapController.Instance != null && WorldMapController.Instance.IsOpen)
            { WorldMapController.Instance.Close(); return; }
            if (PartyStorageController.Instance != null && PartyStorageController.Instance.IsOpen)
            { PartyStorageController.Instance.Close(); return; }
            if (ScriptBookController.Instance != null && ScriptBookController.Instance.IsOpen)
            { ScriptBookController.Instance.Close(); return; }
            Toggle();
        }

        public void Toggle()
        {
            if (_returning) return;
            if (IsOpen) Close(); else Open();
        }

        public void Open()
        {
            if (IsOpen || SaveManager.Current == null || !CanOpen()) return;
            IsOpen = true;
            FieldUiModalState.Show(root);
            titleConfirmRoot?.SetActive(false);
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
            if (!IsOpen || _returning) return;
            IsOpen = false;
            titleConfirmRoot?.SetActive(false);
            root?.SetActive(false);
            ReleasePlayer();
        }

        public void SaveNow()
        {
            if (!IsOpen || _returning || SaveManager.Instance == null || SaveManager.Current == null) return;
            bool saved = SaveManager.Instance.TrySave();
            if (feedbackText != null) feedbackText.text = saved
                ? L10n.Get("menu.saved", "저장했습니다.")
                : L10n.Get("save.failed", "저장하지 못했습니다. 현재 게임을 유지하고 다시 저장해 주세요.");
            RefreshStatus();
        }

        public void RecoverSafePosition()
        {
            var save = SaveManager.Current;
            var player = PlayerController.Instance;
            if (!IsOpen || _returning || save == null || player == null) return;

            string currentMap = FieldGrid.Current != null ? FieldGrid.Current.MapId : save.mapId;
            if (!string.IsNullOrEmpty(save.checkpointMapId) && save.checkpointMapId != currentMap)
            {
                string checkpointMap = save.checkpointMapId;
                var checkpoint = new Vector2Int(save.checkpointX, save.checkpointY);
                Close();
                if (MapLoader.Instance != null && MapLoader.Instance.TravelTo(
                        checkpointMap, checkpoint, FacingDir.Down, true)) return;
                Open();
                if (feedbackText != null)
                    feedbackText.text = L10n.Get("menu.recover_failed", "안전 위치로 이동할 수 없습니다.");
                return;
            }

            var requested = new Vector2Int(save.checkpointX, save.checkpointY);
            var region = RegionRepository.GetByScene(currentMap);
            var fallback = region != null ? region.ArrivalCell : player.Cell;
            if (!player.SnapToSafeCell(requested, fallback, out Vector2Int resolved))
            {
                if (feedbackText != null)
                    feedbackText.text = L10n.Get("menu.recover_failed", "안전 위치로 이동할 수 없습니다.");
                return;
            }

            save.mapId = currentMap;
            save.tileX = save.checkpointX = resolved.x;
            save.tileY = save.checkpointY = resolved.y;
            save.checkpointMapId = currentMap;
            player.SetFacing(FacingDir.Down);
            SaveManager.Instance.Save(false);
            if (feedbackText != null)
                feedbackText.text = L10n.Get("menu.recovered", "가까운 안전 위치로 복귀했습니다.");
            RefreshStatus();
        }

        public void OpenPartyManagement()
        {
            if (!IsOpen || _returning) return;
            Close();
            PartyStorageController.Instance?.Open();
        }

        public void OpenInventory()
        {
            if (!IsOpen || _returning) return;
            Close();
            FieldInventoryController.Instance?.Open();
        }

        public void OpenQuestLog()
        {
            if (!IsOpen || _returning) return;
            Close(); QuestLogController.Instance?.Open();
        }

        public void OpenBossJournal()
        {
            if (!IsOpen || _returning) return;
            Close(); BossInvestigationJournalController.Instance?.Open();
        }

        public void RequestReturnToTitle()
        {
            if (!IsOpen || _returning) return;
            titleConfirmRoot?.SetActive(true);
        }

        public void CancelReturnToTitle() => titleConfirmRoot?.SetActive(false);

        public void ConfirmReturnToTitle()
        {
            if (!IsOpen || _returning) return;
            StartCoroutine(ReturnToTitleRoutine());
        }

        public void SetMaster(float value)
        {
            if (!_refreshing) GameSettings.Instance?.SetMasterVolume(value);
            RefreshAudioLabels();
        }

        public void SetMusic(float value)
        {
            if (!_refreshing) GameSettings.Instance?.SetMusicVolume(value);
            RefreshAudioLabels();
        }

        public void SetAmbience(float value)
        {
            if (!_refreshing) GameSettings.Instance?.SetAmbienceVolume(value);
            RefreshAudioLabels();
        }

        public void SetSfx(float value)
        {
            if (!_refreshing) GameSettings.Instance?.SetSfxVolume(value);
            RefreshAudioLabels();
        }

        private bool CanOpen()
        {
            return FieldUiModalState.CanOpen(FieldUiModal.Pause);
        }

        private void Refresh()
        {
            RefreshStatus();
            RefreshAudio();
            if (feedbackText != null) feedbackText.text = string.Empty;
        }

        private void RefreshStatus()
        {
            var save = SaveManager.Current;
            if (save == null) return;
            var region = RegionRepository.GetByScene(save.mapId);
            string regionName = region != null ? L10n.Get($"region.{region.regionId}.name", L10n.Text(region.displayName)) : save.mapId;
            if (regionText != null)
            {
                string worldText = string.Empty;
                if (region != null)
                {
                    FieldWorldSnapshot world = FieldWorldState.Current(region);
                    worldText = " · " + L10n.Text(FieldWorldState.TimeLabel(world.timeOfDay)) + " · " +
                                L10n.Text(FieldWorldState.WeatherLabel(world.weather));
                }
                regionText.text = L10n.Format("menu.region_value", "현재 지역 · {0}", regionName) + worldText;
            }
            if (playTimeText != null)
            {
                int total = Mathf.Max(0, Mathf.FloorToInt(save.playTimeSeconds));
                string time = $"{total / 3600:00}:{total / 60 % 60:00}:{total % 60:00}";
                playTimeText.text = L10n.Format("menu.playtime_value", "플레이 시간 · {0}", time);
            }
            if (goldText != null) goldText.text = L10n.Format("menu.gold_value", "보유 금화 · {0:N0}", save.gold);
            if (journeyText != null)
            {
                JourneyOverview journey = JourneyOverviewService.Build();
                journeyText.text = L10n.Format("menu.journey_overview",
                    "여정 {0}% · {1}\n탐험 {2}/{3} · 기록 {4}/{5} · 퀘스트 {6}/{7} · 엔딩 {8}/{9}\n다음 · {10}",
                    journey.percent, JourneyRank(journey.rankIndex), journey.visitedRegions, journey.totalRegions,
                    journey.recordedShadows, journey.totalShadows, journey.completedQuests, journey.totalQuests,
                    journey.unlockedEndings, journey.totalEndings, JourneyGoal(journey));
            }
            if (recoverPositionButton != null) recoverPositionButton.interactable = !_returning;
        }

        private static string JourneyRank(int index)
        {
            string[] fallback = { "막을 올리는 중", "기억의 탐색자", "숙련된 기록자", "극단의 연출가", "전설의 목격자", "모든 기억의 주인" };
            int safe = Mathf.Clamp(index, 0, fallback.Length - 1);
            return L10n.Get("journey.rank." + safe, fallback[safe]);
        }

        private static string JourneyGoal(JourneyOverview journey)
        {
            switch (journey.goalKind)
            {
                case JourneyGoalKind.Quest:
                    return L10n.Format("journey.goal.quest", "{0}: {1} ({2}/{3})",
                        L10n.Text(journey.goalTitle), L10n.Text(journey.goalDetail), journey.goalCurrent, journey.goalTarget);
                case JourneyGoalKind.Region:
                    return L10n.Format("journey.goal.region", "{0} 탐험 · {1}",
                        L10n.Text(journey.goalTitle), L10n.Text(journey.goalDetail));
                case JourneyGoalKind.Collection:
                    return L10n.Format("journey.goal.collection", "그림자 기록 {0}/{1}",
                        journey.goalCurrent, journey.goalTarget);
                case JourneyGoalKind.Ending:
                    return L10n.Get("journey.goal.ending", "새로운 엔딩 조건 탐색");
                default:
                    return L10n.Get("journey.goal.complete", "모든 여정을 완료했습니다");
            }
        }

        private void RefreshAudio()
        {
            _refreshing = true;
            if (masterSlider != null) masterSlider.value = GameSettings.MasterVolume;
            if (musicSlider != null) musicSlider.value = GameSettings.MusicVolume;
            if (ambienceSlider != null) ambienceSlider.value = GameSettings.AmbienceVolume;
            if (sfxSlider != null) sfxSlider.value = GameSettings.SfxVolume;
            _refreshing = false;
            RefreshAudioLabels();
        }

        private void RefreshAudioLabels()
        {
            if (masterValue != null) masterValue.text = Percent(GameSettings.MasterVolume);
            if (musicValue != null) musicValue.text = Percent(GameSettings.MusicVolume);
            if (ambienceValue != null) ambienceValue.text = Percent(GameSettings.AmbienceVolume);
            if (sfxValue != null) sfxValue.text = Percent(GameSettings.SfxVolume);
        }

        private void RefreshIfOpen() { if (IsOpen) Refresh(); }
        private void RefreshAudioIfOpen() { if (IsOpen && !_refreshing) RefreshAudio(); }

        private IEnumerator ReturnToTitleRoutine()
        {
            _returning = true;
            titleConfirmRoot?.SetActive(false);
            if (!Application.CanStreamedLevelBeLoaded("Title"))
            {
                _returning = false;
                if (feedbackText != null) feedbackText.text = L10n.Get("menu.title_unavailable", "타이틀 화면을 열 수 없습니다. 현재 게임을 계속할 수 있습니다.");
                yield break;
            }
            if (SaveManager.Instance == null || !SaveManager.Instance.TrySave())
            {
                _returning = false;
                if (feedbackText != null) feedbackText.text = L10n.Get("save.failed", "저장하지 못했습니다. 현재 게임을 유지하고 다시 저장해 주세요.");
                yield break;
            }
            if (feedbackText != null) feedbackText.text = L10n.Get("menu.returning", "저장 후 타이틀로 돌아갑니다…");
            if (ScreenFader.Instance != null) yield return ScreenFader.Instance.FadeOut(.3f);
            ReleasePlayer();
            SceneManager.LoadSceneAsync("Title");
        }

        private void ReleasePlayer()
        {
            if (!_lockedPlayer) return;
            PlayerController.Instance?.Unlock();
            _lockedPlayer = false;
        }

        private static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";
    }
}
