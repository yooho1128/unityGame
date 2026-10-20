using System;
using System.Collections.Generic;
using ShadowTheater.Field;
using UnityEngine;

namespace ShadowTheater.Data
{
    /// <summary>기존 생성 데이터·씬을 덮어쓰지 않고 종별 출시 아트를 연결한다.</summary>
    public class ShadowArtRoster : ScriptableObject
    {
        [Serializable] public class Binding { public string shadowId; public Sprite portrait; }
        public List<Binding> entries = new List<Binding>();
        private static ShadowArtRoster _current;
        private Dictionary<string, Sprite> _images;

        public Sprite Portrait(string id)
        {
            if (_images == null)
            {
                _images = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                foreach (var entry in entries)
                    if (entry != null && !string.IsNullOrEmpty(entry.shadowId) && entry.portrait != null)
                        _images[entry.shadowId] = entry.portrait;
            }
            return id != null && _images.TryGetValue(id, out var image) ? image : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnLoad()
        {
            _current = Resources.Load<ShadowArtRoster>("ShadowArtRoster");
            if (_current == null || ShadowDatabase.Instance == null) return;
            _current._images = null;
            foreach (var entry in _current.entries)
            {
                var shadow = ShadowDatabase.Instance.GetShadow(entry.shadowId);
                if (shadow == null || entry.portrait == null) continue;
                shadow.silhouetteSprite = entry.portrait;
            }
        }

        public static void ApplyFieldVisual(SpriteRenderer renderer, ShadowData shadow)
        {
            if (renderer == null || shadow == null) return;
            if (_current == null) _current = Resources.Load<ShadowArtRoster>("ShadowArtRoster");
            Sprite image = shadow.fieldFrames != null && shadow.fieldFrames.Length > 0
                ? shadow.fieldFrames[0] : null;
            if (image == null) return;
            renderer.sprite = image;
            FieldSpriteSizing.Fit(renderer, .95f);
            var motion = renderer.GetComponent<ShadowFieldMotion>();
            if (motion != null) motion.Configure(shadow, shadow.fieldFrames);
        }
    }
}
