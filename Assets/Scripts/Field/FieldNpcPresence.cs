using System.Collections;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>가까이 온 플레이어를 바라보고, 처음 발견했을 때 작은 몸짓을 보이는 NPC 생활감 레이어.</summary>
    [DisallowMultipleComponent]
    public sealed class FieldNpcPresence : MonoBehaviour
    {
        [SerializeField, Range(1f, 6f)] private float noticeRadius = 3.1f;
        [SerializeField, Range(.5f, 3f)] private float relaxRadiusBonus = 1.1f;

        private SpriteRenderer _renderer;
        private Transform _visual;
        private Vector3 _baseScale;
        private ShadowFieldMotion _motion;
        private bool _noticed;
        private float _noticeAt;
        private float _nextLookAt;
        private SpriteRenderer _emote;
        private TextMesh _speech;
        private TextMesh _speechShadow;
        private Coroutine _speechRoutine;

        public void PlaySocialGesture(Transform other)
        {
            if(other==null||_renderer==null)return;
            float dx=other.position.x-transform.position.x;
            if(Mathf.Abs(dx)>.08f)_renderer.flipX=dx<0f;
            _noticeAt=Time.time;_noticed=true;_motion?.PlayGesture(.48f);
        }

        public void ShowAmbientLine(string line, float duration = 2.1f)
        {
            if (string.IsNullOrWhiteSpace(line) || _speech == null) return;
            if (_speechRoutine != null) StopCoroutine(_speechRoutine);
            _speechRoutine = StartCoroutine(ShowLine(line, duration));
        }

        private void Awake()
        {
            _renderer = GetComponentInChildren<SpriteRenderer>();
            _visual = _renderer != null ? _renderer.transform : transform;
            _baseScale = _visual.localScale;
            _motion = GetComponentInChildren<ShadowFieldMotion>();
            var icon = new GameObject("NoticeEmote", typeof(SpriteRenderer));
            icon.transform.SetParent(transform, false); icon.transform.localPosition = new Vector3(0f,1.05f,0f);
            _emote = icon.GetComponent<SpriteRenderer>(); _emote.sprite = BattleFxSpriteLibrary.Get(BattleFxGlyph.Star);
            _emote.color = new Color(1f,.86f,.3f,0f); _emote.sortingOrder = 310;
            _speechShadow=CreateSpeech("AmbientSpeechShadow",new Vector3(.018f,1.37f,0f),
                new Color(.025f,.018f,.045f,0f),318);
            _speech=CreateSpeech("AmbientSpeech",new Vector3(0f,1.39f,0f),
                new Color(1f,.96f,.82f,0f),319);
        }

        private TextMesh CreateSpeech(string name,Vector3 localPosition,Color color,int order)
        {
            var go=new GameObject(name,typeof(TextMesh));go.transform.SetParent(transform,false);
            go.transform.localPosition=localPosition;
            TextMesh text=go.GetComponent<TextMesh>();text.anchor=TextAnchor.MiddleCenter;
            text.alignment=TextAlignment.Center;text.fontSize=42;text.characterSize=.045f;text.color=color;
            go.GetComponent<MeshRenderer>().sortingOrder=order;return text;
        }

        private IEnumerator ShowLine(string line,float duration)
        {
            _speech.text=line;_speechShadow.text=line;
            float fade=Mathf.Min(.22f,duration*.2f);
            for(float elapsed=0f;elapsed<duration;elapsed+=Time.unscaledDeltaTime)
            {
                float alpha=Mathf.Min(Mathf.InverseLerp(0f,fade,elapsed),
                    Mathf.InverseLerp(duration,duration-fade,elapsed));
                SetSpeechAlpha(alpha);
                float lift=Mathf.Sin(Mathf.Clamp01(elapsed/duration)*Mathf.PI)*.035f;
                _speech.transform.localPosition=new Vector3(0f,1.39f+lift,0f);
                _speechShadow.transform.localPosition=new Vector3(.018f,1.37f+lift,0f);
                yield return null;
            }
            SetSpeechAlpha(0f);_speech.text=string.Empty;_speechShadow.text=string.Empty;
            _speechRoutine=null;
        }

        private void SetSpeechAlpha(float alpha)
        {
            Color front=_speech.color;front.a=alpha;_speech.color=front;
            Color back=_speechShadow.color;back.a=alpha*.88f;_speechShadow.color=back;
        }

        private void Update()
        {
            PlayerController player = PlayerController.Instance;
            if (player == null || _renderer == null) return;
            float distance = Vector2.Distance(transform.position, player.transform.position);
            bool near = distance <= noticeRadius && !player.IsMoving;
            if (near && !_noticed)
            {
                _noticed = true; _noticeAt = Time.time; _nextLookAt = Time.time;
                _motion?.PlayGesture(.55f);
            }
            else if (_noticed && distance > noticeRadius + relaxRadiusBonus) _noticed = false;

            if (_noticed && Time.time >= _nextLookAt)
            {
                float dx = player.transform.position.x - transform.position.x;
                if (Mathf.Abs(dx) > .08f) _renderer.flipX = dx < 0f;
                _nextLookAt = Time.time + .2f;
            }

            float greeting = _noticed ? Mathf.Clamp01(1f - (Time.time - _noticeAt) / .55f) : 0f;
            float bounce = greeting * Mathf.Sin(Mathf.Clamp01((Time.time-_noticeAt)/.55f) * Mathf.PI);
            if (_emote != null)
            {
                Color c = _emote.color; c.a = greeting * .9f; _emote.color = c;
                _emote.transform.localScale = Vector3.one * (.12f + bounce * .13f);
            }
        }

        private void OnDisable()
        {
            if (_visual != null && _motion == null) _visual.localScale = _baseScale;
            if(_speechRoutine!=null)StopCoroutine(_speechRoutine);_speechRoutine=null;
            if(_speech!=null&&_speechShadow!=null){SetSpeechAlpha(0f);_speech.text=string.Empty;_speechShadow.text=string.Empty;}
        }
    }
}
