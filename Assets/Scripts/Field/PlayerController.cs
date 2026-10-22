using System;
using System.Collections;
using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>
    /// 《바람의 나라》식 4방향 타일 단위 이동.
    /// - 입력은 MoveInput(가상 패드) 또는 에디터 키보드(WASD/방향키)
    /// - 한 칸 이동이 끝날 때까지 다음 입력을 받지 않고, 누르고 있으면 연속 이동
    /// - Lock()/Unlock()은 카운터 방식 → 대화 중 + 전투 전환 등이 겹쳐도 안전
    ///
    /// 필요 컴포넌트: Rigidbody2D(Kinematic), Collider2D, Tag = "Player"
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [SerializeField] private float stepDuration = 0.18f;
        [SerializeField] private float turnOnlyDelay = 0.06f; // 방향만 바꿀 때 짧은 대기 (제자리 회전)
        [SerializeField] private SpriteRenderer silhouette;
        [SerializeField] private Animator animator;          // 선택: 파라미터 "Dir"(int), "Moving"(bool)
        [SerializeField] private float bobHeight = 0.025f;   // 격자 판정을 흐리지 않는 작은 체중 이동
        [Header("Natural Input")]
        [SerializeField, Range(.08f, .35f)] private float inputBufferDuration = .22f;
        [SerializeField, Range(0f, .4f)] private float axisSwitchThreshold = .14f;
        [SerializeField, Range(.03f, .2f)] private float blockedRepeatDelay = .09f;
        [SerializeField, Range(.22f, .55f)] private float holdRepeatDelay = .34f;

        /// <summary>가상 패드가 매 프레임 써 넣는 입력값</summary>
        [NonSerialized] public Vector2 MoveInput;

        public Vector2Int Cell { get; private set; }
        public FacingDir Facing { get; private set; } = FacingDir.Down;
        public bool IsMoving { get; private set; }
        public float StepProgress { get; private set; }
        public int CompletedSteps { get; private set; }
        public bool IsLocked => _lockCount > 0;

        /// <summary>한 칸 이동 완료 (발소리, 랜덤 인카운터, 이벤트 타일 체크용)</summary>
        public event Action<Vector2Int> OnStepFinished;
        /// <summary>이동이 시작될 때 출발 칸과 도착 칸을 전달한다. 동행자는 이 발자국을 따라간다.</summary>
        public event Action<Vector2Int, Vector2Int> OnStepStarted;
        /// <summary>대화·조사를 시작할 때 대상 위치를 전달해 동행자와 카메라가 같은 장면에 반응한다.</summary>
        public event Action<Transform> OnInteractionStarted;

        private int _lockCount;
        private Collider2D _collider;
        private FieldGrid _fieldGrid;
        private Vector2Int _lastStepDirection;
        private int _heldDirectionSteps;
        private float _lastStepCompletedAt = -10f;
        private Vector2Int _bufferedDirection;
        private float _bufferedUntil;
        private float _bufferedAt;
        private float _stepStartedAt;
        private float _inputPressedAt;
        private bool _inputHeld;
        private bool _steppedForCurrentPress;
        private bool _dominantHorizontal;
        private static readonly int DirHash = Animator.StringToHash("Dir");
        private static readonly int MovingHash = Animator.StringToHash("Moving");

        private void Awake()
        {
            Instance = this;
            _collider = GetComponent<Collider2D>();
            GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        }

        private void Start()
        {
            _fieldGrid = FieldGrid.Current;
            Vector2Int requested=FieldGrid.Current.WorldToCell(transform.position);
            if (!SnapToSafeCell(requested,Vector2Int.zero,out _)) SnapToCell(requested);
        }

        private void OnDisable()
        {
            // A field toggle stops movement coroutines before their final cleanup runs.
            StopAllCoroutines();
            if (IsMoving && _fieldGrid != null) transform.position = _fieldGrid.CellToWorld(Cell);
            IsMoving = false;
            StepProgress = 0f;
            _heldDirectionSteps = 0;
            _bufferedDirection = Vector2Int.zero;
            _bufferedUntil = 0f;
            _inputHeld = false;
            _steppedForCurrentPress = false;
            MoveInput = Vector2.zero;
            if (Instance == this) VirtualDPadButton.CancelInput();
            if (animator != null) animator.SetBool(MovingHash, false);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            SampleInput();
            if (IsLocked || IsMoving) return;

            Vector2Int dir = BufferedDirection();
            if (dir != Vector2Int.zero) StartCoroutine(StepRoutine(dir));

#if ENABLE_LEGACY_INPUT_MANAGER && (UNITY_EDITOR || UNITY_STANDALONE)
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Z)) TryInteract();
#endif
        }

        // ────────────────────────────────────────────

        public void Lock() { _lockCount++; VirtualDPadButton.CancelInput(); }
        public void Unlock() => _lockCount = Mathf.Max(0, _lockCount - 1);

        public void SnapToCell(Vector2Int cell)
        {
            Cell = cell;
            transform.position = FieldGrid.Current.CellToWorld(cell);
        }

        /// <summary>요청 칸이 벽·절벽·맵 밖이면 주변, 그다음 안전 기준점 주변으로 복구한다.</summary>
        public bool SnapToSafeCell(Vector2Int requested, Vector2Int fallback, out Vector2Int resolved)
        {
            var grid = FieldGrid.Current;
            if (grid != null && (grid.TryFindNearestWalkable(requested, out resolved, _collider) ||
                                 grid.TryFindNearestWalkable(fallback, out resolved, _collider)))
            {
                SnapToCell(resolved);
                return true;
            }
            resolved = fallback;
            Debug.LogError($"[Player] 이동 가능한 복구 좌표를 찾지 못했습니다: {requested} / {fallback}");
            return false;
        }

        public void SetFacing(FacingDir dir)
        {
            Facing = dir;
            if (silhouette != null && (dir == FacingDir.Left || dir == FacingDir.Right))
                silhouette.flipX = dir == FacingDir.Left;
            if (animator != null) animator.SetInteger(DirHash, (int)dir);
        }

        /// <summary>바라보는 칸의 IInteractable 실행 ([A] 버튼)</summary>
        public void TryInteract()
        {
            if (IsLocked || IsMoving) return;
            if (TryGetInteraction(out var interaction))
            {
                var component = interaction as Component;
                if (component != null) FieldCameraFollow.Instance?.FocusOn(component.transform.position, .48f);
                GetComponent<FieldInteractionFeedback>()?.PlayConfirm();
                if (component != null) OnInteractionStarted?.Invoke(component.transform);
                interaction.Interact(this);
            }
            else GetComponent<FieldTrailGuide>()?.ShowHint();
        }

        public bool TryGetInteraction(out IInteractable interaction)
        {
            interaction = null;
            if (FieldGrid.Current == null) return false;
            var target = FieldGrid.Current.CellToWorld(Cell + FieldGrid.DirToVector(Facing));
            // 생활 NPC는 일정에 따라 칸 중앙 사이에 멈출 수 있으므로 작은 조사 반경을 허용한다.
            var hits = Physics2D.OverlapCircleAll(target,.42f);
            foreach (var h in hits)
            {
                if (h.TryGetComponent<IInteractable>(out var it))
                {
                    interaction = it;
                    return true;
                }
            }
            return false;
        }

        // ────────────────────────────────────────────

        private void SampleInput()
        {
            Vector2 v = MoveInput;
#if ENABLE_LEGACY_INPUT_MANAGER && (UNITY_EDITOR || UNITY_STANDALONE)
            if (v == Vector2.zero) v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
            bool held = v.sqrMagnitude >= .1f;
            if (held && !_inputHeld)
            {
                _inputPressedAt = Time.unscaledTime;
                _steppedForCurrentPress = false;
            }
            _inputHeld = held;
            if (!_inputHeld) return;

            bool previousAxis = _bufferedDirection == Vector2Int.zero
                ? Mathf.Abs(v.x)>Mathf.Abs(v.y) : _dominantHorizontal;
            _bufferedDirection = ResolveDirection(v,previousAxis,axisSwitchThreshold,out _dominantHorizontal);
            _bufferedAt = Time.unscaledTime;
            _bufferedUntil = Time.unscaledTime + inputBufferDuration;
        }

        public static Vector2Int ResolveDirection(Vector2 input, bool previousHorizontal,
            float switchThreshold, out bool horizontal)
        {
            float x=Mathf.Abs(input.x), y=Mathf.Abs(input.y);
            horizontal = x>y+switchThreshold ? true : y>x+switchThreshold ? false : previousHorizontal;
            return horizontal ? new Vector2Int(input.x>=0f?1:-1,0)
                : new Vector2Int(0,input.y>=0f?1:-1);
        }

        public static bool CanRepeatHeldStep(bool alreadyStepped, float heldDuration, float repeatDelay) =>
            !alreadyStepped || heldDuration >= Mathf.Max(0f,repeatDelay);

        private Vector2Int BufferedDirection()
        {
            if (_inputHeld)
            {
                if (!CanRepeatHeldStep(_steppedForCurrentPress,
                    Time.unscaledTime-_inputPressedAt,holdRepeatDelay))
                    return Vector2Int.zero;
                return _bufferedDirection;
            }
            return Time.unscaledTime <= _bufferedUntil ? _bufferedDirection : Vector2Int.zero;
        }

        private Vector2Int BufferedFollowUpDirection(Vector2Int completedDirection)
        {
            if (_inputHeld)
            {
                bool changedDirection = _bufferedDirection != Vector2Int.zero &&
                                        _bufferedDirection != completedDirection;
                bool holdRepeatReady = CanRepeatHeldStep(_steppedForCurrentPress,
                    Time.unscaledTime-_inputPressedAt,holdRepeatDelay);
                return changedDirection || holdRepeatReady
                    ? _bufferedDirection : Vector2Int.zero;
            }

            // A short tap is sampled for several frames while the current step is moving.
            // Do not mistake those repeated samples for a request to walk a second tile.
            // Only a genuinely different direction pressed during the step is buffered.
            bool changedDirection = _bufferedDirection != Vector2Int.zero &&
                                    _bufferedDirection != completedDirection;
            return changedDirection && _bufferedAt > _stepStartedAt + .001f &&
                   Time.unscaledTime <= _bufferedUntil
                ? _bufferedDirection : Vector2Int.zero;
        }

        private void ClearBufferedInput()
        {
            _bufferedDirection = Vector2Int.zero;
            _bufferedUntil = 0f;
            _bufferedAt = 0f;
        }

        private IEnumerator StepRoutine(Vector2Int dir)
        {
            IsMoving = true;
            _stepStartedAt = Time.unscaledTime;
            var newFacing = FieldGrid.VectorToDir(dir);
            bool turned = newFacing != Facing;
            SetFacing(newFacing);

            Vector2Int target = Cell + dir;
            string tileName = FieldGrid.Current.GroundTileName(target);
            int heldSteps = dir == _lastStepDirection && Time.time-_lastStepCompletedAt < .34f
                ? _heldDirectionSteps + 1 : 0;
            TraversalMotionProfile motion = FieldTraversalMotion.Profile(tileName, dir, heldSteps);
            if (!motion.allowed)
            {
                FieldCameraFollow.Instance?.Nudge(-(Vector2)dir * .045f);
                if (!_inputHeld) _bufferedDirection = Vector2Int.zero;
                yield return new WaitForSeconds(turned ? turnOnlyDelay : blockedRepeatDelay);
                IsMoving = false;
                if (animator != null) animator.SetBool(MovingHash,false);
                yield break;
            }
            if (FieldGrid.Current.IsBlocked(target, _collider))
            {
                // 막혀 있으면 방향만 전환
                _heldDirectionSteps = 0;
                FieldCameraFollow.Instance?.Nudge(-(Vector2)dir * .045f);
                if (!_inputHeld) _bufferedDirection = Vector2Int.zero;
                yield return new WaitForSeconds(turned ? turnOnlyDelay : blockedRepeatDelay);
                IsMoving = false;
                if (animator != null) animator.SetBool(MovingHash,false);
                yield break;
            }

            if (animator != null) animator.SetBool(MovingHash, true);

            Vector3 from = transform.position;
            Vector3 to = FieldGrid.Current.CellToWorld(target);
            OnStepStarted?.Invoke(Cell, target);
            Cell = target; // 출발 시점에 칸 예약 (심볼과 겹침 방지)
            _steppedForCurrentPress = true;
            _lastStepDirection = dir;
            _heldDirectionSteps = heldSteps;

            RegionData region = RegionRepository.GetByScene(SaveManager.Current?.mapId);
            float duration = stepDuration * motion.durationMultiplier *
                FieldWorldState.MovementDurationMultiplier(FieldWorldState.Current(region));
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(.01f, duration);
                StepProgress = Mathf.Clamp01(t);
                // 칸 경계에서 완전히 멈추지 않으면서 출발과 착지에 미세한 무게를 준다.
                float weighted = StepProgress - Mathf.Sin(StepProgress * Mathf.PI * 2f) * .035f;
                if (motion.overshoot > 0f) weighted += Mathf.Sin(StepProgress * Mathf.PI) * motion.overshoot;
                Vector3 p = Vector3.LerpUnclamped(from, to, weighted);
                p.y += Mathf.Sin(StepProgress * Mathf.PI) * bobHeight * motion.arcMultiplier;
                transform.position = p;
                yield return null;
            }
            transform.position = to;

            IsMoving = false;
            StepProgress = 0f;
            CompletedSteps++;
            _lastStepCompletedAt = Time.time;
            OnStepFinished?.Invoke(Cell);

            // 이동 중 들어온 방향 전환을 다음 프레임까지 버리지 않고 바로 이어 걷는다.
            Vector2Int next = IsLocked ? Vector2Int.zero : BufferedFollowUpDirection(dir);
            if (next != Vector2Int.zero)
            {
                StartCoroutine(StepRoutine(next));
                yield break;
            }
            if (!_inputHeld) ClearBufferedInput();
            if (animator != null) animator.SetBool(MovingHash, false);
        }
    }
}
