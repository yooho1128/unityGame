using System.Collections.Generic;
using UnityEngine;

namespace ShadowTheater.UI
{
    public enum FeedbackCue { Confirm, Record, Denied, Heal, Escape, Faint }

    /// <summary>필드·전투 공통의 짧은 결과음. 효과음/마스터 볼륨 설정을 따른다.</summary>
    public static class GameFeedbackAudio
    {
        private static AudioSource _source;
        private static readonly Dictionary<FeedbackCue, AudioClip> Clips = new Dictionary<FeedbackCue, AudioClip>();
        private static float _nextConfirmAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        { _source = null; Clips.Clear(); _nextConfirmAt = 0; }

        public static void Play(FeedbackCue cue)
        {
            if (!Application.isPlaying || GameSettings.SfxVolume <= 0) return;
            if (cue == FeedbackCue.Confirm && Time.unscaledTime < _nextConfirmAt) return;
            if (!Clips.TryGetValue(cue, out var clip))
                Clips[cue] = clip = Resources.Load<AudioClip>("Audio/Feedback/" + cue);
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
            _source.PlayOneShot(clip, GameSettings.SfxVolume * (cue == FeedbackCue.Confirm ? .35f : .65f));
            if (cue == FeedbackCue.Confirm) _nextConfirmAt = Time.unscaledTime + .06f;
        }
    }
}
