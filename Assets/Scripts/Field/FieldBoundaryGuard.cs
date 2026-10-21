using System.Collections;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>포털·저장 좌표·연출 이동이 잘못되어도 플레이어를 마지막 안전 칸 안에 유지한다.</summary>
    [DisallowMultipleComponent]
    public sealed class FieldBoundaryGuard : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField, Min(.05f)] private float validationInterval=.18f;
        private Vector2Int _lastSafeCell;
        private Vector2Int _sceneFallback;
        private float _nextValidation;

        private void Awake(){if(controller==null)controller=GetComponent<PlayerController>();}
        private IEnumerator Start()
        {
            yield return null;yield return null;
            if(controller==null||FieldGrid.Current==null)yield break;
            _sceneFallback=controller.Cell;
            if(FieldGrid.Current.TryFindNearestWalkable(controller.Cell,out Vector2Int safe,
                controller.GetComponent<Collider2D>(),16,false))
            {
                _lastSafeCell=safe;
                if(safe!=controller.Cell)controller.SnapToCell(safe);
            }
            controller.OnStepFinished+=RememberSafeCell;
        }

        private void OnDestroy(){if(controller!=null)controller.OnStepFinished-=RememberSafeCell;}

        private void RememberSafeCell(Vector2Int cell)
        {
            if(FieldGrid.Current!=null&&!FieldGrid.Current.IsBlocked(cell,controller.GetComponent<Collider2D>(),false))
                _lastSafeCell=cell;
        }

        private void LateUpdate()
        {
            if(controller==null||controller.IsMoving||controller.IsLocked||FieldGrid.Current==null||
                Time.unscaledTime<_nextValidation)return;
            _nextValidation=Time.unscaledTime+validationInterval;
            Vector2Int actual=FieldGrid.Current.WorldToCell(transform.position);
            bool invalidCell=FieldGrid.Current.IsBlocked(controller.Cell,controller.GetComponent<Collider2D>(),false);
            bool invalidWorld=FieldGrid.Current.IsBlocked(actual,controller.GetComponent<Collider2D>(),false);
            if(!invalidCell&&!invalidWorld)
            {
                if(actual!=controller.Cell)controller.SnapToCell(actual);
                _lastSafeCell=actual;
                return;
            }
            if(controller.SnapToSafeCell(_lastSafeCell,_sceneFallback,out Vector2Int recovered))
            {
                _lastSafeCell=recovered;
                FieldCameraFollow.Instance?.Snap();
                SaveFeedbackController.Instance?.ShowMessage("길 밖으로 벗어나 마지막 안전 위치로 돌아왔습니다.");
            }
        }
    }
}
