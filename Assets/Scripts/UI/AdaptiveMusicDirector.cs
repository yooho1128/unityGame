using System.Collections;
using ShadowTheater.Battle;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowTheater.UI
{
    public enum MusicCue
    {
        Title, Act1, Act2, Act3, Act4, Act5, Act6, Act7, Act8,
        WildBattle, RivalBattle, BossBattle, Ending
    }

    /// <summary>필드·전투·엔딩 음악을 두 채널 사이에서 끊김 없이 교차 전환한다.</summary>
    public class AdaptiveMusicDirector : MonoBehaviour
    {
        public const int SampleRate = 22050;
        public static AdaptiveMusicDirector Instance { get; private set; }

        [SerializeField] private AudioClip[] clips;
        [SerializeField, Range(0f, 1f)] private float volume = .42f;
        [SerializeField, Min(.1f)] private float crossfadeDuration = .8f;

        private AudioSource _active;
        private AudioSource _standby;
        private Coroutine _transition;
        private MusicCue _current;
        private bool _hasCue;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _active = CreateSource("MusicA");
            _standby = CreateSource("MusicB");
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            GameSettings.Changed += RefreshVolume;
        }

        private void Start() => Play(FieldCue(SceneManager.GetActiveScene().name), true);

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameSettings.Changed -= RefreshVolume;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void EnterBattle(BattleMode mode)
        {
            Play(mode == BattleMode.Boss ? MusicCue.BossBattle :
                mode == BattleMode.Rival ? MusicCue.RivalBattle : MusicCue.WildBattle);
        }

        public void ReturnToField() => Play(FieldCue(SceneManager.GetActiveScene().name));
        public void PlayEnding() => Play(MusicCue.Ending, false);

        private void OnSceneLoaded(Scene scene, LoadSceneMode _) => Play(FieldCue(scene.name));

        private MusicCue FieldCue(string sceneName)
        {
            if (sceneName == "Title") return MusicCue.Title;
            RegionData region = RegionRepository.GetByScene(sceneName);
            int act = region != null ? Mathf.Clamp(region.act, 1, 8) : 1;
            return (MusicCue)((int)MusicCue.Act1 + act - 1);
        }

        private void Play(MusicCue cue, bool immediate = false)
        {
            if (_hasCue && _current == cue && _active != null && _active.isPlaying) return;
            _current = cue;
            _hasCue = true;
            AudioClip clip = Resolve(cue);
            if (clip == null) return;
            if (_transition != null) StopCoroutine(_transition);
            _transition = StartCoroutine(Crossfade(clip, immediate ? 0f : crossfadeDuration));
        }

        private IEnumerator Crossfade(AudioClip clip, float duration)
        {
            _standby.clip = clip;
            _standby.volume = 0f;
            _standby.Play();
            float start = _active != null ? _active.volume : 0f;
            float target = volume * GameSettings.MusicVolume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / duration);
                _active.volume = Mathf.Lerp(start, 0f, p);
                _standby.volume = Mathf.Lerp(0f, target, p);
                yield return null;
            }
            _active.Stop();
            _active.volume = 0f;
            _standby.volume = target;
            AudioSource swap = _active; _active = _standby; _standby = swap;
            _transition = null;
        }

        private void RefreshVolume()
        {
            if (_active != null) _active.volume = volume * GameSettings.MusicVolume;
        }

        private AudioClip Resolve(MusicCue cue)
        {
            int index = (int)cue;
            if (clips != null && index >= 0 && index < clips.Length && clips[index] != null) return clips[index];
            float[] samples = SynthesizeSamples(cue);
            var generated = AudioClip.Create("GeneratedMusic_" + cue, samples.Length, 1, SampleRate, false);
            generated.SetData(samples, 0);
            return generated;
        }

        private AudioSource CreateSource(string sourceName)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            return source;
        }

        public static float[] SynthesizeSamples(MusicCue cue)
        {
            const float duration = 8f;
            int count = Mathf.RoundToInt(SampleRate * duration);
            var samples = new float[count];
            int root = RootCycle(cue);
            int third = Mathf.RoundToInt(root * (cue == MusicCue.Act2 || cue == MusicCue.BossBattle ? 1.25f : 1.2f));
            int fifth = Mathf.RoundToInt(root * 1.5f);
            for (int i = 0; i < count; i++)
            {
                float p = i / (float)count;
                float pulse = .58f + .42f * Mathf.Sin(Mathf.PI * 2f * PulseCycle(cue) * p);
                float melody = Mathf.Sin(Mathf.PI * 2f * root * p) * .42f +
                               Mathf.Sin(Mathf.PI * 2f * third * p) * .23f +
                               Mathf.Sin(Mathf.PI * 2f * fifth * p) * .18f;
                float bass = Mathf.Sin(Mathf.PI * 2f * Mathf.Max(4, root / 2) * p) * .28f;
                samples[i] = Mathf.Clamp((melody * pulse + bass) * .52f, -.82f, .82f);
            }
            return samples;
        }

        private static int RootCycle(MusicCue cue)
        {
            switch (cue)
            {
                case MusicCue.Title: return 22;
                case MusicCue.Act1: return 28;
                case MusicCue.Act2: return 26;
                case MusicCue.Act3: return 35;
                case MusicCue.Act4: return 31;
                case MusicCue.Act5: return 33;
                case MusicCue.Act6: return 18;
                case MusicCue.Act7: return 30;
                case MusicCue.Act8: return 20;
                case MusicCue.WildBattle: return 44;
                case MusicCue.RivalBattle: return 39;
                case MusicCue.BossBattle: return 16;
                default: return 24;
            }
        }

        private static int PulseCycle(MusicCue cue) =>
            cue == MusicCue.BossBattle ? 32 : cue == MusicCue.WildBattle || cue == MusicCue.RivalBattle ? 24 : 8;
    }
}
