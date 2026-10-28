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
                // A dark backing keeps route beads legible on both snow and illuminated roads.
                Diamond(mesh, point, 5f, new Color(.025f, .04f, .06f, .85f));
                Diamond(mesh, point, 3f, color);
            }
        }

        private static void Diamond(VertexHelper mesh, Vector2 point, float radius, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(point + Vector2.up * radius, tint, Vector2.zero);
            mesh.AddVert(point + Vector2.right * radius, tint, Vector2.zero);
            mesh.AddVert(point + Vector2.down * radius, tint, Vector2.zero);
            mesh.AddVert(point + Vector2.left * radius, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
