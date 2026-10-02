using System;
using System.Collections;
using ShadowTheater.Field;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>
    /// 모바일 대화창. 탭하면 타자 효과를 즉시 완성하고, 다시 탭하면 다음 줄로 넘어간다.
    /// 대화 중 플레이어 이동을 잠그며 Time.timeScale과 무관하게 동작한다.
    /// </summary>
    public class DialogueController : MonoBehaviour
    {
        public static DialogueController Instance { get; private set; }

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text speakerText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Text continueText;
        [SerializeField] private Image accentBar;
        [SerializeField, Min(1f)] private float charactersPerSecond = 42f;

        public bool IsPlaying { get; private set; }

        private DialogueSequence _sequence;
        private int _lineIndex;
        private Coroutine _typingRoutine;
        private bool _isTyping;
        private bool _lockedPlayer;
        private string _fullLine;
        private Action _onComplete;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ReleasePlayerLock();
        }

        private void OnDisable()
        {
            if (IsPlaying) Cancel(false);
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER && (UNITY_EDITOR || UNITY_STANDALONE)
            if (IsPlaying && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Z) ||
                              Input.GetKeyDown(KeyCode.Return)))
                Advance();
#endif
        }

        public bool Play(string dialogueId, Color accent, Action onComplete = null)
        {
            if (IsPlaying) return false;
            if (!DialogueRepository.TryGet(dialogueId, out var sequence) ||
                sequence.lines == null || sequence.lines.Count == 0)
            {
                Debug.LogWarning($"[Dialogue] 비어 있거나 존재하지 않는 대화: {dialogueId}");
                onComplete?.Invoke();
                return false;
            }

            _sequence = sequence;
            _lineIndex = 0;
            _onComplete = onComplete;
            IsPlaying = true;

            if (accentBar != null) accentBar.color = accent;
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.MoveInput = Vector2.zero;
                PlayerController.Instance.Lock();
                _lockedPlayer = true;
            }

            SetVisible(true);
            ShowCurrentLine();
            return true;
        }

        /// <summary>UI의 전체 화면 투명 버튼 또는 '다음' 버튼에 연결.</summary>
        public void Advance()
        {
            if (!IsPlaying) return;
            if (_isTyping)
            {
                FinishTypingImmediately();
                return;
            }

            _lineIndex++;
            if (_lineIndex >= _sequence.lines.Count)
            {
                Finish();
                return;
            }
            ShowCurrentLine();
        }

        public void Cancel(bool invokeCompletion = false)
        {
            if (!IsPlaying) return;
            var callback = invokeCompletion ? _onComplete : null;
            Cleanup();
            callback?.Invoke();
        }

        private void ShowCurrentLine()
        {
            var line = _sequence.lines[_lineIndex];
            if (speakerText != null) speakerText.text = line.speaker ?? string.Empty;
            _fullLine = line.text ?? string.Empty;
            if (_typingRoutine != null) StopCoroutine(_typingRoutine);
            _typingRoutine = StartCoroutine(TypeLine());
        }

        private IEnumerator TypeLine()
        {
            _isTyping = true;
            if (bodyText != null) bodyText.text = string.Empty;
            if (continueText != null) continueText.gameObject.SetActive(false);

            float visibleCharacters = 0f;
            while (visibleCharacters < _fullLine.Length)
            {
                visibleCharacters += charactersPerSecond * Time.unscaledDeltaTime;
                int count = Mathf.Min(_fullLine.Length, Mathf.FloorToInt(visibleCharacters));
                if (bodyText != null) bodyText.text = _fullLine.Substring(0, count);
                yield return null;
            }

            if (bodyText != null) bodyText.text = _fullLine;
            _isTyping = false;
            _typingRoutine = null;
            if (continueText != null) continueText.gameObject.SetActive(true);
        }

        private void FinishTypingImmediately()
        {
            if (_typingRoutine != null) StopCoroutine(_typingRoutine);
            _typingRoutine = null;
            _isTyping = false;
            if (bodyText != null) bodyText.text = _fullLine;
            if (continueText != null) continueText.gameObject.SetActive(true);
        }

        private void Finish()
        {
            var callback = _onComplete;
            Cleanup();
            callback?.Invoke();
        }

        private void Cleanup()
        {
            if (_typingRoutine != null) StopCoroutine(_typingRoutine);
            _typingRoutine = null;
            _isTyping = false;
            IsPlaying = false;
            _sequence = null;
            _onComplete = null;
            SetVisible(false);
            ReleasePlayerLock();
        }

        private void ReleasePlayerLock()
        {
            if (!_lockedPlayer) return;
            if (PlayerController.Instance != null) PlayerController.Instance.Unlock();
            _lockedPlayer = false;
        }

        private void SetVisible(bool visible)
        {
            if (panelRoot != null) panelRoot.SetActive(visible);
            if (canvasGroup == null) return;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }
}
