using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    public enum ScriptBookFilter { All, Seen, Recorded }

    /// <summary>각본집 도감: 미조우/조우/기록 상태와 해금된 영웅 비극을 표시한다.</summary>
    public class ScriptBookController : MonoBehaviour
    {
        public static ScriptBookController Instance { get; private set; }

        [Header("목록")]
        [SerializeField] private GameObject root;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private ScriptBookEntryView entryTemplate;
        [SerializeField] private Text progressText;

        [Header("상세")]
        [SerializeField] private Image portrait;
        [SerializeField] private Image accentGlow;
        [SerializeField] private Text nameText;
        [SerializeField] private Text titleText;
        [SerializeField] private Text typeText;
        [SerializeField] private Text statsText;
        [SerializeField] private Text loreText;
        [SerializeField] private Text stageText;
        [SerializeField] private Text awakeningFeedbackText;
        [SerializeField] private Button restoreButton;
        [SerializeField] private Button salvationButton;
        [SerializeField] private Button grudgeButton;

        public bool IsOpen { get; private set; }
        private ScriptBookFilter _filter;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private bool _lockedPlayer;
        private ShadowData _selectedData;
        private ShadowInstance _selectedInstance;
        private bool _refreshPending;
        private ScrollRect _loreScroll;
        private int _lastActionFrame = -1;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (root != null) root.SetActive(false);
            if (entryTemplate != null) entryTemplate.gameObject.SetActive(false);
            EnsureLoreScroll();
        }

        private void OnEnable()
        {
            L10n.Changed += RefreshIfOpen;
            SaveManager.CurrentChanged += OnCurrentChanged;
            SaveManager.PartyChanged += RefreshIfOpen;
            QuestManager.GlobalQuestChanged += RefreshIfOpen;
            MemoryAwakeningService.StageChanged += OnStageChanged;
        }
        private void RefreshIfOpen() { if (IsOpen) _refreshPending = true; }
        private void OnStageChanged(ShadowInstance instance) => RefreshIfOpen();
        private void OnCurrentChanged() { if (IsOpen) Close(); _selectedData = null; _selectedInstance = null; }
        private void LateUpdate() { if (IsOpen && _refreshPending) RefreshList(); }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ReleasePlayer();
        }

        private void OnDisable()
        {
            L10n.Changed -= RefreshIfOpen;
            SaveManager.CurrentChanged -= OnCurrentChanged;
            SaveManager.PartyChanged -= RefreshIfOpen;
            QuestManager.GlobalQuestChanged -= RefreshIfOpen;
            MemoryAwakeningService.StageChanged -= OnStageChanged;
            if (IsOpen) Close();
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER && (UNITY_EDITOR || UNITY_STANDALONE)
            if (Input.GetKeyDown(KeyCode.Tab)) Toggle();
            if (IsOpen && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.X))) Close();
