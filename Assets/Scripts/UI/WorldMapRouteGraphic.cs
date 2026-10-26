using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>One masked mesh for the entire dotted route, rather than one GameObject per dot.</summary>
    public sealed class WorldMapRouteGraphic : MaskableGraphic
    {
        private readonly List<Vector2> points = new List<Vector2>();

        public void SetPoints(List<Vector2> value)
        {
            points.Clear();
            points.AddRange(value);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            foreach (var point in points)
            {
                int start = mesh.currentVertCount;
                mesh.AddVert(point + Vector2.up * 3, color, Vector2.zero);
                mesh.AddVert(point + Vector2.right * 3, color, Vector2.zero);
                mesh.AddVert(point + Vector2.down * 3, color, Vector2.zero);
                mesh.AddVert(point + Vector2.left * 3, color, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
