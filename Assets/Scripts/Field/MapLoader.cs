using System;
using System.Collections;
using ShadowTheater.Save;
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowTheater.Field
{
    /// <summary>씬 기반 지역 이동. 씬 로드 요청이 수락되면 도착 위치를 저장한다.</summary>
    public class MapLoader : MonoBehaviour
    {
        public static MapLoader Instance { get; private set; }

        [SerializeField, Min(0f)] private float arrivalPortalCooldown = 0.65f;
        public bool IsTransitioning { get; private set; }
        public bool CanTravel => !IsTransitioning && Time.unscaledTime >= _travelEnabledAt
            && (GameFlowController.Instance == null || !GameFlowController.Instance.IsInBattle);

        private float _travelEnabledAt;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool TravelTo(string sceneName, Vector2Int arrivalCell, FacingDir facing,
            bool setCheckpoint = false, Action onFailed = null) =>
            BeginTravel(sceneName, arrivalCell, facing, setCheckpoint, false, onFailed);

        private bool BeginTravel(string sceneName, Vector2Int arrivalCell, FacingDir facing,
            bool setCheckpoint, bool restoreSavedLocation, Action onFailed)
        {
            if (!isActiveAndEnabled || !CanTravel || string.IsNullOrWhiteSpace(sceneName) || SaveManager.Current == null) return false;
            if (!restoreSavedLocation && !RegionProgress.CanEnterScene(sceneName))
            {
                Debug.LogWarning($"[MapLoader] 아직 해금되지 않은 지역입니다: {sceneName}");
                return false;
            }
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[MapLoader] Build Settings에 씬이 없습니다: {sceneName}");
                return false;
            }
            GameFlowController.WritePlayerPosition();
            StartCoroutine(TravelRoutine(sceneName, arrivalCell, facing, setCheckpoint, onFailed));
            return true;
        }

        public bool LoadSavedMap(Action onFailed = null)
        {
            var save = SaveManager.Current;
            if (save == null) return false;
            string sceneName = save.mapId;
            var cell = new Vector2Int(save.tileX, save.tileY);
            if (!CanRestoreScene(sceneName))
            {
                Debug.LogWarning($"[MapLoader] 저장된 씬을 찾지 못해 체크포인트로 복구합니다: {sceneName}");
                sceneName = save.checkpointMapId;
                cell = new Vector2Int(save.checkpointX, save.checkpointY);
            }
            if (!CanRestoreScene(sceneName))
            {
                sceneName = "PrologueTheater";
                cell = new Vector2Int(0, -5);
            }
            // 이어하기는 저장된 위치로 복귀한다. 시작 실패 시 저장 위치는 변경하지 않는다.
            return BeginTravel(sceneName, cell, (FacingDir)Mathf.Clamp(save.facing, 0, 3), false, true, onFailed);
        }

        private static bool CanRestoreScene(string sceneName) => !string.IsNullOrWhiteSpace(sceneName) &&
            RegionRepository.GetByScene(sceneName) != null && Application.CanStreamedLevelBeLoaded(sceneName);

        private IEnumerator TravelRoutine(string sceneName, Vector2Int arrivalCell, FacingDir facing,
            bool setCheckpoint, Action onFailed)
        {
            IsTransitioning = true;
            var save = SaveManager.Current;
            var player = PlayerController.Instance;
            if (player != null)
            {
                player.MoveInput = Vector2.zero;
                player.Lock();
            }

            var oldFader = ScreenFader.Instance;
            FieldAmbientAudio.Instance?.BeginFadeOut();
            if(player!=null)FieldCameraFollow.Instance?.FocusOn(player.transform.position,.28f);
            GameSettings.TryVibrate();
            yield return new WaitForSecondsRealtime(.1f);
            if (oldFader != null) yield return oldFader.FadeOut();

            if (!ReferenceEquals(save, SaveManager.Current))
            {
                yield return RecoverFailedTravel(player, oldFader, onFailed);
                yield break;
            }

            // 로드가 거절되는 경우 목적지 좌표·방문 기록을 남기지 않는다.
            var operation = TryBeginSceneLoad(sceneName);
            if (operation == null)
            {
                yield return RecoverFailedTravel(player, oldFader, onFailed);
                yield break;
            }
            save.mapId = sceneName;
            save.tileX = arrivalCell.x;
            save.tileY = arrivalCell.y;
            save.facing = (int)facing;
            RegionProgress.MarkVisitedScene(sceneName);
            // 거점 지역은 어느 입구나 월드맵으로 도착해도 자동으로 새 부활 지점이 된다.
            bool updateCheckpoint = setCheckpoint || RegionRepository.GetByScene(sceneName)?.isSettlement == true;
            if (updateCheckpoint)
            {
                save.checkpointMapId = sceneName;
                save.checkpointX = arrivalCell.x;
                save.checkpointY = arrivalCell.y;
            }
            // 이전 씬의 BeforeSave 위치 캡처가 목표 좌표를 덮지 않게 한다.
            bool saved = SaveManager.Instance.TrySave(false);

            while (!operation.isDone) yield return null;

            // 새 씬 Awake가 끝난 같은 프레임에 검게 만들어 화면 깜빡임을 막는다.
            var newFader = ScreenFader.Instance;
            if (newFader != null)
            {
                newFader.SetOpaque();
                yield return null;
                yield return newFader.FadeIn();
            }

            _travelEnabledAt = Time.unscaledTime + arrivalPortalCooldown;
            GameFlowController.Instance?.AllowTimeToLeave();
            if (player != null) player.Unlock();
            IsTransitioning = false;
            if (saved) SaveFeedbackController.Instance?.ShowSaved();
            else SaveFeedbackController.Instance?.ShowSaveFailed();
        }

        private static AsyncOperation TryBeginSceneLoad(string sceneName)
        {
            try { return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single); }
            catch (Exception e)
            {
                Debug.LogWarning($"[MapLoader] 씬 로드 시작 실패: {sceneName} · {e.Message}");
                return null;
            }
        }

        private IEnumerator RecoverFailedTravel(PlayerController player, ScreenFader fader, Action onFailed)
        {
            if (fader != null) yield return fader.FadeIn();
            FieldAmbientAudio.Instance?.BeginFadeIn();
            if (player != null) player.Unlock();
            _travelEnabledAt = Time.unscaledTime + arrivalPortalCooldown;
            GameFlowController.Instance?.AllowTimeToLeave();
            IsTransitioning = false;
            SaveFeedbackController.Instance?.ShowMessage(L10n.Get("map.travel_failed", "지역을 열지 못했습니다. 잠시 후 다시 시도해 주세요."));
            onFailed?.Invoke();
        }
    }
}
