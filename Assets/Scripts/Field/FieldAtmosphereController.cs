using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>지역 안개층과 빛가루를 저비용으로 이동·점멸시키는 모바일용 환경 연출.</summary>
    public class FieldAtmosphereController : MonoBehaviour
    {
        [SerializeField] private Transform[] fogLayers;
        [SerializeField] private Transform[] motes;
        [SerializeField] private Vector2 fogDrift = new Vector2(0.12f, 0.015f);
        [SerializeField] private Vector2 moteDrift = new Vector2(0.04f, 0.08f);
        [SerializeField] private Vector2 boundsMin = new Vector2(-13f, -11f);
        [SerializeField] private Vector2 boundsMax = new Vector2(13f, 11f);
        [SerializeField, Range(0f, 0.5f)] private float fogPulse = 0.12f;
        [SerializeField, Range(0f, 0.8f)] private float motePulse = 0.35f;
        [SerializeField, Min(0.1f)] private float pulseSpeed = 0.75f;

        private Vector3[] _fogScales;
        private Vector3[] _moteScales;
        private SpriteRenderer[] _fogRenderers;
        private SpriteRenderer[] _moteRenderers;
        private Color[] _fogColors;
        private Color[] _moteColors;

        private void Awake()
        {
            Cache(fogLayers, out _fogScales, out _fogRenderers, out _fogColors);
            Cache(motes, out _moteScales, out _moteRenderers, out _moteColors);
        }

        private void Update()
        {
            float time = Time.time;
            Animate(fogLayers, _fogScales, _fogRenderers, _fogColors, fogDrift, fogPulse, time, true);
            Animate(motes, _moteScales, _moteRenderers, _moteColors, moteDrift, motePulse, time, false);
        }

        private void Animate(Transform[] targets, Vector3[] baseScales, SpriteRenderer[] renderers,
                             Color[] baseColors, Vector2 drift, float pulse, float time, bool horizontalWrap)
        {
            if (targets == null || baseScales == null) return;
            for (int i = 0; i < targets.Length; i++)
            {
                Transform target = targets[i];
                if (target == null) continue;
                float direction = i % 2 == 0 ? 1f : -1f;
                Vector3 position = target.localPosition;
                position += (Vector3)(drift * (direction * (0.65f + i % 3 * 0.18f)) * Time.deltaTime);
                if (horizontalWrap)
                {
                    if (position.x > boundsMax.x + 5f) position.x = boundsMin.x - 5f;
                    if (position.x < boundsMin.x - 5f) position.x = boundsMax.x + 5f;
                }
                else
                {
                    if (position.y > boundsMax.y) position.y = boundsMin.y;
                    if (position.y < boundsMin.y) position.y = boundsMax.y;
                    if (position.x > boundsMax.x) position.x = boundsMin.x;
                    if (position.x < boundsMin.x) position.x = boundsMax.x;
                }
                target.localPosition = position;

                float wave = Mathf.Sin(time * pulseSpeed * (1f + i % 4 * 0.11f) + i * 1.37f);
                target.localScale = baseScales[i] * (1f + wave * pulse * 0.18f);
                if (renderers[i] != null)
                {
                    Color color = baseColors[i];
                    color.a *= 1f + wave * pulse;
                    renderers[i].color = color;
                }
            }
        }

        private static void Cache(Transform[] targets, out Vector3[] scales, out SpriteRenderer[] renderers,
                                  out Color[] colors)
        {
            int count = targets?.Length ?? 0;
            scales = new Vector3[count];
            renderers = new SpriteRenderer[count];
            colors = new Color[count];
            for (int i = 0; i < count; i++)
            {
                if (targets[i] == null) continue;
                scales[i] = targets[i].localScale;
                renderers[i] = targets[i].GetComponent<SpriteRenderer>();
                colors[i] = renderers[i] != null ? renderers[i].color : Color.white;
            }
        }
    }
}
