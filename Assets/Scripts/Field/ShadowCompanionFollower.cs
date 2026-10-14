using System.Collections;
using ShadowTheater.Data;
using ShadowTheater.Save;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>선택한 파티원 한 마리가 플레이어가 지나간 칸을 한 칸 늦게 따라간다.</summary>
    public class ShadowCompanionFollower : MonoBehaviour
    {
        [SerializeField, Min(.05f)] private float moveDuration = .2f;
        private Transform _actor;
        private SpriteRenderer _renderer;
        private ShadowFieldMotion _motion;
        private PlayerController _player;
        private Vector2Int _lastPlayerCell;
        private Coroutine _move;
        private bool _started;

        private IEnumerator Start()
        {
            yield return null;
            _player = PlayerController.Instance;
            if (_player == null) yield break;
            CreateActor();
            _lastPlayerCell = _player.Cell;
            _actor.position = FieldGrid.Current.CellToWorld(_lastPlayerCell);
            _player.OnStepFinished += FollowStep;
            SaveManager.PartyChanged += RefreshCompanion;
            RefreshCompanion();
            _started = true;
        }

        private void OnEnable()
        {
            if (!_started || _player == null || _actor == null) return;
            _lastPlayerCell = _player.Cell;
            _actor.position = FieldGrid.Current.CellToWorld(_lastPlayerCell);
            _move = null;
        }

        private void OnDisable()
        {
            if (_move != null) StopCoroutine(_move);
            _move = null;
        }

        private void OnDestroy()
        {
            if (_player != null) _player.OnStepFinished -= FollowStep;
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
        }

        private void RefreshCompanion()
        {
            SaveData save = SaveManager.Current;
            ShadowInstance companion = save?.party?.Find(x => x.instanceId == save.companionInstanceId);
            if (companion == null && save?.party?.Count > 0) companion = save.party[0];
            if (_actor == null) return;
            _actor.gameObject.SetActive(companion != null);
            if (companion == null) return;
            _renderer.sprite = companion.Silhouette;
            _renderer.color = Color.white;
            _motion.Configure(companion.Data);
        }

        private void FollowStep(Vector2Int newPlayerCell)
        {
            if (_actor == null || !_actor.gameObject.activeSelf) return;
            Vector2Int target = _lastPlayerCell;
            _lastPlayerCell = newPlayerCell;
            if (_move != null) StopCoroutine(_move);
            _move = StartCoroutine(MoveTo(target));
        }

        private IEnumerator MoveTo(Vector2Int cell)
        {
            Vector3 from = _actor.position;
            Vector3 to = FieldGrid.Current.CellToWorld(cell);
            if (_renderer != null && Mathf.Abs(to.x - from.x) > .01f) _renderer.flipX = to.x < from.x;
            for (float t = 0f; t < 1f; t += Time.deltaTime / moveDuration)
            {
                _actor.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            _actor.position = to;
            _move = null;
        }
    }
}
