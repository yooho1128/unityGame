using UnityEngine;
using UnityEngine.Tilemaps;

namespace ShadowTheater.Field
{
    /// <summary>
    /// 맵 하나당 1개. 타일 좌표 ↔ 월드 좌표 변환과 "이 칸으로 갈 수 있나?" 판정을 담당.
    /// 플레이어와 인카운터 심볼이 같은 규칙으로 움직이도록 공용으로 사용.
    ///
    /// 막힘 판정 = (collisionTilemap에 타일 있음) OR (obstacleMask 레이어 콜라이더 있음) OR (다른 유닛이 예약한 칸)
    /// </summary>
    [RequireComponent(typeof(Grid))]
    public class FieldGrid : MonoBehaviour
    {
        public static FieldGrid Current { get; private set; }

        [SerializeField] private string mapId = "Prologue";
        [Tooltip("이동 가능한 바닥을 그린 Tilemap. 지정하면 바닥이 없는 맵 밖 좌표도 차단")]
        [SerializeField] private Tilemap groundTilemap;
        [Tooltip("벽/물 등 이동 불가 타일을 그린 Tilemap (렌더러 꺼둬도 됨)")]
        [SerializeField] private Tilemap collisionTilemap;
        [Tooltip("나무, 상자 등 오브젝트 콜라이더 레이어")]
        [SerializeField] private LayerMask obstacleMask;
        [Tooltip("NPC/심볼 등 서로 겹치면 안 되는 유닛 레이어")]
        [SerializeField] private LayerMask unitMask;

        private Grid _grid;
        public string MapId => mapId;

        private void Awake()
        {
            _grid = GetComponent<Grid>();
            Current = this;
        }

        private void OnEnable()
        {
            if (_grid == null) _grid = GetComponent<Grid>();
            Current = this;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public Vector2Int WorldToCell(Vector3 world)
        {
            var c = _grid.WorldToCell(world);
            return new Vector2Int(c.x, c.y);
        }

        public Vector3 CellToWorld(Vector2Int cell) =>
            _grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));

        /// <param name="self">판정에서 제외할 자기 자신 콜라이더</param>
        public bool IsBlocked(Vector2Int cell, Collider2D self = null, bool checkUnits = true)
        {
            if (groundTilemap != null && !groundTilemap.HasTile(new Vector3Int(cell.x, cell.y, 0)))
                return true;
            if (collisionTilemap != null && collisionTilemap.HasTile(new Vector3Int(cell.x, cell.y, 0)))
                return true;

            Vector2 center = CellToWorld(cell);
            Vector2 size = (Vector2)_grid.cellSize * 0.8f;

            if (obstacleMask != 0 && Physics2D.OverlapBox(center, size, 0f, obstacleMask) != null)
                return true;

            if (checkUnits && unitMask != 0)
            {
                var hits = Physics2D.OverlapBoxAll(center, size, 0f, unitMask);
                foreach (var h in hits)
                    if (h != self && !h.isTrigger) return true;
            }
            return false;
        }

        /// <summary>
        /// 저장 좌표나 포털 도착점이 맵 수정으로 막혔을 때 가장 가까운 이동 가능 칸을 찾는다.
        /// 같은 거리에서는 아래→좌→우→위 순으로 안정적으로 선택해 실행마다 위치가 달라지지 않는다.
        /// </summary>
        public bool TryFindNearestWalkable(Vector2Int requested, out Vector2Int resolved,
                                           Collider2D self = null, int maxRadius = 12, bool checkUnits = true)
        {
            if (!IsBlocked(requested, self, checkUnits)) { resolved = requested; return true; }
            int limit = Mathf.Max(1, maxRadius);
            Vector2Int[] directions = { Vector2Int.down, Vector2Int.left, Vector2Int.right, Vector2Int.up };
            for (int radius = 1; radius <= limit; radius++)
            {
                foreach (Vector2Int direction in directions)
                {
                    Vector2Int edge = requested + direction * radius;
                    if (!IsBlocked(edge, self, checkUnits)) { resolved = edge; return true; }
                }
                for (int y = -radius; y <= radius; y++)
                {
                    int x = radius - Mathf.Abs(y);
                    if (x <= 0) continue;
                    var left = requested + new Vector2Int(-x, y);
                    if (!IsBlocked(left, self, checkUnits)) { resolved = left; return true; }
                    var right = requested + new Vector2Int(x, y);
                    if (!IsBlocked(right, self, checkUnits)) { resolved = right; return true; }
                }
            }
            resolved = requested;
            return false;
        }

        public static Vector2Int DirToVector(FacingDir dir)
        {
            switch (dir)
            {
                case FacingDir.Up:    return Vector2Int.up;
                case FacingDir.Left:  return Vector2Int.left;
                case FacingDir.Right: return Vector2Int.right;
                default:              return Vector2Int.down;
            }
        }

        public static FacingDir VectorToDir(Vector2Int v)
        {
            if (v.y > 0) return FacingDir.Up;
            if (v.y < 0) return FacingDir.Down;
            if (v.x < 0) return FacingDir.Left;
            return FacingDir.Right;
        }
    }

    public enum FacingDir { Down = 0, Left = 1, Right = 2, Up = 3 }

    /// <summary>NPC, 표지판, 상자 등 [A]버튼으로 상호작용하는 대상</summary>
    public interface IInteractable
    {
        void Interact(PlayerController player);
    }
}
