using System.Collections;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>씬 시작 시 입구에서 한 발 걸어 나온 듯한 짧은 정착 동작을 재생한다.</summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class FieldArrivalPresentation:MonoBehaviour
    {
        private IEnumerator Start()
        {
            // GameFlow가 저장 좌표를 복구한 뒤 최종 도착 칸을 기준으로 연출한다.
            yield return null;yield return null;
            PlayerController player=GetComponent<PlayerController>();
            if(player==null||FieldGrid.Current==null||player.IsMoving)yield break;
            Vector3 destination=FieldGrid.Current.CellToWorld(player.Cell);
            Vector2Int facing=FieldGrid.DirToVector(player.Facing);
            Vector3 from=destination-(Vector3)(Vector2)facing*.34f;
            player.Lock();transform.position=from;
            const float duration=.26f;
            for(float elapsed=0f;elapsed<duration;elapsed+=Time.unscaledDeltaTime)
            {
                float t=Mathf.Clamp01(elapsed/duration),ease=1f-Mathf.Pow(1f-t,2.2f);
                transform.position=Vector3.LerpUnclamped(from,destination,ease);yield return null;
            }
            transform.position=destination;player.Unlock();FieldCameraFollow.Instance?.Snap();
        }
    }
}
