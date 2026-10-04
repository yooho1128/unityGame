using ShadowTheater.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>에디터가 만든 Canvas 파티클 프리팹을 진명 테마별 궤적으로 재생한다.</summary>
    public class UltimateFxAssetPlayer : MonoBehaviour
    {
        [SerializeField] private UltimateFxStyle style;
        [SerializeField] private RectTransform[] particles;
        private Vector2[] _positions;
        private Quaternion[] _rotations;
        private Vector3[] _scales;

        public void Setup(UltimateFxStyle value, RectTransform[] values)
        {
            style = value;
            particles = values;
        }

        public void Initialize(Color primary, Color secondary, int burstCount)
        {
            if (particles == null) return;
            _positions = new Vector2[particles.Length];
            _rotations = new Quaternion[particles.Length];
            _scales = new Vector3[particles.Length];
            int visible = Mathf.Clamp(burstCount, 6, particles.Length);
            for (int i = 0; i < particles.Length; i++)
            {
                var particle = particles[i];
                if (particle == null) continue;
                particle.gameObject.SetActive(i < visible);
                _positions[i] = particle.anchoredPosition;
                _rotations[i] = particle.localRotation;
                _scales[i] = particle.localScale;
                var image = particle.GetComponent<Image>();
                if (image != null)
                {
                    Color color = i % 2 == 0 ? primary : secondary;
                    color.a = .82f;
                    image.color = color;
                }
            }
        }

        public void SetProgress(float value)
        {
            if (particles == null || _positions == null) return;
            float p = Mathf.Clamp01(value);
            float eased = 1f - (1f - p) * (1f - p);
            for (int i = 0; i < particles.Length; i++)
            {
                RectTransform r = particles[i];
                if (r == null || !r.gameObject.activeSelf) continue;
                float phase = i * .618f;
                float wave = Mathf.Sin(p * Mathf.PI * 2f + phase);
                switch (style)
                {
                    case UltimateFxStyle.FlameCrown:
                        r.anchoredPosition = _positions[i] + Vector2.up * (eased * (90f + i % 5 * 18f));
                        r.localScale = _scales[i] * (.15f + eased * (1f + .25f * wave));
                        break;
                    case UltimateFxStyle.FrozenArchive:
                        r.anchoredPosition = Vector2.Lerp(_positions[i] * 1.45f, _positions[i], eased);
                        r.localRotation = _rotations[i] * Quaternion.Euler(0f, 0f, wave * 28f);
                        r.localScale = _scales[i] * (.25f + eased);
                        break;
                    case UltimateFxStyle.MoonBeast:
                        r.anchoredPosition = _positions[i] * (1f + .08f * wave);
                        r.localScale = _scales[i] * (.2f + eased * (1f + .18f * wave));
                        break;
                    case UltimateFxStyle.PuppetThreads:
                        r.localScale = new Vector3(_scales[i].x, Mathf.Max(.01f, eased), 1f);
                        r.anchoredPosition = _positions[i] + Vector2.up * wave * 18f;
                        break;
                    case UltimateFxStyle.AshBird:
                        r.anchoredPosition = Vector2.Lerp(new Vector2(0f, -250f), _positions[i], eased);
                        r.localRotation = _rotations[i] * Quaternion.Euler(0f, 0f, wave * 16f);
                        r.localScale = _scales[i] * (.2f + eased);
                        break;
                    case UltimateFxStyle.FrozenMask:
                        r.anchoredPosition = Vector2.Lerp(new Vector2(Mathf.Sign(_positions[i].x) * 520f,
                            _positions[i].y), _positions[i], eased);
                        r.localScale = _scales[i] * (.3f + eased * .7f);
                        break;
                    case UltimateFxStyle.MoonPetals:
                    {
                        float angle = phase + p * 3.4f;
                        Vector3 rotated = Quaternion.Euler(0f, 0f, p * 160f) * (Vector3)_positions[i];
                        r.anchoredPosition = new Vector2(rotated.x, rotated.y) * (.4f + .6f * eased);
                        r.localRotation = _rotations[i] * Quaternion.Euler(0f, 0f, angle * 30f);
                        r.localScale = _scales[i] * (.2f + eased);
                        break;
                    }
                    case UltimateFxStyle.LivingScript:
                        r.anchoredPosition = new Vector2(Mathf.Lerp(-620f, _positions[i].x, eased), _positions[i].y + wave * 6f);
                        r.localScale = new Vector3(eased, _scales[i].y, 1f);
                        break;
                    case UltimateFxStyle.CosmicAudience:
                        r.localScale = _scales[i] * (.15f + eased * (1f + Mathf.Max(0f, wave) * .45f));
                        r.anchoredPosition = _positions[i] + Vector2.up * Mathf.Max(0f, wave) * 22f;
                        break;
                }
            }
        }
    }
}
