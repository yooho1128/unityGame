using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>Quiet stage framing and drifting memory lights behind explicitly marked story scenes.</summary>
    public sealed class StorySceneGraphic : MaskableGraphic
    {
        public bool Cinematic { get; set; }
        public Color Accent { get; set; } = TheatreUiStyle.Brass;
        private float _blend;

        protected override void OnEnable() { base.OnEnable(); raycastTarget = false; _blend = 0f; }
        private void Update()
        {
            float next = Mathf.MoveTowards(_blend, Cinematic ? 1f : 0f, Time.unscaledDeltaTime * 3f);
            if (next != _blend || next > 0f) { _blend = next; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (_blend <= 0f) return;
            Rect rect = GetPixelAdjustedRect();
            Color shade = new Color(.015f, .025f, .04f, .78f * _blend);
            Quad(mesh, new Rect(rect.xMin, rect.yMax - rect.height * .065f, rect.width, rect.height * .065f), shade);
            Quad(mesh, new Rect(rect.xMin, rect.yMin, rect.width, rect.height * .025f), shade);
            if (GameSettings.ReducedMotion) return;
            float time = Time.unscaledTime;
            for (int i = 0; i < 18; i++)
            {
                float phase = Mathf.Repeat(i * .618f + time * (.006f + i % 3 * .002f), 1f);
                float x = rect.xMin + rect.width * Mathf.Repeat(i * .381966f + Mathf.Sin(time * .16f + i) * .012f, 1f);
                float y = rect.yMin + rect.height * (.43f + phase * .48f);
                float alpha = Mathf.Sin(phase * Mathf.PI) * .38f * _blend;
                Color tint = new Color(Accent.r, Accent.g, Accent.b, alpha);
                Diamond(mesh, new Vector2(x, y), 2.5f + i % 4, tint);
            }
        }

        private static void Quad(VertexHelper mesh, Rect r, Color color)
        {
            int start = mesh.currentVertCount;
            Add(mesh, new Vector2(r.xMin, r.yMin), color); Add(mesh, new Vector2(r.xMin, r.yMax), color);
            Add(mesh, new Vector2(r.xMax, r.yMax), color); Add(mesh, new Vector2(r.xMax, r.yMin), color);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
        private static void Diamond(VertexHelper mesh, Vector2 center, float radius, Color color)
        {
            int start = mesh.currentVertCount;
            Add(mesh, center, color); color.a = 0;
            Add(mesh, center + Vector2.up * radius * 2, color); Add(mesh, center + Vector2.right * radius, color);
            Add(mesh, center + Vector2.down * radius * 2, color); Add(mesh, center + Vector2.left * radius, color);
            for (int i = 0; i < 4; i++) mesh.AddTriangle(start, start + i + 1, start + (i + 1) % 4 + 1);
        }
        private static void Add(VertexHelper mesh, Vector2 point, Color color)
        {
            var vertex = UIVertex.simpleVert; vertex.position = point; vertex.color = color; mesh.AddVert(vertex);
        }
    }
}
