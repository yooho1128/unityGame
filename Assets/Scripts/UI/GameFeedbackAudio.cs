using System.Collections.Generic;
using UnityEngine;

namespace ShadowTheater.UI
{
    public enum FeedbackCue { Confirm, Record, Denied, Heal, Escape, Faint, DialogueTick, PageTurn, Revelation }

    /// <summary>필드·전투 공통의 짧은 결과음. 효과음/마스터 볼륨 설정을 따른다.</summary>
    public static class GameFeedbackAudio
    {
        private static AudioSource _source;
        private static readonly Dictionary<FeedbackCue, AudioClip> Clips = new Dictionary<FeedbackCue, AudioClip>();
        private static float _nextConfirmAt;
        private static float _nextDialogueAt, _nextRevelationAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        { _source = null; Clips.Clear(); _nextConfirmAt = _nextDialogueAt = _nextRevelationAt = 0; }

        public static void Play(FeedbackCue cue)
        {
            if (!Application.isPlaying || GameSettings.SfxVolume <= 0) return;
            if (cue == FeedbackCue.Confirm && Time.unscaledTime < _nextConfirmAt) return;
            bool dialogueCue = cue == FeedbackCue.DialogueTick || cue == FeedbackCue.PageTurn || cue == FeedbackCue.Revelation;
            if (dialogueCue && !GameSettings.DialogueSounds) return;
            if (cue == FeedbackCue.DialogueTick && Time.unscaledTime < _nextDialogueAt) return;
            if (cue == FeedbackCue.Revelation && Time.unscaledTime < _nextRevelationAt) return;
            if (!Clips.TryGetValue(cue, out var clip))
            {
                clip = Resources.Load<AudioClip>("Audio/Feedback/" + cue);
                if (clip == null && dialogueCue) clip = CreateDialogueCue(cue);
                Clips[cue] = clip;
            }
            if (clip == null) return;
            if (_source == null)
            {
                var host = new GameObject("GameFeedbackAudio");
                Object.DontDestroyOnLoad(host);
                _source = host.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0;
                _source.priority = 64;
            }
            float level = cue == FeedbackCue.DialogueTick ? .12f : cue == FeedbackCue.PageTurn ? .22f
                : cue == FeedbackCue.Revelation ? .32f : cue == FeedbackCue.Confirm ? .35f : .65f;
            _source.PlayOneShot(clip, GameSettings.SfxVolume * level);
            if (cue == FeedbackCue.Confirm) _nextConfirmAt = Time.unscaledTime + .06f;
            if (cue == FeedbackCue.DialogueTick) _nextDialogueAt = Time.unscaledTime + .085f;
            if (cue == FeedbackCue.Revelation) _nextRevelationAt = Time.unscaledTime + 2f;
        }

        private static AudioClip CreateDialogueCue(FeedbackCue cue)
        {
            const int rate = 22050;
            float duration = cue == FeedbackCue.Revelation ? .85f : cue == FeedbackCue.PageTurn ? .16f : .035f;
            var samples = new float[Mathf.CeilToInt(rate * duration)];
            var noise = new System.Random(719);
            float filtered = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)rate, p = time / duration;
                float envelope = Mathf.Sin(Mathf.PI * p) * Mathf.Exp(-p * 3f);
                filtered = Mathf.Lerp(filtered, (float)noise.NextDouble() * 2f - 1f, .24f);
                float value = cue == FeedbackCue.PageTurn ? filtered * .7f
                    : cue == FeedbackCue.DialogueTick ? Mathf.Sin(time * 2f * Mathf.PI * 560f) * .4f
                    : (Mathf.Sin(time * 2f * Mathf.PI * 440f) + Mathf.Sin(time * 2f * Mathf.PI * 660f)
                        + Mathf.Sin(time * 2f * Mathf.PI * 880f)) * .19f;
                samples[i] = value * envelope;
            }
            var clip = AudioClip.Create("Story_" + cue, samples.Length, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }
    }
}
