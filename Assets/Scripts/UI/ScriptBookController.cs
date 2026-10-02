using System.Collections.Generic;
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

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (root != null) root.SetActive(false);
            if (entryTemplate != null) entryTemplate.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ReleasePlayer();
        }

        private void OnDisable()
        {
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
            if (GameFlowController.Instance != null && GameFlowController.Instance.IsInBattle) return;
            if (DialogueController.Instance != null && DialogueController.Instance.IsPlaying) return;

            IsOpen = true;
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.MoveInput = Vector2.zero;
                PlayerController.Instance.Lock();
                _lockedPlayer = true;
            }
            if (root != null) root.SetActive(true);
            RefreshList();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (root != null) root.SetActive(false);
            ReleasePlayer();
        }

        public void ShowAll() { _filter = ScriptBookFilter.All; RefreshList(); }
        public void ShowSeen() { _filter = ScriptBookFilter.Seen; RefreshList(); }
        public void ShowRecorded() { _filter = ScriptBookFilter.Recorded; RefreshList(); }

        public void RestoreSelected()
        {
            bool success = MemoryAwakeningService.TryRestore(_selectedInstance, out string message);
            ShowAwakeningResult(message, success);
        }

        public void AwakenSalvation()
        {
            bool success = MemoryAwakeningService.TryAwaken(_selectedInstance, AwakeningPath.Salvation,
                out string message);
            ShowAwakeningResult(message, success);
        }

        public void AwakenGrudge()
        {
            bool success = MemoryAwakeningService.TryAwaken(_selectedInstance, AwakeningPath.Grudge,
                out string message);
            ShowAwakeningResult(message, success);
        }

        private void RefreshList()
        {
            foreach (var item in _spawned) Destroy(item);
            _spawned.Clear();

            var database = ShadowDatabase.Instance;
            if (database == null || SaveManager.Current == null) return;
            int recorded = 0;
            for (int i = 0; i < database.shadows.Count; i++)
            {
                var data = database.shadows[i];
                if (data == null) continue;
                var state = GetState(data.shadowId);
                if (state == ScriptBookEntryState.Recorded) recorded++;
                if (!PassesFilter(state)) continue;

                var view = Instantiate(entryTemplate, contentRoot);
                view.Bind(data, i, state, Select);
                _spawned.Add(view.gameObject);
            }

            if (progressText != null) progressText.text = $"기록 {recorded} / {database.shadows.Count}";
            if (_spawned.Count > 0)
            {
                // 목록의 첫 데이터를 다시 찾는 대신 필터 순서대로 선택한다.
                foreach (var data in database.shadows)
                    if (data != null && PassesFilter(GetState(data.shadowId))) { Select(data); break; }
            }
            else ShowEmptyDetail();
        }

        private void Select(ShadowData data)
        {
            if (data == null) return;
            _selectedData = data;
            _selectedInstance = FindOwnedInstance(data.shadowId);
            var state = GetState(data.shadowId);
            bool seen = state != ScriptBookEntryState.Unknown;
            bool recorded = state == ScriptBookEntryState.Recorded;

            if (portrait != null)
            {
                portrait.sprite = seen ? (_selectedInstance?.Silhouette ?? data.silhouetteSprite) : null;
                portrait.color = Color.black;
                portrait.preserveAspect = true;
            }
            if (accentGlow != null) accentGlow.color = recorded
                ? (_selectedInstance?.AccentColor ?? data.accentColor)
                : new Color(0.18f, 0.16f, 0.24f, 1f);
            if (nameText != null) nameText.text = seen
                ? (_selectedInstance?.DisplayName ?? data.displayName)
                : "기록되지 않은 그림자";
            if (titleText != null) titleText.text = recorded ? data.title : "???";
            if (typeText != null) typeText.text = recorded
                ? $"{ElementName(data.element)} · {RoleName(data.role)}"
                : "속성 미상";
            if (statsText != null) statsText.text = recorded
                ? (_selectedInstance != null
                    ? $"Lv.{_selectedInstance.level}  HP {_selectedInstance.MaxHp}  공격 {_selectedInstance.Atk}  방어 {_selectedInstance.Def}  속도 {_selectedInstance.Spd}"
                    : $"기본  HP {data.baseHp}  공격 {data.baseAtk}  방어 {data.baseDef}  속도 {data.baseSpd}")
                : "능력치가 아직 기록되지 않았습니다.";
            if (loreText != null) loreText.text = recorded
                ? BuildLore(data, _selectedInstance)
                : (seen ? data.loreLocked : "이 그림자와 아직 조우하지 않았습니다.");
            RefreshAwakeningActions(recorded);
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
            if (portrait != null) portrait.sprite = null;
            if (nameText != null) nameText.text = "표시할 기록이 없습니다";
            if (titleText != null) titleText.text = string.Empty;
            if (typeText != null) typeText.text = string.Empty;
            if (statsText != null) statsText.text = string.Empty;
            if (loreText != null) loreText.text = "다른 필터를 선택하거나 새로운 그림자를 만나 보세요.";
            _selectedData = null;
            _selectedInstance = null;
            RefreshAwakeningActions(false);
        }

        private void ReleasePlayer()
        {
            if (!_lockedPlayer) return;
            if (PlayerController.Instance != null) PlayerController.Instance.Unlock();
            _lockedPlayer = false;
        }

        private void ShowAwakeningResult(string message, bool success)
        {
            if (_selectedData != null) Select(_selectedData);
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
            bool canSalvation = recorded && MemoryAwakeningService.CanAwaken(_selectedInstance,
                AwakeningPath.Salvation, out salvationReason);
            bool canGrudge = recorded && MemoryAwakeningService.CanAwaken(_selectedInstance,
                AwakeningPath.Grudge, out grudgeReason);
            if (restoreButton != null) restoreButton.interactable = canRestore;
            if (salvationButton != null) salvationButton.interactable = canSalvation;
            if (grudgeButton != null) grudgeButton.interactable = canGrudge;

            if (awakeningFeedbackText == null) return;
            if (!recorded) awakeningFeedbackText.text = string.Empty;
            else if (_selectedInstance == null) awakeningFeedbackText.text = "보유 중인 개체가 없습니다.";
            else if (_selectedInstance.memoryStage == MemoryStage.Echo)
                awakeningFeedbackText.text = canRestore ? "기억 복원 조건을 충족했습니다." : restoreReason;
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
            if (instance?.ActiveForm == null || string.IsNullOrWhiteSpace(instance.ActiveForm.loreAppend))
                return data.loreUnlocked;
            return data.loreUnlocked + "\n\n" + instance.ActiveForm.loreAppend;
        }

        private static string ElementName(ShadowElement element)
        {
            switch (element)
            {
                case ShadowElement.Flame: return "붉은 불꽃";
                case ShadowElement.Frost: return "푸른 서리";
                case ShadowElement.Shade: return "자줏빛 그림자";
                default: return "무속성";
            }
        }

        private static string RoleName(ShadowRole role)
        {
            switch (role)
            {
                case ShadowRole.PhysicalDealer: return "물리 공격";
                case ShadowRole.MagicNuker: return "마법 공격";
                case ShadowRole.SpeedUtility: return "속도·유틸";
                case ShadowRole.Tank: return "수호";
                default: return "지원";
            }
        }
    }
}
