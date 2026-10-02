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
        [SerializeField] private StarterSelectionController starterSelection;
        [SerializeField] private EndingGalleryController endingGallery;
        [Header("새 게임 시작 위치")]
        [SerializeField] private string firstScene = "Prologue";
        [SerializeField] private Vector2Int firstCell = new Vector2Int(0, -7);
        [SerializeField] private FacingDir firstFacing = FacingDir.Up;
        [SerializeField, Min(1)] private int starterLevel = 5;
        [SerializeField] private ItemData startingItem;
        [SerializeField, Min(0)] private int startingItemCount = 3;
        private bool _startingNewCycle;

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => SaveManager.Instance != null && MapLoader.Instance != null);
            if (SaveManager.Current == null && SaveManager.Instance.HasSave()) SaveManager.Instance.Load();
            ShowTitle();
        }

        public void NewGame()
        {
            if (starterSelection == null) return;
            _startingNewCycle = false;
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
                RefreshContinue();
                return;
            }

            if (titleRoot != null) titleRoot.SetActive(false);
            if (!MapLoader.Instance.LoadSavedMap()) ShowTitle();
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
            if (titleRoot != null) titleRoot.SetActive(true);
            RefreshContinue();
        }

        private void RefreshContinue()
        {
            if (continueButton != null) continueButton.interactable = SaveManager.Instance != null &&
                                                                      SaveManager.Instance.HasSave();
            if (newCycleButton != null)
            {
                bool available = SaveManager.Instance != null && SaveManager.Instance.CanStartNewCycle;
                newCycleButton.gameObject.SetActive(available);
                newCycleButton.interactable = available;
            }
        }
    }
}
