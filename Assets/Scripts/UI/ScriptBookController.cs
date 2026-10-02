using System.Collections.Generic;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
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

        public bool IsOpen { get; private set; }
        private ScriptBookFilter _filter;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private bool _lockedPlayer;

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
            var state = GetState(data.shadowId);
            bool seen = state != ScriptBookEntryState.Unknown;
            bool recorded = state == ScriptBookEntryState.Recorded;

            if (portrait != null)
            {
                portrait.sprite = seen ? data.silhouetteSprite : null;
                portrait.color = Color.black;
                portrait.preserveAspect = true;
            }
            if (accentGlow != null) accentGlow.color = recorded
                ? data.accentColor
                : new Color(0.18f, 0.16f, 0.24f, 1f);
            if (nameText != null) nameText.text = seen ? data.displayName : "기록되지 않은 그림자";
            if (titleText != null) titleText.text = recorded ? data.title : "???";
            if (typeText != null) typeText.text = recorded
                ? $"{ElementName(data.element)} · {RoleName(data.role)}"
                : "속성 미상";
            if (statsText != null) statsText.text = recorded
                ? $"HP {data.baseHp}   공격 {data.baseAtk}   방어 {data.baseDef}   속도 {data.baseSpd}"
                : "능력치가 아직 기록되지 않았습니다.";
            if (loreText != null) loreText.text = recorded
                ? data.loreUnlocked
                : (seen ? data.loreLocked : "이 그림자와 아직 조우하지 않았습니다.");
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
        }

        private void ReleasePlayer()
        {
            if (!_lockedPlayer) return;
            if (PlayerController.Instance != null) PlayerController.Instance.Unlock();
            _lockedPlayer = false;
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
