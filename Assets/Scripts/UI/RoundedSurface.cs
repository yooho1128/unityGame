using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>Resolution-independent rounded surfaces; no built-in bevel sprite or texture atlas.</summary>
    [DisallowMultipleComponent]
    public sealed class RoundedSurface : BaseMeshEffect
    {
        [SerializeField] private float radius = 12f;
        [SerializeField] private float borderWidth = 1f;
        [SerializeField] private Color border = new Color(.40f, .48f, .59f, .24f);

        public void Configure(float cornerRadius, Color stroke, float width = 1f)
        {
            radius = cornerRadius; border = stroke; borderWidth = width;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            mesh.Clear();
            Rect rect = graphic.GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;
            float corner = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * .5f);
            float stroke = Mathf.Min(borderWidth, corner);
            const int steps = 8, count = (steps + 1) * 4;
            Color fill = graphic.color;
            Add(mesh, rect.center, fill);
            // Independent rings keep the border crisp instead of tinting the whole face.
            for (int ring = 0; ring < 4; ring++)
            {
                float inset = ring < 2 ? stroke : ring == 2 ? 0f : -.7f;
                float r = Mathf.Max(0f, corner - inset);
                for (int i = 0; i < count; i++)
                {
                    int quadrant = i / (steps + 1);
                    float angle = (quadrant * 90f + i % (steps + 1) * 90f / steps) * Mathf.Deg2Rad;
                    Vector2 center = new Vector2(quadrant == 0 || quadrant == 3 ? rect.xMax - corner : rect.xMin + corner,
                        quadrant < 2 ? rect.yMax - corner : rect.yMin + corner);
                    Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                    Color color = ring == 0 || stroke == 0f ? fill : border;
                    if (ring > 0 && stroke > 0f) color.a *= fill.a;
                    if (ring == 3) color.a = 0f;
                    Add(mesh, point, color);
                }
            }
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                mesh.AddTriangle(0, 1 + i, 1 + next);
                for (int ring = 1; ring < 3; ring++)
                {
                    int a = 1 + ring * count, b = a + count;
                    mesh.AddTriangle(a + i, b + i, b + next);
                    mesh.AddTriangle(a + i, b + next, a + next);
                }
            }
        }

        private static void Add(VertexHelper mesh, Vector2 position, Color color)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = position; vertex.color = color;
            mesh.AddVert(vertex);
        }
    }
}
