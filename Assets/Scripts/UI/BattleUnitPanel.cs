using ShadowTheater.Battle;
using ShadowTheater.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    public class BattleUnitPanel : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private Image accent;
        [SerializeField] private Image hpFill;
        [SerializeField] private Text nameText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text elementText;
        [SerializeField] private Text hpText;
        [SerializeField] private Text statusText;

        private Vector3 _restPosition;
        private Vector3 _portraitPosition;
        private Vector3 _portraitScale;
        private Quaternion _portraitRotation;
        private ShadowRole _motionRole;
        private float _motionAmount = 0.025f;
        private float _motionPhase;
        private BattleUnit _boundUnit;
        private float _displayedHp, _healthStart, _healthElapsed, _healthDuration;
        private int _healthTarget, _healthMax = 1;
        public float DisplayedHp => _displayedHp;
        public bool Represents(BattleUnit unit) => _boundUnit == unit;
        private RectTransform _actorMotion;
        public RectTransform MotionRoot
        {
            get
            {
                if (_actorMotion == null && portrait != null)
                {
                    var source = portrait.rectTransform;
                    Vector2 min = source.anchorMin, max = source.anchorMax, size = source.sizeDelta;
                    Vector3 anchored = source.anchoredPosition3D, scale = source.localScale;
                    Quaternion rotation = source.localRotation;
                    _actorMotion = new GameObject("ActorMotion", typeof(RectTransform)).GetComponent<RectTransform>();
                    _actorMotion.SetParent(source.parent, false);
                    _actorMotion.SetSiblingIndex(source.GetSiblingIndex());
                    _actorMotion.anchorMin = Vector2.zero; _actorMotion.anchorMax = Vector2.one;
                    _actorMotion.offsetMin = _actorMotion.offsetMax = Vector2.zero;
                    source.SetParent(_actorMotion, false);
                    source.anchorMin = min; source.anchorMax = max; source.sizeDelta = size;
                    source.anchoredPosition3D = anchored; source.localScale = scale; source.localRotation = rotation;
                    _restPosition = _actorMotion.localPosition;
                    _portraitPosition = source.anchoredPosition3D;
                    _portraitScale = source.localScale;
                    _portraitRotation = source.localRotation;
                }
                return _actorMotion != null ? _actorMotion : (RectTransform)transform;
            }
        }
        public Image Portrait => portrait;
        public ShadowRole MotionRole => _motionRole;

        private void Awake()
        {
            if (hpText != null)
            {
                var rect = hpText.rectTransform;
                rect.anchorMin = new Vector2(.03f,.18f); rect.anchorMax = new Vector2(.97f,.40f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            if (statusText != null)
            {
                var rect = statusText.rectTransform;
                rect.anchorMin = new Vector2(.03f,.02f); rect.anchorMax = new Vector2(.97f,.17f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            _restPosition = MotionRoot.localPosition;
            if (portrait != null)
            {
                _portraitPosition = portrait.rectTransform.anchoredPosition3D;
                _portraitScale = portrait.rectTransform.localScale;
                _portraitRotation = portrait.rectTransform.localRotation;
            }
            _motionPhase = (GetInstanceID() & 15) * 0.41f;
        }
        public void ResetMotion() => MotionRoot.localPosition = _restPosition;

        public void Bind(BattleUnit unit, float healthDuration = .28f)
        {
            if (unit == null) return;
            _motionRole = unit.Data.role;
            _motionAmount = (int)unit.Data.growthTier >= (int)GrowthTier.RegionalBoss ? 0.045f : 0.025f;
            if (portrait != null)
            {
                portrait.sprite = unit.Instance.Silhouette;
                portrait.color = ShadowPortraitStyle.Tint(portrait.sprite);
                portrait.preserveAspect = true;
            }
            if (accent != null) accent.color = unit.Instance.AccentColor;
            if (_boundUnit != unit)
                _displayedHp = _healthStart = _healthTarget = unit.Hp;
            if (_boundUnit != unit || _healthTarget != unit.Hp || _healthMax != unit.MaxHp)
            {
                _healthStart = _displayedHp; _healthTarget = unit.Hp; _healthMax = Mathf.Max(1, unit.MaxHp);
                _healthElapsed = 0f; _healthDuration = Mathf.Max(.01f, healthDuration);
            }
            _boundUnit = unit;
            AdvanceHealthPresentation(0f);
            if (nameText != null) nameText.text = StarVariantPolicy.Decorate(L10n.Text(unit.Name), unit.Instance);
            if (levelText != null) levelText.text = $"Lv.{unit.Level}";
            if (elementText != null)
            {
                elementText.text = L10n.Get("element." + unit.Data.element.ToString().ToLowerInvariant(), unit.Data.element.ToString());
                elementText.color = ElementColor(unit.Data.element);
            }
            if (statusText != null)
            {
                statusText.text = BattleStatusFormatter.Summary(unit);
                statusText.color = unit.HasAnyStatus ? new Color(1f,.66f,.30f) : new Color(.75f,.72f,.82f);
            }
        }

        public void AdvanceHealthPresentation(float deltaTime)
        {
            if (_boundUnit == null) return;
            _healthElapsed += Mathf.Max(0f, deltaTime);
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_healthElapsed / _healthDuration));
            _displayedHp = Mathf.Lerp(_healthStart, _healthTarget, p);
            float ratio = Mathf.Clamp01(_displayedHp / _healthMax);
            if (hpFill != null)
            {
                hpFill.fillAmount = ratio;
                hpFill.color = ratio > .5f ? new Color(.35f,.92f,.65f) : ratio > .2f
                    ? new Color(1f,.72f,.25f) : new Color(1f,.28f,.36f);
            }
            if (hpText != null) hpText.text = $"{Mathf.Clamp(Mathf.RoundToInt(_displayedHp), 0, _healthMax)} / {_healthMax}";
        }

        private static Color ElementColor(ShadowElement element) => ElementChart.ColorFor(element);

        private void LateUpdate()
        {
            AdvanceHealthPresentation(Time.unscaledDeltaTime);
            if (portrait == null || !portrait.enabled) return;
            float speed = _motionRole == ShadowRole.SpeedUtility ? 5.5f
                : _motionRole == ShadowRole.MagicNuker || _motionRole == ShadowRole.Support ? 2.2f : 3.6f;
            float wave = Mathf.Sin((Time.unscaledTime + _motionPhase) * speed);
            float rise = (_motionRole == ShadowRole.MagicNuker || _motionRole == ShadowRole.Support)
                ? wave * 5f : Mathf.Abs(wave) * 2f;
            portrait.rectTransform.anchoredPosition3D = _portraitPosition + Vector3.up * rise;
            portrait.rectTransform.localScale = Vector3.Scale(_portraitScale,
                new Vector3(1f - wave * _motionAmount * .25f, 1f + wave * _motionAmount, 1f));
            float tilt = _motionRole == ShadowRole.SpeedUtility ? wave * 2.5f : wave * .7f;
            portrait.rectTransform.localRotation = _portraitRotation * Quaternion.Euler(0f, 0f, tilt);
        }

    }
}
