using System.Collections;
using System.Collections.Generic;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using ShadowTheater.Save;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>
    /// 《거상》식 필드 심볼. 필드에 보이는 그림자가 배회하다 플레이어가 가까우면 추적,
    /// 닿으면(OnTriggerEnter2D) GameFlowController에 전투를 요청한다.
    ///
    /// - encounterId 비움  → 야생 심볼: 처치/포획 후 respawnSeconds 뒤 다시 등장
    /// - encounterId 지정  → 고정 심볼(보스/라이벌/검열단): 한 번 이기면 세이브에 기록되어 영구 제거
    ///
    /// 필요 컴포넌트: Collider2D(isTrigger), SpriteRenderer
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class EncounterSymbol : MonoBehaviour
    {
        [Header("전투 구성")]
        [SerializeField] private BattleMode mode = BattleMode.Wild;
        [SerializeField] private ShadowData leadShadow;
        [SerializeField] private List<ShadowData> extraShadows = new List<ShadowData>(); // 라이벌 등 다수 출전
        [SerializeField] private Vector2Int levelRange = new Vector2Int(3, 5);
        [Tooltip("고정 심볼만 입력. 비워두면 리스폰되는 야생 심볼")]
        [SerializeField] private string encounterId;
        [Tooltip("숨은 보스 등 특수 고정 조우의 전용 전투 규칙")]
        [SerializeField] private BattleGimmick battleGimmick;

        [Header("배회 / 추적")]
        [SerializeField] private bool wander = true;
        [SerializeField] private int wanderRadius = 3;
        [SerializeField] private Vector2 wanderInterval = new Vector2(1.2f, 2.5f);
        [SerializeField] private int chaseRange = 4;       // 0이면 추적 안 함
        [SerializeField] private float stepDuration = 0.28f;

        [Header("리스폰 / 도주 후")]
        [SerializeField] private float respawnSeconds = 30f;
        [SerializeField] private float escapeCooldown = 3f;

        [Header("연출")]
        [SerializeField] private SpriteRenderer silhouette;

        public BattleMode Mode => mode;
        public string EncounterId => encounterId;
        public BattleGimmick Gimmick => battleGimmick;
        public bool IsFixed => !string.IsNullOrEmpty(encounterId);
        public bool IsActive { get; private set; } = true;

        private Collider2D _trigger;
        private Vector2Int _home;
        private Vector2Int _cell;
        private bool _started;
        private bool _frozen;
        private float _cooldownUntil;
        private float _respawnAt = -1f;
        private Coroutine _ai;
        private Vector2Int _lastWanderDirection;

        // ────────────────────────────────────────────

        private void Awake()
        {
            _trigger = GetComponent<Collider2D>();
            _trigger.isTrigger = true;
            if (silhouette == null) silhouette = GetComponentInChildren<SpriteRenderer>();
        }

        private void Start()
        {
            if (IsFixed && SaveManager.IsEncounterCleared(encounterId))
            {
                gameObject.SetActive(false);
                return;
            }

            _home = _cell = FieldGrid.Current.WorldToCell(transform.position);
            transform.position = FieldGrid.Current.CellToWorld(_cell);
            if (silhouette != null && silhouette.sprite == null && leadShadow != null && leadShadow.silhouetteSprite != null)
                silhouette.sprite = leadShadow.silhouetteSprite;
            ShadowArtRoster.ApplyFieldVisual(silhouette, leadShadow);

            _ai = StartCoroutine(AIRoutine());
            _started = true;
        }

        // 전투 중 fieldRoot가 꺼지면 코루틴이 멈추므로, 다시 켜질 때 위치 정리 후 AI 재시작
        private void OnEnable()
        {
            if (!_started) return;
            if (_ai == null) _ai = StartCoroutine(ResumeAfterFieldEnable());
        }

        private void OnDisable() { StopAllCoroutines(); _ai = null; }

        private IEnumerator ResumeAfterFieldEnable()
        {
            // Re-enabling FieldRoot invokes sibling OnEnable callbacks in an unspecified
            // order. Wait until FieldGrid has restored its static reference before using it.
            while (FieldGrid.Current == null) yield return null;
            transform.position = FieldGrid.Current.CellToWorld(_cell);
            if (silhouette != null) silhouette.enabled = IsActive;
            _ai = StartCoroutine(AIRoutine());
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryEncounter(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            // 도주 쿨다운 끝났을 때 겹쳐 있으면 바로 재전투
            TryEncounter(other);
        }

        private void TryEncounter(Collider2D other)
        {
            if (!IsActive || _frozen || Time.time < _cooldownUntil) return;
            if (!other.CompareTag("Player")) return;
            if (GameFlowController.Instance == null || !GameFlowController.Instance.CanStartEncounter) return;

            GameFlowController.Instance.RequestSymbolBattle(this);
        }

        // ────────────────────────────────────────────
        #region GameFlow 연동

        /// <summary>전투에 내보낼 적 파티 생성 (매번 새 개체)</summary>
        public List<ShadowInstance> BuildEnemyParty()
        {
            var list = new List<ShadowInstance>();
            int lv = RollEnemyLevel();
            if (leadShadow != null) list.Add(new ShadowInstance(leadShadow, lv));
            foreach (var s in extraShadows)
                if (s != null) list.Add(new ShadowInstance(s, RollEnemyLevel()));
            return list;
        }

        private int RollEnemyLevel()
        {
            var region = ShadowTheater.Story.RegionRepository.GetByScene(gameObject.scene.name);
            // Fixed regional curve: training and later acts retain their authored levels.
            int relief = region != null && region.act == 1 ? 2 : region != null && region.act == 2 ? 1 : 0;
            int minimum = Mathf.Max(1, levelRange.x - relief);
            int maximum = Mathf.Max(minimum, levelRange.y - relief);
            return Random.Range(minimum, maximum + 1);
        }

        public void FaceToward(Vector3 world)
        {
            if(silhouette!=null&&Mathf.Abs(world.x-transform.position.x)>.03f)
                silhouette.flipX=world.x<transform.position.x;
        }

        public void Freeze(bool on) => _frozen = on;

        public void OnBattleFinished(BattleResult result)
        {
            _frozen = false;
            switch (result)
            {
                case BattleResult.Victory:
                case BattleResult.Captured:
                    if (IsFixed) { gameObject.SetActive(false); return; }
                    _respawnAt = Time.time + Mathf.Max(0f, respawnSeconds);
                    SetVisible(false);
                    break;

                case BattleResult.Escaped:
                case BattleResult.Defeat:
                    _cooldownUntil = Time.time + escapeCooldown;
                    StartCoroutine(BlinkRoutine(escapeCooldown));
                    break;
            }
        }

        #endregion

        // ────────────────────────────────────────────
        #region AI (배회 / 추적)

        private IEnumerator AIRoutine()
        {
            while (true)
            {
                if (!IsActive && _respawnAt >= 0f && Time.time >= _respawnAt)
                {
                    _respawnAt = -1f;
                    _cell = _home;
                    transform.position = FieldGrid.Current.CellToWorld(_home);
                    SetVisible(true);
                }
                if (_frozen || !IsActive || !wander)
                {
                    yield return null;
                    continue;
                }

                var player = PlayerController.Instance;
                bool chasing = chaseRange > 0 && player != null && !player.IsLocked
                               && Manhattan(player.Cell, _cell) <= chaseRange
                               && Time.time >= _cooldownUntil;

                Vector2Int dir = chasing ? DirTowards(player.Cell) : RandomWanderDir();
                if (dir != Vector2Int.zero) yield return Step(dir);

                float wait = chasing ? Random.Range(.06f, .13f) : Random.Range(wanderInterval.x, wanderInterval.y);
                if (!chasing && dir == Vector2Int.zero) wait *= Random.Range(1.25f, 1.8f);
                yield return new WaitForSeconds(wait);
            }
        }

        private Vector2Int RandomWanderDir()
        {
            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            // 계속 왕복하지 않고 때때로 멈춰 주변을 살피며, 직전 방향을 어느 정도 유지한다.
            if (Random.value < .22f) return Vector2Int.zero;
            if (_lastWanderDirection != Vector2Int.zero && Random.value < .52f &&
                CanWander(_lastWanderDirection)) return _lastWanderDirection;
            int start = Random.Range(0, dirs.Length);
            for (int i = 0; i < dirs.Length; i++)
            {
                Vector2Int candidate = dirs[(start + i) % dirs.Length];
                if (candidate == -_lastWanderDirection && i < dirs.Length - 1) continue;
                if (!CanWander(candidate)) continue;
                _lastWanderDirection = candidate;
                return candidate;
            }
            return Vector2Int.zero;
        }

        private bool CanWander(Vector2Int direction)
        {
            Vector2Int cell = _cell + direction;
            return Manhattan(cell, _home) <= wanderRadius && !FieldGrid.Current.IsBlocked(cell, _trigger, false);
        }

        private Vector2Int DirTowards(Vector2Int target)
        {
            Vector2Int diff = target - _cell;
            var primary = Mathf.Abs(diff.x) > Mathf.Abs(diff.y)
                ? new Vector2Int(System.Math.Sign(diff.x), 0)
                : new Vector2Int(0, System.Math.Sign(diff.y));
            // 플레이어 칸으로는 이동 허용(=접촉), 그 외 막힌 칸이면 다른 축 시도
            if (_cell + primary == target || !FieldGrid.Current.IsBlocked(_cell + primary, _trigger, false)) return primary;

            var secondary = primary.x != 0
                ? new Vector2Int(0, System.Math.Sign(diff.y))
                : new Vector2Int(System.Math.Sign(diff.x), 0);
            return secondary;
        }

        private IEnumerator Step(Vector2Int dir)
        {
            Vector2Int target = _cell + dir;
            bool intoPlayer = PlayerController.Instance != null && PlayerController.Instance.Cell == target;
            if (!intoPlayer && FieldGrid.Current.IsBlocked(target, _trigger, false)) yield break;

            if (silhouette != null && dir.x != 0) silhouette.flipX = dir.x < 0;

            Vector3 from = transform.position;
            Vector3 to = FieldGrid.Current.CellToWorld(target);
            _cell = target;
            for (float t = 0f; t < 1f; t += Time.deltaTime / stepDuration)
            {
                if (_frozen) break;
                float weighted = t - Mathf.Sin(t * Mathf.PI * 2f) * .045f;
                transform.position = Vector3.LerpUnclamped(from, to, weighted);
                yield return null;
            }
            if (!_frozen)
            {
                transform.position = to;
                FieldTraversalEffects.Instance?.PlayWorldStep(target, .62f);
            }
        }

        private static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        #endregion

        // ────────────────────────────────────────────

        private IEnumerator BlinkRoutine(float duration)
        {
            if (silhouette == null) yield break;
            float end = Time.time + duration;
            while (Time.time < end)
            {
                silhouette.enabled = !silhouette.enabled;
                yield return new WaitForSeconds(0.12f);
            }
            silhouette.enabled = true;
        }

        private void SetVisible(bool on)
        {
            IsActive = on;
            _trigger.enabled = on;
            if (silhouette != null) silhouette.enabled = on;
        }
    }
}
