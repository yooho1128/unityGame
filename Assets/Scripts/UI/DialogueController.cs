using System;
using System.Collections;
using System.Collections.Generic;
using ShadowTheater.Field;
using ShadowTheater.Save;
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
        [SerializeField] private RectTransform dialogueBox;
        [SerializeField] private Image dialogueBoxImage;
        [SerializeField] private GameObject choiceRoot;
        [SerializeField] private List<DialogueChoiceView> choiceViews = new List<DialogueChoiceView>();
        [SerializeField, Min(1f)] private float charactersPerSecond = 42f;

        public bool IsPlaying { get; private set; }

        private DialogueSequence _sequence;
        private int _lineIndex;
        private Coroutine _typingRoutine;
        private bool _isTyping;
        private bool _lockedPlayer;
        private string _fullLine;
        private Action _onComplete;
        private readonly List<DialogueChoice> _visibleChoices = new List<DialogueChoice>();
        private bool _awaitingChoice;
        private Coroutine _emotionRoutine;
        private Vector2 _dialogueBoxHome;
        private Color _baseAccent = new Color(.71f, .38f, 1f, 1f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (dialogueBox != null) _dialogueBoxHome = dialogueBox.anchoredPosition;
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
            if (_awaitingChoice && Input.GetKeyDown(KeyCode.Alpha1)) SelectChoice(0);
            if (_awaitingChoice && Input.GetKeyDown(KeyCode.Alpha2)) SelectChoice(1);
            if (_awaitingChoice && Input.GetKeyDown(KeyCode.Alpha3)) SelectChoice(2);
#endif
        }

        public bool Play(string dialogueId, Color accent, Action onComplete = null)
        {
            if (IsPlaying) return false;
            if (!FieldUiModalState.CanOpen(FieldUiModal.Dialogue)) return false;
            if (!DialogueRepository.TryGet(dialogueId, out var sequence) || !HasContent(sequence))
            {
                Debug.LogWarning($"[Dialogue] 비어 있거나 존재하지 않는 대화: {dialogueId}");
                onComplete?.Invoke();
                return false;
            }

            _sequence = sequence;
            _lineIndex = 0;
            _onComplete = onComplete;
            IsPlaying = true;

            _baseAccent = accent;
            if (accentBar != null) accentBar.color = accent;
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.MoveInput = Vector2.zero;
                PlayerController.Instance.Lock();
                _lockedPlayer = true;
            }

            SetVisible(true);
            HideChoices();
            ShowNextAvailableLine();
            return true;
        }

        /// <summary>UI의 전체 화면 투명 버튼 또는 '다음' 버튼에 연결.</summary>
        public void Advance()
        {
            if (!IsPlaying) return;
            if (_awaitingChoice) return;
            if (_isTyping)
            {
                FinishTypingImmediately();
                return;
            }

            _lineIndex++;
            ShowNextAvailableLine();
        }

        public void SelectChoice(int visibleIndex)
        {
            if (!_awaitingChoice || visibleIndex < 0 || visibleIndex >= _visibleChoices.Count) return;
            var choice = _visibleChoices[visibleIndex];

            if (!string.IsNullOrEmpty(choice.setFlag))
                SaveManager.SetFlag(choice.setFlag, choice.setFlagValue);
            if (choice.flagChanges != null)
            {
                foreach (var change in choice.flagChanges)
                    if (change != null && !string.IsNullOrEmpty(change.flag) && change.delta != 0)
                        SaveManager.SetFlag(change.flag, SaveManager.GetFlag(change.flag) + change.delta);
            }
            if (!string.IsNullOrEmpty(choice.startQuestId))
                QuestManager.Instance?.TryStartQuest(choice.startQuestId, false);

            HideChoices();
            if (!string.IsNullOrEmpty(choice.nextDialogueId) &&
                DialogueRepository.TryGet(choice.nextDialogueId, out var next) && HasContent(next))
            {
                _sequence = next;
                _lineIndex = 0;
                ShowNextAvailableLine();
                return;
            }
            Finish();
        }

        public void Cancel(bool invokeCompletion = false)
        {
            if (!IsPlaying) return;
            var callback = invokeCompletion ? _onComplete : null;
            Cleanup();
            callback?.Invoke();
        }

        private void ShowNextAvailableLine()
        {
            var lines = _sequence.lines;
            while (lines != null && _lineIndex < lines.Count && !ConditionsPass(
                       lines[_lineIndex].requiredFlag, lines[_lineIndex].requiredFlagValue,
                       lines[_lineIndex].blockedFlag))
                _lineIndex++;

            if (lines == null || _lineIndex >= lines.Count)
            {
                ShowChoicesOrFinish();
                return;
            }
            ShowCurrentLine();
        }

        private void ShowCurrentLine()
        {
            var line = _sequence.lines[_lineIndex];
            if (!string.IsNullOrEmpty(line.setFlag)) SaveManager.SetFlag(line.setFlag, line.setFlagValue);
            ApplyEmotion(line.emotion);
            if (speakerText != null) speakerText.text = L10n.Get(
                $"dialogue.{_sequence.dialogueId}.{_lineIndex}.speaker", L10n.Text(line.speaker));
            _fullLine = L10n.Get($"dialogue.{_sequence.dialogueId}.{_lineIndex}.text", L10n.Text(line.text));
            if (_typingRoutine != null) StopCoroutine(_typingRoutine);
            _typingRoutine = StartCoroutine(TypeLine());
        }

        private void ShowChoicesOrFinish()
        {
            _visibleChoices.Clear();
            if (_sequence.choices != null)
            {
                foreach (var choice in _sequence.choices)
                    if (choice != null && ConditionsPass(choice.requiredFlag, choice.requiredFlagValue,
                            choice.blockedFlag))
                        _visibleChoices.Add(choice);
            }

            if (_visibleChoices.Count == 0)
            {
                Finish();
                return;
            }

            _awaitingChoice = true;
            if (choiceRoot != null) choiceRoot.SetActive(true);
            if (continueText != null) continueText.gameObject.SetActive(false);
            for (int i = 0; i < choiceViews.Count; i++)
            {
                if (i < _visibleChoices.Count)
                {
                    var choice = _visibleChoices[i];
                    int sourceIndex = _sequence.choices.IndexOf(choice);
                    choiceViews[i].Bind(i, L10n.Get(
                        $"dialogue.{_sequence.dialogueId}.choice.{sourceIndex}", L10n.Text(choice.text)), SelectChoice);
                }
                else choiceViews[i].Clear();
            }
            if (_visibleChoices.Count > choiceViews.Count)
                Debug.LogWarning($"[Dialogue] 선택지 UI가 부족합니다: {_sequence.dialogueId} " +
                                 $"({_visibleChoices.Count}/{choiceViews.Count})");
        }

        private IEnumerator TypeLine()
        {
            _isTyping = true;
            if (bodyText != null) bodyText.text = string.Empty;
            if (continueText != null) continueText.gameObject.SetActive(false);

            float visibleCharacters = 0f;
            while (visibleCharacters < _fullLine.Length)
            {
                float speed = GameSettings.Instance != null ? GameSettings.TextSpeed : charactersPerSecond;
                visibleCharacters += speed * Time.unscaledDeltaTime;
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
            StopEmotionMotion();
            HideChoices();
            SetVisible(false);
            ReleasePlayerLock();
        }

        private void HideChoices()
        {
            _awaitingChoice = false;
            _visibleChoices.Clear();
            foreach (var view in choiceViews)
                if (view != null) view.Clear();
            if (choiceRoot != null) choiceRoot.SetActive(false);
        }

        private static bool ConditionsPass(string requiredFlag, int requiredValue, string blockedFlag)
        {
            if (!string.IsNullOrEmpty(requiredFlag) &&
                SaveManager.GetFlag(requiredFlag) < Mathf.Max(1, requiredValue)) return false;
            return string.IsNullOrEmpty(blockedFlag) || !SaveManager.HasFlag(blockedFlag);
        }

        private static bool HasContent(DialogueSequence sequence) =>
            sequence != null && ((sequence.lines != null && sequence.lines.Count > 0) ||
                                 (sequence.choices != null && sequence.choices.Count > 0));

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

        private void ApplyEmotion(string emotion)
        {
            string mood = (emotion ?? string.Empty).Trim().ToLowerInvariant();
            Color color;
            float shake = 0f;
            bool pulse = false;
            switch (mood)
            {
                case "rage": case "warning": case "bitter":
                    color = new Color(.98f, .22f, .28f, 1f); shake = 9f; break;
                case "fear": case "cold": case "mystery": case "guilt":
                    color = new Color(.34f, .62f, 1f, 1f); shake = 3f; break;
                case "hope": case "joy": case "relief": case "ending":
                    color = new Color(1f, .79f, .32f, 1f); pulse = true; break;
                case "sad": case "solemn":
                    color = new Color(.55f, .58f, .86f, 1f); break;
                case "resolve": case "serious":
                    color = new Color(.78f, .42f, 1f, 1f); pulse = mood == "resolve"; break;
                case "curious": case "question": case "surprised":
                    color = new Color(.34f, .90f, .86f, 1f); pulse = mood == "surprised"; break;
                default:
                    color = _baseAccent; break;
            }

            Color accent = Color.Lerp(_baseAccent, color, .72f);
            if (accentBar != null) accentBar.color = accent;
            if (speakerText != null) speakerText.color = Color.Lerp(Color.white, color, .42f);
            if (bodyText != null) bodyText.color = Color.Lerp(Color.white, color, .08f);
            if (dialogueBoxImage != null)
                dialogueBoxImage.color = Color.Lerp(new Color(.035f, .026f, .075f, .96f), color, .08f);

            StopEmotionMotion();
            if (dialogueBox != null && (shake > 0f || pulse))
                _emotionRoutine = StartCoroutine(EmotionMotion(shake, pulse));
        }

        private IEnumerator EmotionMotion(float shake, bool pulse)
        {
            const float duration = .24f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float fade = 1f - Mathf.Clamp01(elapsed / duration);
                if (shake > 0f)
                {
                    float x = Mathf.Sin(elapsed * 115f) * shake * fade;
                    dialogueBox.anchoredPosition = _dialogueBoxHome + new Vector2(x, 0f);
                }
                if (pulse)
                {
                    float scale = 1f + Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI) * .018f;
                    dialogueBox.localScale = new Vector3(scale, scale, 1f);
                }
                yield return null;
            }
            ResetDialogueBoxTransform();
            _emotionRoutine = null;
        }

        private void StopEmotionMotion()
        {
            if (_emotionRoutine != null) StopCoroutine(_emotionRoutine);
            _emotionRoutine = null;
            ResetDialogueBoxTransform();
        }

        private void ResetDialogueBoxTransform()
        {
            if (dialogueBox == null) return;
            dialogueBox.anchoredPosition = _dialogueBoxHome;
            dialogueBox.localScale = Vector3.one;
        }
    }
}
