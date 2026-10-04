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

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            HideImmediate();
        }

        private void OnEnable()
        {
            if (SaveManager.Instance != null) SaveManager.Instance.OnSaved += ShowSaved;
            L10n.Changed += RefreshLabel;
        }

        private void OnDisable()
        {
            if (SaveManager.Instance != null) SaveManager.Instance.OnSaved -= ShowSaved;
            L10n.Changed -= RefreshLabel;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ShowSaved()
        {
            ShowMessage(L10n.Get("save.recorded", "기억을 기록했습니다"));
        }

        public void ShowMessage(string message)
        {
            if (!isActiveAndEnabled || root == null) return;
            if (_routine != null) StopCoroutine(_routine);
            if (label != null) label.text = message ?? string.Empty;
            _routine = StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            root.gameObject.SetActive(true);
            yield return FadeTo(1f);
            yield return new WaitForSecondsRealtime(holdDuration);
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
            if (label != null) label.text = L10n.Get("save.recorded", "기억을 기록했습니다");
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
