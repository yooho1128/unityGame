using System.Collections;
using System.Collections.Generic;
using ShadowTheater.Data;
using ShadowTheater.Save;
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>플레이어의 안전한 발자국을 독립적으로 밟으며 일정한 간격을 유지하는 동행 그림자.</summary>
    public class ShadowCompanionFollower : MonoBehaviour
    {
        [SerializeField, Min(.05f)] private float moveDuration = .19f;
        [SerializeField, Range(0f, .2f)] private float reactionDelay = .055f;
        [SerializeField, Range(1f, 2f)] private float catchUpMultiplier = 1.38f;

        private readonly Queue<Vector2Int> _trail = new Queue<Vector2Int>();
        private Transform _actor;
        private SpriteRenderer _renderer;
        private ShadowFieldMotion _motion;
        private PlayerController _player;
        private Vector2Int _actorCell;
        private Coroutine _move;
        private bool _started;
        private ShadowInstance _companion;
        private SpriteRenderer _emote;
        private float _lastActivityAt;
        private float _nextCuriosityAt;
        private float _gestureUntil;
        private float _bondReactionDelay;

        private IEnumerator Start()
        {
            yield return null;
            _player = PlayerController.Instance;
            if (_player == null) yield break;
            CreateActor();
            ResetTrail();
            _player.OnStepStarted += RememberFootstep;
            _player.OnInteractionStarted += ReactToInteraction;
            SaveManager.PartyChanged += RefreshCompanion;
            RefreshCompanion();
            _lastActivityAt = Time.time;
            _nextCuriosityAt = Time.time + 2.4f;
            _started = true;
        }

        private void Update()
        {
            if (!_started || _actor == null || !_actor.gameObject.activeSelf || _player == null) return;
            bool resting = _move == null && _trail.Count == 0 && !_player.IsMoving;
            if (!resting) { _lastActivityAt = Time.time; FadeEmote(0f); return; }

            Vector3 home = FieldGrid.Current != null ? FieldGrid.Current.CellToWorld(_actorCell) : _actor.position;
            float idle = Time.time - _lastActivityAt;
            float bond = _companion != null ? CompanionBondService.BondLevel(_companion.instanceId) : 1;
            float closeness = Mathf.InverseLerp(1f,5f,bond);
            float orbit = idle > 1.1f ? Mathf.Sin(Time.time*.85f + GetInstanceID()*.1f) * .045f*(1f-closeness*.45f) : 0f;
            Vector3 desired = home + Vector3.right*orbit;
            if (Time.time < _gestureUntil) desired += Vector3.up * (Mathf.Sin(Mathf.InverseLerp(_gestureUntil-.55f,_gestureUntil,Time.time)*Mathf.PI)*.075f);
            _actor.position = Vector3.Lerp(_actor.position, desired, 1f-Mathf.Exp(-Time.deltaTime*7f));

            if (idle > 1.2f && Time.time >= _nextCuriosityAt)
            {
                Transform interest = FindNearbyInterest();
                if (interest != null && _renderer != null)
                {
                    float dx = interest.position.x - _actor.position.x;
                    if (Mathf.Abs(dx) > .05f) _renderer.flipX = dx < 0f;
                    _gestureUntil = Time.time + .55f;
                    _motion?.PlayGesture(.55f);
                }
                float personality=PersonalityInterval();
                _nextCuriosityAt = Time.time + personality*Mathf.Lerp(1.08f,.78f,closeness);
            }
            FadeEmote(Time.time < _gestureUntil ? .9f : 0f);
        }

        private void OnEnable()
        {
            if (!_started || _player == null || _actor == null) return;
            _move = StartCoroutine(ResumeTrail());
        }

        private IEnumerator ResumeTrail()
        {
            while (FieldGrid.Current == null) yield return null;
            ResetTrail();
            _move = null;
        }

        private void ResetTrail()
        {
            _trail.Clear();
            if (_actor == null || _player == null || FieldGrid.Current == null) return;
            _actorCell = FindStandCellBehindPlayer();
            _actor.position = FieldGrid.Current.CellToWorld(_actorCell);
        }

        private Vector2Int FindStandCellBehindPlayer()
        {
            Vector2Int facing = FieldGrid.DirToVector(_player.Facing);
            Vector2Int behind = _player.Cell - facing;
            if (!FieldGrid.Current.IsBlocked(behind)) return behind;
            Vector2Int side = new Vector2Int(-facing.y, facing.x);
            Vector2Int[] alternatives = { _player.Cell + side, _player.Cell - side, _player.Cell + facing };
            foreach (Vector2Int cell in alternatives)
                if (!FieldGrid.Current.IsBlocked(cell)) return cell;
            return _player.Cell;
        }

        private void OnDisable()
        {
            if (_move != null) StopCoroutine(_move);
            _move = null;
            _trail.Clear();
        }

        private void OnDestroy()
        {
            if (_player != null) _player.OnStepStarted -= RememberFootstep;
            if (_player != null) _player.OnInteractionStarted -= ReactToInteraction;
            SaveManager.PartyChanged -= RefreshCompanion;
        }

        private void CreateActor()
        {
            var actor = new GameObject("CompanionShadow");
            actor.transform.SetParent(transform, false);
            _actor = actor.transform;
            var visual = new GameObject("Visual", typeof(SpriteRenderer), typeof(ShadowFieldMotion));
            visual.transform.SetParent(_actor, false);
            _renderer = visual.GetComponent<SpriteRenderer>();
            _renderer.sortingOrder = 9;
            _motion = visual.GetComponent<ShadowFieldMotion>();
            actor.AddComponent<FieldDepthSorter>().Configure(_renderer, -2);
            actor.AddComponent<GroundContactShadow>().Configure(_renderer);
            var emote = new GameObject("CompanionEmote", typeof(SpriteRenderer));
            emote.transform.SetParent(_actor, false); emote.transform.localPosition = new Vector3(0f,1.03f,0f);
            _emote = emote.GetComponent<SpriteRenderer>(); _emote.sprite = BattleFxSpriteLibrary.Get(BattleFxGlyph.Star);
            _emote.color = new Color(.68f,.92f,1f,0f); _emote.sortingOrder = 311;
        }

        private void RefreshCompanion()
        {
            SaveData save = SaveManager.Current;
            ShadowInstance companion = save?.party?.Find(x => x.instanceId == save.companionInstanceId);
            if (companion == null && save?.party?.Count > 0) companion = save.party[0];
            _companion = companion;
            if (_actor == null) return;
            _actor.gameObject.SetActive(companion != null);
            if (companion == null) return;
            Sprite[] frames = companion.Data.fieldFrames;
            _renderer.sprite = frames != null && frames.Length > 0 ? frames[0] : companion.Silhouette;
            _renderer.color = Color.white;
            FieldSpriteSizing.Fit(_renderer, .92f);
            _motion.Configure(companion.Data, frames);
            int bondLevel = CompanionBondService.BondLevel(companion.instanceId);
            _bondReactionDelay = reactionDelay * Mathf.Lerp(1.18f,.55f,Mathf.InverseLerp(1f,5f,bondLevel));
        }

        private void RememberFootstep(Vector2Int from, Vector2Int to)
        {
            if (_actor == null || !_actor.gameObject.activeSelf || FieldGrid.Current == null) return;
            _lastActivityAt = Time.time;
            // 현재 서 있는 칸을 다시 넣지 않아 제자리 떨림을 막는다.
            if (_trail.Count == 0 && from == _actorCell) return;
            _trail.Enqueue(from);
            if (_move == null) _move = StartCoroutine(FollowTrail());
        }

        private IEnumerator FollowTrail()
        {
            if (_trail.Count <= 1 && _bondReactionDelay > 0f) yield return new WaitForSeconds(_bondReactionDelay);
            while (_trail.Count > 0)
            {
                bool catchingUp = _trail.Count >= 2;
                Vector2Int targetCell = _trail.Dequeue();
                int distance = Mathf.Abs(targetCell.x - _actorCell.x) + Mathf.Abs(targetCell.y - _actorCell.y);
                if (distance > 4)
                {
                    _actorCell = targetCell;
                    _actor.position = FieldGrid.Current.CellToWorld(targetCell);
                    continue;
                }

                Vector3 from = _actor.position;
                Vector3 to = FieldGrid.Current.CellToWorld(targetCell);
                if (_renderer != null && Mathf.Abs(to.x - from.x) > .01f) _renderer.flipX = to.x < from.x;
                float speed = catchingUp ? catchUpMultiplier : 1f;
                float duration = moveDuration / speed;
                for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
                {
                    float t = Mathf.Clamp01(elapsed / Mathf.Max(.01f, duration));
                    // 빠른 출발 뒤 부드러운 착지. 매 프레임 목표를 바꾸지 않아 고무줄 느낌이 없다.
                    float eased = 1f - Mathf.Pow(1f - t, 2.4f);
                    _actor.position = Vector3.LerpUnclamped(from, to, eased);
                    yield return null;
                }
                _actor.position = to;
                _actorCell = targetCell;
                FieldTraversalEffects.Instance?.PlayWorldStep(targetCell, .68f);
            }
            _move = null;
            _lastActivityAt = Time.time;
        }

        private Transform FindNearbyInterest()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(_actor.position, 2.4f);
            Transform threat=null;
            foreach (Collider2D hit in hits)
            {
                if (hit == null || hit.transform == _actor || hit.GetComponent<PlayerController>() != null) continue;
                if(hit.GetComponent<EncounterSymbol>()!=null)threat=hit.transform;
                if (hit.GetComponent<IInteractable>() != null) return hit.transform;
            }
            if(threat!=null&&_companion?.Data!=null)
            {
                if(_companion.Data.role==ShadowRole.Support)return _player.transform;
                if(_companion.Data.role==ShadowRole.Tank||_companion.Data.role==ShadowRole.SpeedUtility)return threat;
            }
            return _player.transform;
        }

        private float PersonalityInterval()
        {
            if(_companion?.Data==null)return 3.8f;
            switch(_companion.Data.role)
            {
                case ShadowRole.SpeedUtility:return 2.45f;
                case ShadowRole.Support:return 3.05f;
                case ShadowRole.MagicNuker:return 3.35f;
                case ShadowRole.Tank:return 4.15f;
                default:return 3.65f;
            }
        }

        private void ReactToInteraction(Transform target)
        {
            if (_actor==null || target==null || _renderer==null) return;
            float dx=target.position.x-_actor.position.x;
            if (Mathf.Abs(dx)>.05f) _renderer.flipX=dx<0f;
            _gestureUntil=Time.time+.68f;
            _nextCuriosityAt=Time.time+2.4f;
            _motion?.PlayGesture(.68f);
        }

        private void FadeEmote(float target)
        {
            if (_emote == null) return;
            Color color = _emote.color;
            color.a = Mathf.MoveTowards(color.a,target,Time.deltaTime*5f);
            _emote.color = color;
            _emote.transform.localScale = Vector3.one*(.12f+color.a*.1f);
        }
    }
}
