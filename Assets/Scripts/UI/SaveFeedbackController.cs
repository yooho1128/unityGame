using System.Collections;
using ShadowTheater.Save;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>수동·자동 저장 완료를 방해되지 않는 짧은 HUD로 알린다.</summary>
    public class SaveFeedbackController : MonoBehaviour
    {
        public static SaveFeedbackController Instance { get; private set; }

        [SerializeField] private CanvasGroup root;
        [SerializeField] private Text label;
        [SerializeField, Min(0f)] private float holdDuration = 1.15f;
        [SerializeField, Min(.01f)] private float fadeDuration = .2f;

        private Coroutine _routine;
        private SaveManager _saveManager;
        private string _messageKey;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            HideImmediate();
        }

        private void OnEnable()
        {
            BindSaveManager();
            L10n.Changed += RefreshLabel;
        }

        private void Start() => BindSaveManager();

        private void BindSaveManager()
        {
            if (_saveManager == SaveManager.Instance) return;
            UnbindSaveManager();
            _saveManager = SaveManager.Instance;
            if (_saveManager == null) return;
            _saveManager.OnSaved += ShowSaved;
            _saveManager.OnSaveFailed += ShowSaveFailed;
        }

        private void UnbindSaveManager()
        {
            if (_saveManager != null)
            {
                _saveManager.OnSaved -= ShowSaved;
                _saveManager.OnSaveFailed -= ShowSaveFailed;
            }
            _saveManager = null;
        }

        private void OnDisable()
        {
            UnbindSaveManager();
            L10n.Changed -= RefreshLabel;
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            HideImmediate();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ShowSaved()
        {
            ShowMessage(L10n.Get("save.recorded", "기억을 기록했습니다"));
            _messageKey = "save.recorded";
        }

        public void ShowSaveFailed()
        {
            ShowMessage(L10n.Get("save.failed", "저장하지 못했습니다. 현재 게임을 유지하고 다시 저장해 주세요."), 3.5f);
            _messageKey = "save.failed";
        }

        public void ShowMessage(string message) => ShowMessage(message, holdDuration);

        private void ShowMessage(string message, float duration)
        {
            if (!isActiveAndEnabled || root == null) return;
            if (_routine != null) StopCoroutine(_routine);
            _messageKey = null;
            if (label != null) label.text = message ?? string.Empty;
            _routine = StartCoroutine(ShowRoutine(duration));
        }

        private IEnumerator ShowRoutine(float duration)
        {
            root.gameObject.SetActive(true);
            yield return FadeTo(1f);
            yield return new WaitForSecondsRealtime(duration);
            yield return FadeTo(0f);
            root.gameObject.SetActive(false);
            _routine = null;
        }

        private IEnumerator FadeTo(float target)
        {
            float start = root.alpha;
            for (float elapsed = 0f; elapsed < fadeDuration; elapsed += Time.unscaledDeltaTime)
            {
                root.alpha = Mathf.Lerp(start, target, elapsed / fadeDuration);
                yield return null;
            }
            root.alpha = target;
        }

        private void RefreshLabel()
        {
            if (label != null && !string.IsNullOrEmpty(_messageKey))
                label.text = _messageKey == "save.failed"
                    ? L10n.Get("save.failed", "저장하지 못했습니다. 현재 게임을 유지하고 다시 저장해 주세요.")
                    : L10n.Get("save.recorded", "기억을 기록했습니다");
        }

        private void HideImmediate()
        {
            if (root == null) return;
            root.alpha = 0f;
            root.interactable = false;
            root.blocksRaycasts = false;
            root.gameObject.SetActive(false);
        }
    }
}
