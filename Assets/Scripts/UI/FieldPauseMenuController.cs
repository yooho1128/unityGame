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
        [SerializeField] private Text feedbackText;
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider ambienceSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Text masterValue;
        [SerializeField] private Text musicValue;
        [SerializeField] private Text ambienceValue;
        [SerializeField] private Text sfxValue;

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
            if (Input.GetKeyDown(KeyCode.Escape)) Toggle();
#endif
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
            root?.SetActive(true);
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
            if (SaveManager.Instance == null || SaveManager.Current == null) return;
            SaveManager.Instance.Save();
            if (feedbackText != null) feedbackText.text = L10n.Get("menu.saved", "저장했습니다.");
            RefreshStatus();
        }

        public void OpenPartyManagement()
        {
            if (!IsOpen || _returning) return;
            Close();
            PartyStorageController.Instance?.Open();
        }

        public void RequestReturnToTitle()
        {
            if (!IsOpen || _returning) return;
            titleConfirmRoot?.SetActive(true);
        }

        public void CancelReturnToTitle() => titleConfirmRoot?.SetActive(false);

        public void ConfirmReturnToTitle()
        {
            if (_returning) return;
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
            if (GameFlowController.Instance != null && GameFlowController.Instance.IsInBattle) return false;
            if (DialogueController.Instance != null && DialogueController.Instance.IsPlaying) return false;
            if (ScriptBookController.Instance != null && ScriptBookController.Instance.IsOpen) return false;
            if (PartyStorageController.Instance != null && PartyStorageController.Instance.IsOpen) return false;
            if (WorldMapController.Instance != null && WorldMapController.Instance.IsOpen) return false;
            if (SettlementShopController.Instance != null && SettlementShopController.Instance.IsOpen) return false;
            return true;
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
            if (regionText != null) regionText.text = L10n.Format("menu.region_value", "현재 지역 · {0}", regionName);
            if (playTimeText != null)
            {
                int total = Mathf.Max(0, Mathf.FloorToInt(save.playTimeSeconds));
                string time = $"{total / 3600:00}:{total / 60 % 60:00}:{total % 60:00}";
                playTimeText.text = L10n.Format("menu.playtime_value", "플레이 시간 · {0}", time);
            }
            if (goldText != null) goldText.text = L10n.Format("menu.gold_value", "보유 금화 · {0:N0}", save.gold);
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
            SaveManager.Instance?.Save();
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
