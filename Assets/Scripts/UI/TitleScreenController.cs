using System.Collections;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>타이틀 → 이어하기 또는 스타터 선택 → 첫 필드 진입 흐름.</summary>
    public class TitleScreenController : MonoBehaviour
    {
        [SerializeField] private GameObject titleRoot;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button newCycleButton;
        [SerializeField] private GameObject newGameConfirmRoot;
        [SerializeField] private Text saveSummaryText;
        [SerializeField] private Text feedbackText;
        [SerializeField] private StarterSelectionController starterSelection;
        [SerializeField] private EndingGalleryController endingGallery;
        [SerializeField] private SettingsPanelController settingsPanel;
        [Header("새 게임 시작 위치")]
        [SerializeField] private string firstScene = "Prologue";
        [SerializeField] private Vector2Int firstCell = new Vector2Int(0, -7);
        [SerializeField] private FacingDir firstFacing = FacingDir.Up;
        [SerializeField, Min(1)] private int starterLevel = 5;
        [SerializeField] private ItemData startingItem;
        [SerializeField, Min(0)] private int startingItemCount = 3;
        private bool _startingNewCycle;

        private void OnEnable() => L10n.Changed += RefreshLocalizedStatus;
        private void OnDisable() => L10n.Changed -= RefreshLocalizedStatus;

        private void RefreshLocalizedStatus()
        {
            if (SaveManager.Instance != null) RefreshContinue();
        }

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => SaveManager.Instance != null && MapLoader.Instance != null);
            if (SaveManager.Current == null && SaveManager.Instance.HasSave())
            {
                bool loaded = SaveManager.Instance.Load();
                if (!loaded) ShowLoadFailure();
                else if (SaveManager.Instance.LastLoadUsedBackup)
                    SetFeedback(L10n.Get("title.backup_recovered", "손상된 저장 대신 백업 기록을 복구했습니다."), false);
            }
            ShowTitle();
        }

        public void NewGame()
        {
            if (starterSelection == null) return;
            if (SaveManager.Instance != null && SaveManager.Instance.HasSave() && newGameConfirmRoot != null)
            {
                newGameConfirmRoot?.SetActive(true);
                return;
            }
            BeginNewGameSelection();
        }

        public void ConfirmNewGame()
        {
            newGameConfirmRoot?.SetActive(false);
            BeginNewGameSelection();
        }

        public void CancelNewGame()
        {
            newGameConfirmRoot?.SetActive(false);
        }

        private void BeginNewGameSelection()
        {
            _startingNewCycle = false;
            SetFeedback(string.Empty, false);
            if (titleRoot != null) titleRoot.SetActive(false);
            starterSelection.Show(StartWithStarter);
        }

        public void NewCycle()
        {
            if (SaveManager.Current == null && SaveManager.Instance.HasSave()) SaveManager.Instance.Load();
            if (!SaveManager.Instance.CanStartNewCycle || starterSelection == null) return;
            _startingNewCycle = true;
            if (titleRoot != null) titleRoot.SetActive(false);
            starterSelection.Show(StartWithStarter);
        }

        public void ContinueGame()
        {
            if (!SaveManager.Instance.HasSave() || !SaveManager.Instance.Load())
            {
                ShowLoadFailure();
                RefreshContinue();
                return;
            }

            SetFeedback(string.Empty, false);
            if (titleRoot != null) titleRoot.SetActive(false);
            if (!MapLoader.Instance.LoadSavedMap())
            {
                ShowTitle();
                SetFeedback(L10n.Get("title.scene_load_failed", "저장된 지역을 열 수 없습니다. 전체 게임 생성을 다시 실행해 주세요."), true);
            }
        }

        public void CancelStarterSelection()
        {
            _startingNewCycle = false;
            starterSelection?.Hide();
            ShowTitle();
        }

        private void StartWithStarter(ShadowData starter)
        {
            if (starter == null) { ShowTitle(); return; }
            var save = _startingNewCycle
                ? SaveManager.Instance.NewCycle(starter, starterLevel)
                : SaveManager.Instance.NewGame(starter, starterLevel);
            _startingNewCycle = false;
            if (save == null) { ShowTitle(); return; }
            save.mapId = firstScene;
            save.tileX = save.checkpointX = firstCell.x;
            save.tileY = save.checkpointY = firstCell.y;
            save.facing = (int)firstFacing;
            save.checkpointMapId = firstScene;
            if (startingItem != null && startingItemCount > 0)
                SaveManager.AddItem(startingItem.itemId, startingItemCount);
            SaveManager.Instance.Save(false);

            if (!MapLoader.Instance.TravelTo(firstScene, firstCell, firstFacing, true)) ShowTitle();
        }

        private void ShowTitle()
        {
            starterSelection?.Hide();
            endingGallery?.Close();
            settingsPanel?.Close();
            newGameConfirmRoot?.SetActive(false);
            if (titleRoot != null) titleRoot.SetActive(true);
            RefreshContinue();
        }

        private void RefreshContinue()
        {
            if (continueButton != null) continueButton.interactable = SaveManager.Instance != null &&
                                                                      SaveManager.Instance.HasSave();
            RefreshSaveSummary();
            if (newCycleButton != null)
            {
                bool available = SaveManager.Instance != null && SaveManager.Instance.CanStartNewCycle;
                newCycleButton.gameObject.SetActive(available);
                newCycleButton.interactable = available;
            }
        }

        private void RefreshSaveSummary()
        {
            if (saveSummaryText == null) return;
            var save = SaveManager.Current;
            if (save == null)
            {
                saveSummaryText.text = SaveManager.Instance != null && SaveManager.Instance.HasSave()
                    ? L10n.Get("title.save_unavailable", "저장 기록을 확인할 수 없습니다") : L10n.Get("title.no_save", "새로운 기억을 시작하세요");
                return;
            }
            var region = ShadowTheater.Story.RegionRepository.GetByScene(save.mapId);
            string regionName = region != null ? L10n.Get($"region.{region.regionId}.name", L10n.Text(region.displayName)) : save.mapId;
            int seconds = Mathf.Max(0, Mathf.FloorToInt(save.playTimeSeconds));
            string time = $"{seconds / 3600:00}:{seconds / 60 % 60:00}";
            saveSummaryText.text = L10n.Format("title.save_summary", "{0} · {1} · 파티 {2}명", regionName, time, save.party?.Count ?? 0);
        }

        private void ShowLoadFailure()
        {
            string message;
            switch (SaveManager.Instance != null ? SaveManager.Instance.LastLoadFailure : SaveLoadFailure.NoFile)
            {
                case SaveLoadFailure.FutureVersion:
                    message = L10n.Get("title.load_future", "더 최신 버전에서 만든 저장 기록입니다. 게임을 업데이트해 주세요."); break;
                case SaveLoadFailure.Corrupt:
                    message = L10n.Get("title.load_corrupt", "저장 기록과 백업을 읽을 수 없습니다. 새 게임은 기존 파일을 덮어씁니다."); break;
                default:
                    message = L10n.Get("title.load_missing", "이어갈 저장 기록이 없습니다."); break;
            }
            SetFeedback(message, true);
        }

        private void SetFeedback(string message, bool error)
        {
            if (feedbackText == null) return;
            feedbackText.text = message ?? string.Empty;
            feedbackText.color = error ? new Color(1f,.48f,.55f) : new Color(.55f,.88f,.76f);
        }
    }
}