#endif
        }

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        public void Open()
        {
            if (IsOpen || SaveManager.Current == null) return;
            if (!FieldUiModalState.CanOpen(FieldUiModal.ScriptBook)) return;

            IsOpen = true;
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.MoveInput = Vector2.zero;
                PlayerController.Instance.Lock();
                _lockedPlayer = true;
            }
            FieldUiModalState.Show(root);
            TheatreUiStyle.ApplyBook(root);
            TheatreUiStyle.BookFilter(root, _filter);
            if (_selectedData == null && contentRoot is RectTransform content) content.anchoredPosition = Vector2.zero;
            RefreshList();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (root != null) root.SetActive(false);
            ReleasePlayer();
        }

        public void ShowAll() => SetFilter(ScriptBookFilter.All);
        public void ShowSeen() => SetFilter(ScriptBookFilter.Seen);
        public void ShowRecorded() => SetFilter(ScriptBookFilter.Recorded);
        private void SetFilter(ScriptBookFilter filter)
        {
            if (!IsOpen) return;
            _filter = filter;
            TheatreUiStyle.BookFilter(root, _filter);
            if (contentRoot is RectTransform content) content.anchoredPosition = Vector2.zero;
            RefreshList();
        }

        public void RestoreSelected()
        {
            if (!BeginGrowthAction()) return;
            bool success = MemoryAwakeningService.TryRestore(_selectedInstance, out string message);
            if (!success && MemoryAwakeningService.TryStartMemoryQuest(_selectedInstance, out string questMessage))
            {
                success = true;
                message = questMessage;
            }
            ShowAwakeningResult(message, success);
        }

        public void AwakenSalvation()
        {
            if (!BeginGrowthAction()) return;
            bool success = MemoryAwakeningService.TryAwaken(_selectedInstance, AwakeningPath.Salvation,
                out string message);
            ShowAwakeningResult(message, success);
        }

        public void AwakenGrudge()
        {
            if (!BeginGrowthAction()) return;
            bool success = MemoryAwakeningService.TryAwaken(_selectedInstance, AwakeningPath.Grudge,
                out string message);
            ShowAwakeningResult(message, success);
        }

        private void RefreshList()
        {
            _refreshPending = false;
            Vector2 position = UiListLayout.Position(contentRoot);
            string selectedId = _selectedData?.shadowId;
            UiListLayout.Clear(_spawned);

            var database = ShadowDatabase.Instance;
            if (SaveManager.Current == null) { Close(); return; }
            if (database == null) { ShowEmptyDetail(); return; }
            int recorded = 0, seen = 0, total = 0;
            var ids = new HashSet<string>();
            ShadowData first = null, firstRecorded = null, firstSeen = null, selected = null;
            for (int i = 0; i < database.shadows.Count; i++)
            {
                var data = database.shadows[i];
                if (data == null || string.IsNullOrEmpty(data.shadowId) || !ids.Add(data.shadowId)) continue;
                total++;
                var state = GetState(data.shadowId);
                if (state != ScriptBookEntryState.Unknown) seen++;
                if (state == ScriptBookEntryState.Recorded) recorded++;
                if (!PassesFilter(state)) continue;

                var view = Instantiate(entryTemplate, contentRoot);
                view.Bind(data, total - 1, state, Select);
                _spawned.Add(view.gameObject);
                if (first == null) first = data;
                if (firstRecorded == null && state == ScriptBookEntryState.Recorded) firstRecorded = data;
                if (firstSeen == null && state == ScriptBookEntryState.Seen) firstSeen = data;
                if (data.shadowId == selectedId) selected = data;
            }

            bool claimedReward = CollectionRewardService.TryClaim(recorded, total, out CollectionReward reward);
            if (claimedReward) SaveManager.Instance?.TrySave(false);
            if (progressText != null)
            {
                int nextMilestone = CollectionRewardService.NextMilestone(recorded, total);
                progressText.text = claimedReward
                    ? L10n.Format("book.collection_reward", "조우 {0} · 기록 {1}/{2}\n이정표 {3} · 금화 +{4} · 잉크 x{5}",
                        seen, recorded, total, reward.milestone, reward.gold, reward.inkCount)
                    : nextMilestone > 0
                        ? L10n.Format("book.collection_progress", "조우 {0} · 기록 {1}/{2}\n다음 수집 보상 {3}",
                            seen, recorded, total, nextMilestone)
                        : L10n.Format("book.collection_complete", "조우 {0} · 기록 {1}/{2}\n수집 이정표 완료",
                            seen, recorded, total);
            }
            if (first != null) Select(selected ?? firstRecorded ?? firstSeen ?? first);
            else ShowEmptyDetail();
            UiListLayout.Rebuild(contentRoot, position);
        }

        private void Select(ShadowData data)
        {
            if (data == null) return;
            bool changed = _selectedData?.shadowId != data.shadowId;
            Vector2 lorePosition = !changed && loreText != null ? UiListLayout.Position(loreText.transform) : Vector2.zero;
            _selectedData = data;
            foreach (var row in _spawned) row.GetComponent<ScriptBookEntryView>()?.SetSelected(data.shadowId);
            _selectedInstance = FindOwnedInstance(data.shadowId);
            var state = GetState(data.shadowId);
            bool seen = state != ScriptBookEntryState.Unknown;
            bool recorded = state == ScriptBookEntryState.Recorded;

            if (portrait != null)
            {
                portrait.sprite = seen ? (_selectedInstance?.Silhouette ?? data.silhouetteSprite) : null;
                portrait.enabled = portrait.sprite != null;
                portrait.color = ShadowPortraitStyle.Tint(portrait.sprite);
                portrait.preserveAspect = true;
            }
            if (accentGlow != null) accentGlow.color = recorded
                ? (_selectedInstance?.AccentColor ?? data.accentColor)
                : new Color(0.18f, 0.16f, 0.24f, 1f);
            if (nameText != null) nameText.text = seen
                ? L10n.Text(_selectedInstance?.DisplayName ?? data.displayName)
                : L10n.Get("book.unrecorded", "기록되지 않은 그림자");
            if (titleText != null) titleText.text = recorded ? L10n.Text(data.title) : "???";
            if (typeText != null) typeText.text = recorded
                ? $"{GrowthTierName(data.growthTier)} · {ElementName(data.element)} · {RoleName(data.role)}"
                : L10n.Get("book.unknown_type", "속성 미상");
            if (statsText != null) statsText.text = recorded
                ? (_selectedInstance != null
                    ? $"Lv.{_selectedInstance.level}  HP {_selectedInstance.MaxHp}  공격 {_selectedInstance.Atk}  방어 {_selectedInstance.Def}  속도 {_selectedInstance.Spd}"
                    : $"기본  HP {data.baseHp}  공격 {data.baseAtk}  방어 {data.baseDef}  속도 {data.baseSpd}")
                : "능력치가 아직 기록되지 않았습니다.";
            if (loreText != null) loreText.text = recorded
                ? BuildLore(data, _selectedInstance)
                : (seen ? data.loreLocked : "이 그림자와 아직 조우하지 않았습니다.");
            RefreshAwakeningActions(recorded);
            if (_loreScroll != null && loreText != null) UiListLayout.Rebuild(loreText.transform, lorePosition);
        }

        private static ScriptBookEntryState GetState(string shadowId)
        {
            if (SaveManager.IsRecorded(shadowId)) return ScriptBookEntryState.Recorded;
            return SaveManager.Current != null && SaveManager.Current.seenShadowIds.Contains(shadowId)
                ? ScriptBookEntryState.Seen
                : ScriptBookEntryState.Unknown;
        }

        private bool PassesFilter(ScriptBookEntryState state)
        {
            switch (_filter)
            {
                case ScriptBookFilter.Seen: return state != ScriptBookEntryState.Unknown;
                case ScriptBookFilter.Recorded: return state == ScriptBookEntryState.Recorded;
                default: return true;
            }
        }

        private void ShowEmptyDetail()
        {
            if (portrait != null) { portrait.sprite = null; portrait.enabled = false; }
            if (nameText != null) nameText.text = L10n.Get("book.empty", "표시할 기록이 없습니다");
            if (titleText != null) titleText.text = string.Empty;
            if (typeText != null) typeText.text = string.Empty;
            if (statsText != null) statsText.text = string.Empty;
            if (loreText != null) loreText.text = L10n.Get("book.empty_hint", "다른 필터를 선택하거나 새로운 그림자를 만나 보세요.");
            _selectedData = null;
            _selectedInstance = null;
            RefreshAwakeningActions(false);
            if (accentGlow != null) accentGlow.color = new Color(.18f, .16f, .24f, 1f);
            if (_loreScroll != null && loreText != null) UiListLayout.Rebuild(loreText.transform, Vector2.zero);
        }

        private void ReleasePlayer()
        {
            if (!_lockedPlayer) return;
            if (PlayerController.Instance != null) PlayerController.Instance.Unlock();
            _lockedPlayer = false;
        }

        private void ShowAwakeningResult(string message, bool success)
        {
            RefreshList();
            if (awakeningFeedbackText != null)
            {
                awakeningFeedbackText.text = message;
                awakeningFeedbackText.color = success
                    ? new Color(0.56f, 1f, 0.76f, 1f)
                    : new Color(1f, 0.66f, 0.72f, 1f);
            }
        }

        private void RefreshAwakeningActions(bool recorded)
        {
            if (stageText != null) stageText.text = recorded
                ? $"현재 형태  {MemoryAwakeningService.StageLabel(_selectedInstance)}"
                : string.Empty;

            string restoreReason = string.Empty;
            string salvationReason = string.Empty;
            string grudgeReason = string.Empty;
            bool canRestore = recorded && MemoryAwakeningService.CanRestore(_selectedInstance, out restoreReason);
            bool canStartMemoryQuest = recorded && MemoryAwakeningService.CanStartMemoryQuest(_selectedInstance, out _);
            bool memoryQuestActive = recorded && MemoryAwakeningService.IsMemoryQuestActive(_selectedInstance, out _);
            bool canSalvation = recorded && MemoryAwakeningService.CanAwaken(_selectedInstance,
                AwakeningPath.Salvation, out salvationReason);
            bool canGrudge = recorded && MemoryAwakeningService.CanAwaken(_selectedInstance,
                AwakeningPath.Grudge, out grudgeReason);
            if (restoreButton != null)
            {
                restoreButton.interactable = canRestore || canStartMemoryQuest;
                var label = restoreButton.GetComponentInChildren<Text>(true);
                if (label != null) label.text = canStartMemoryQuest ? "기억 여정 시작" : "기억 복원";
            }
            if (salvationButton != null) salvationButton.interactable = canSalvation;
            if (grudgeButton != null) grudgeButton.interactable = canGrudge;

            if (awakeningFeedbackText == null) return;
            if (!recorded) awakeningFeedbackText.text = string.Empty;
            else if (_selectedInstance == null) awakeningFeedbackText.text = "보유 중인 개체가 없습니다.";
            else if (_selectedInstance.memoryStage == MemoryStage.Echo)
                awakeningFeedbackText.text = canRestore ? "기억 복원 조건을 충족했습니다."
                    : memoryQuestActive ? "개인 기억 여정이 진행 중입니다. 기억 여정에서 목표를 확인하세요."
                    : canStartMemoryQuest ? "이 그림자의 개인 기억 여정을 시작할 수 있습니다." : restoreReason;
            else if (_selectedInstance.memoryStage == MemoryStage.Restored)
                awakeningFeedbackText.text = canSalvation || canGrudge
                    ? "진명 각성 방향을 선택할 수 있습니다."
                    : $"구원: {salvationReason}  /  원한: {grudgeReason}";
            else awakeningFeedbackText.text = "진명을 되찾은 최종 형태입니다.";
            awakeningFeedbackText.color = new Color(0.72f, 0.68f, 0.82f, 1f);
        }

        private static ShadowInstance FindOwnedInstance(string shadowId)
        {
            ShadowInstance best = null;
            void Consider(ShadowInstance candidate)
            {
                if (candidate == null || candidate.shadowId != shadowId) return;
                if (best == null || candidate.memoryStage > best.memoryStage ||
                    (candidate.memoryStage == best.memoryStage && candidate.level > best.level)) best = candidate;
            }
            if (SaveManager.Current == null) return null;
            foreach (var instance in SaveManager.Current.party) Consider(instance);
            foreach (var instance in SaveManager.Current.storage) Consider(instance);
            return best;
        }

        private static string BuildLore(ShadowData data, ShadowInstance instance)
        {
            string text = L10n.Text(data.loreUnlocked);
            if (instance?.ActiveForm != null && !string.IsNullOrWhiteSpace(instance.ActiveForm.loreAppend))
                text += "\n\n" + L10n.Text(instance.ActiveForm.loreAppend);
            if (instance == null) return text;
            var learned = instance.GetAvailableSkills();
            if (data.basicAttack != null && !learned.Contains(data.basicAttack)) learned.Insert(0, data.basicAttack);
            text += "\n\n" + L10n.Format("book.learned_skills", "현재 기술 · {0}",
                learned.Count > 0 ? string.Join(" · ", learned.Select(x => L10n.Text(x.displayName)))
                    : L10n.Get("book.no_skills", "없음"));
            var future = instance.GetAvailableSkills(ShadowInstance.MaxLevel)
                .Where(x => Mathf.Max(1, x.requiredLevel) > instance.level).OrderBy(x => x.requiredLevel).ToList();
            text += "\n" + (future.Count > 0
                ? L10n.Format("book.next_skill", "다음 기술 · Lv.{0} {1}", future[0].requiredLevel,
                    string.Join(" · ", future.Where(x => x.requiredLevel == future[0].requiredLevel).Select(x => L10n.Text(x.displayName))))
                : L10n.Get("book.skills_complete", "현재 형태의 레벨 기술을 모두 익혔습니다."));
            return text;
        }

        private bool BeginGrowthAction()
        {
            if (!IsOpen || !FieldUiModalState.CanOpen(FieldUiModal.ScriptBook) || _lastActionFrame == Time.frameCount) return false;
            _lastActionFrame = Time.frameCount;
            return true;
        }

        private void EnsureLoreScroll()
        {
            if (loreText == null) return;
            var textRect = loreText.rectTransform;
            var existing = textRect.parent != null ? textRect.parent.GetComponent<ScrollRect>() : null;
            if (existing != null && existing.content == textRect)
            {
                _loreScroll = existing;
                return;
            }
            var viewportGo = new GameObject("LoreViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            var viewport = (RectTransform)viewportGo.transform;
            viewport.SetParent(textRect.parent, false);
            viewport.SetSiblingIndex(textRect.GetSiblingIndex());
            viewport.anchorMin = textRect.anchorMin; viewport.anchorMax = textRect.anchorMax;
            viewport.pivot = textRect.pivot;
            viewport.offsetMin = textRect.offsetMin; viewport.offsetMax = textRect.offsetMax;
            viewportGo.GetComponent<Image>().color = Color.clear;
            _loreScroll = viewportGo.GetComponent<ScrollRect>();
            _loreScroll.horizontal = false;
            _loreScroll.movementType = ScrollRect.MovementType.Clamped;
            _loreScroll.scrollSensitivity = 30f;
            _loreScroll.viewport = viewport;
            textRect.SetParent(viewport, false);
            textRect.anchorMin = new Vector2(0f, 1f); textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(.5f, 1f);
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            loreText.horizontalOverflow = HorizontalWrapMode.Wrap;
            loreText.verticalOverflow = VerticalWrapMode.Overflow;
            loreText.raycastTarget = false;
            var fitter = loreText.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = loreText.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _loreScroll.content = textRect;
        }

        private static string ElementName(ShadowElement element) =>
            L10n.Get("element." + element.ToString().ToLowerInvariant(), ElementChart.Name(element));

        private static string RoleName(ShadowRole role)
        {
            switch (role)
            {
                case ShadowRole.PhysicalDealer: return L10n.Get("role.physical", "물리 공격");
                case ShadowRole.MagicNuker: return L10n.Get("role.magic", "마법 공격");
                case ShadowRole.SpeedUtility: return L10n.Get("role.speed", "속도·유틸");
                case ShadowRole.Tank: return L10n.Get("role.tank", "수호");
                default: return L10n.Get("role.support", "지원");
            }
        }

        private static string GrowthTierName(GrowthTier tier)
        {
            switch (tier)
            {
                case GrowthTier.Rare: return L10n.Get("tier.rare", "희귀");
                case GrowthTier.RegionalBoss: return L10n.Get("tier.boss", "지역 보스");
                case GrowthTier.Legendary: return L10n.Get("tier.legendary", "전설");
                default: return L10n.Get("tier.standard", "일반");
            }
        }
    }
}
