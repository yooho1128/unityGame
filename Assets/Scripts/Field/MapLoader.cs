using System.Collections;
using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowTheater.Field
{
    /// <summary>씬 기반 지역 이동. 목표 맵과 도착 타일을 먼저 저장한 뒤 페이드 전환한다.</summary>
    public class MapLoader : MonoBehaviour
    {
        public static MapLoader Instance { get; private set; }

        [SerializeField, Min(0f)] private float arrivalPortalCooldown = 0.65f;
        public bool IsTransitioning { get; private set; }
        public bool CanTravel => !IsTransitioning && Time.unscaledTime >= _travelEnabledAt;

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
            bool setCheckpoint = false)
        {
            if (!CanTravel || string.IsNullOrWhiteSpace(sceneName) || SaveManager.Current == null) return false;
            if (!RegionProgress.CanEnterScene(sceneName))
            {
                Debug.LogWarning($"[MapLoader] 아직 해금되지 않은 지역입니다: {sceneName}");
                return false;
            }
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[MapLoader] Build Settings에 씬이 없습니다: {sceneName}");
                return false;
            }
            StartCoroutine(TravelRoutine(sceneName, arrivalCell, facing, setCheckpoint));
            return true;
        }

        public bool LoadSavedMap()
        {
            var save = SaveManager.Current;
            if (save == null) return false;
            return TravelTo(save.mapId, new Vector2Int(save.tileX, save.tileY), (FacingDir)save.facing);
        }

        private IEnumerator TravelRoutine(string sceneName, Vector2Int arrivalCell, FacingDir facing,
            bool setCheckpoint)
        {
            IsTransitioning = true;
            var player = PlayerController.Instance;
            if (player != null)
            {
                player.MoveInput = Vector2.zero;
                player.Lock();
            }

            var oldFader = ScreenFader.Instance;
            FieldAmbientAudio.Instance?.BeginFadeOut();
            if (oldFader != null) yield return oldFader.FadeOut();

            var save = SaveManager.Current;
            save.mapId = sceneName;
            save.tileX = arrivalCell.x;
            save.tileY = arrivalCell.y;
            save.facing = (int)facing;
            RegionProgress.MarkVisitedScene(sceneName);
            if (setCheckpoint)
            {
                save.checkpointMapId = sceneName;
                save.checkpointX = arrivalCell.x;
                save.checkpointY = arrivalCell.y;
            }
            // 이전 씬의 BeforeSave 위치 캡처가 목표 좌표를 덮지 않게 한다.
            SaveManager.Instance.Save(false);

            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError($"[MapLoader] 씬 로드를 시작하지 못했습니다: {sceneName}");
                IsTransitioning = false;
                if (player != null) player.Unlock();
                yield break;
            }
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
            IsTransitioning = false;
        }
    }
}
