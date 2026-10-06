using System;
using System.Collections;
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
        [SerializeField] private float bobHeight = 0.04f;    // 프레임 애니 없이 걷는 느낌 주는 상하 흔들림

        /// <summary>가상 패드가 매 프레임 써 넣는 입력값</summary>
        [NonSerialized] public Vector2 MoveInput;

        public Vector2Int Cell { get; private set; }
        public FacingDir Facing { get; private set; } = FacingDir.Down;
        public bool IsMoving { get; private set; }
        public bool IsLocked => _lockCount > 0;

        /// <summary>한 칸 이동 완료 (발소리, 랜덤 인카운터, 이벤트 타일 체크용)</summary>
        public event Action<Vector2Int> OnStepFinished;

        private int _lockCount;
        private Collider2D _collider;
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
            SnapToCell(FieldGrid.Current.WorldToCell(transform.position));
        }

        private void Update()
        {
            if (IsLocked || IsMoving) return;

            Vector2Int dir = ReadDirection();
            if (dir != Vector2Int.zero) StartCoroutine(StepRoutine(dir));

#if ENABLE_LEGACY_INPUT_MANAGER && (UNITY_EDITOR || UNITY_STANDALONE)
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Z)) TryInteract();
#endif
        }

        // ────────────────────────────────────────────

        public void Lock() => _lockCount++;
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
            var target = FieldGrid.Current.CellToWorld(Cell + FieldGrid.DirToVector(Facing));
            var hits = Physics2D.OverlapPointAll(target);
            foreach (var h in hits)
            {
                if (h.TryGetComponent<IInteractable>(out var it))
                {
                    it.Interact(this);
                    return;
                }
            }
        }

        // ────────────────────────────────────────────

        private Vector2Int ReadDirection()
        {
            Vector2 v = MoveInput;
#if ENABLE_LEGACY_INPUT_MANAGER && (UNITY_EDITOR || UNITY_STANDALONE)
            if (v == Vector2.zero) v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
            if (v.sqrMagnitude < 0.1f) return Vector2Int.zero;

            // 대각선 입력은 큰 축 하나만 (4방향 고정)
            return Mathf.Abs(v.x) > Mathf.Abs(v.y)
                ? new Vector2Int(v.x > 0 ? 1 : -1, 0)
                : new Vector2Int(0, v.y > 0 ? 1 : -1);
        }

        private IEnumerator StepRoutine(Vector2Int dir)
        {
            IsMoving = true;
            var newFacing = FieldGrid.VectorToDir(dir);
            bool turned = newFacing != Facing;
            SetFacing(newFacing);

            Vector2Int target = Cell + dir;
            if (FieldGrid.Current.IsBlocked(target, _collider))
            {
                // 막혀 있으면 방향만 전환
                if (turned) yield return new WaitForSeconds(turnOnlyDelay);
                IsMoving = false;
                yield break;
            }

            if (animator != null) animator.SetBool(MovingHash, true);

            Vector3 from = transform.position;
            Vector3 to = FieldGrid.Current.CellToWorld(target);
            Cell = target; // 출발 시점에 칸 예약 (심볼과 겹침 방지)

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / stepDuration;
                Vector3 p = Vector3.Lerp(from, to, Mathf.Clamp01(t));
                p.y += Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * bobHeight;
                transform.position = p;
                yield return null;
            }
            transform.position = to;

            IsMoving = false;
            if (animator != null && ReadDirection() == Vector2Int.zero) animator.SetBool(MovingHash, false);
            OnStepFinished?.Invoke(Cell);
        }
    }
}
