using System.Collections.Generic;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>플레이어의 발걸음에 가까운 꽃·나뭇잎·등불·천막·문·다리가 가볍게 반응한다.</summary>
    [DisallowMultipleComponent]
    public sealed class FieldEnvironmentReactor : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField, Range(.5f, 4f)] private float reactionRadius = 2.15f;

        private readonly List<ReactiveVisual> _visuals = new List<ReactiveVisual>();

        private sealed class ReactiveVisual
        {
            public Transform transform;
            public Vector3 scale;
            public Quaternion rotation;
            public float impulse;
            public float direction;
            public float phase;
            public Kind kind;
        }

        private enum Kind { Foliage, Lantern, Cloth, Door, Bridge }

        private void Awake()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
        }

        private void Start() => RefreshVisuals();

        private void OnEnable()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            if (controller != null) controller.OnStepStarted += ReactToStep;
        }

        private void OnDisable()
        {
            if (controller != null) controller.OnStepStarted -= ReactToStep;
            RestoreAll();
        }

        public void Configure(PlayerController owner) => controller = owner;

        public void RefreshVisuals()
        {
            RestoreAll();
            _visuals.Clear();
            foreach (SpriteRenderer renderer in FindObjectsOfType<SpriteRenderer>())
            {
                if (renderer == null || renderer.transform.IsChildOf(transform) ||
                    renderer.GetComponentInParent<PlayerController>() != null ||
                    renderer.GetComponent<ShadowTheater.Story.TownLifeProp>()!=null ||
                    FullName(renderer.transform).Contains("CompanionShadow")) continue;
                if (!TryKind(renderer.transform, out Kind kind)) continue;
                _visuals.Add(new ReactiveVisual
                {
                    transform = renderer.transform, kind = kind,
                    scale = renderer.transform.localScale, rotation = renderer.transform.localRotation,
                    phase = (renderer.GetInstanceID() & 31) * .37f
                });
            }
        }

        private void ReactToStep(Vector2Int from, Vector2Int to)
        {
            Vector3 world = FieldGrid.Current != null ? FieldGrid.Current.CellToWorld(to) : (Vector3)(Vector2)to;
            Vector2 movement = to - from;
            foreach (ReactiveVisual visual in _visuals)
            {
                if (visual.transform == null) continue;
                float distance = Vector2.Distance(world, visual.transform.position);
                if (distance > reactionRadius) continue;
                visual.impulse = Mathf.Max(visual.impulse, 1f - distance / reactionRadius);
                float side = visual.transform.position.x - world.x;
                visual.direction = Mathf.Abs(side) > .04f ? Mathf.Sign(side) : -Mathf.Sign(movement.x == 0 ? 1 : movement.x);
            }
        }

        private void LateUpdate()
        {
            foreach (ReactiveVisual visual in _visuals)
            {
                if (visual.transform == null) continue;
                visual.impulse = Mathf.MoveTowards(visual.impulse, 0f, Time.deltaTime * 2.7f);
                float wave = Mathf.Sin(Time.time * 3.1f + visual.phase);
                float kick = Mathf.Sin(visual.impulse * Mathf.PI) * visual.direction;
                float angle = 0f;
                Vector3 scale = visual.scale;
                switch (visual.kind)
                {
                    case Kind.Foliage:
                        angle = wave * .7f + kick * 8f;
                        scale = Vector3.Scale(scale, new Vector3(1f + visual.impulse * .05f, 1f - visual.impulse * .06f, 1f));
                        break;
                    case Kind.Lantern:
                        angle = wave * 1.4f + kick * 5f;
                        break;
                    case Kind.Cloth: angle = wave * 1.2f + kick * 6f; break;
                    case Kind.Door:
                        scale = Vector3.Scale(scale, new Vector3(1f - visual.impulse * .08f, 1f + visual.impulse * .025f, 1f));
                        break;
                    case Kind.Bridge: angle = kick * 1.2f; break;
                }
                visual.transform.localRotation = visual.rotation * Quaternion.Euler(0f,0f,angle);
                visual.transform.localScale = scale;
            }
        }

        private void RestoreAll()
        {
            foreach (ReactiveVisual visual in _visuals)
            {
                if (visual.transform == null) continue;
                visual.transform.localScale = visual.scale;
                visual.transform.localRotation = visual.rotation;
            }
        }

        private static bool TryKind(Transform target, out Kind kind)
        {
            string value = FullName(target).ToLowerInvariant();
            if (ContainsAny(value,"flower","grass","bush","leaf","tree","vine","petal")) { kind=Kind.Foliage; return true; }
            if (ContainsAny(value,"lantern","lamp","light","torch")) { kind=Kind.Lantern; return true; }
            if (ContainsAny(value,"tent","banner","flag","curtain","cloth")) { kind=Kind.Cloth; return true; }
            if (ContainsAny(value,"door","gate","portal")) { kind=Kind.Door; return true; }
            if (ContainsAny(value,"bridge","pier","dock")) { kind=Kind.Bridge; return true; }
            kind = Kind.Foliage; return false;
        }

        private static string FullName(Transform target) => target.parent == null
            ? target.name : target.parent.name + "/" + target.name;

        private static bool ContainsAny(string value, params string[] tokens)
        {
            foreach (string token in tokens) if (value.Contains(token)) return true;
            return false;
        }
    }
}
