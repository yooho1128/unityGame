using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    public readonly struct WeatherEffectProfile
    {
        public readonly Color color;
        public readonly float rate, speed, size, lifetime, wind, volume;
        public WeatherEffectProfile(Color color, float rate, float speed, float size,
            float lifetime, float wind, float volume)
        {
            this.color = color; this.rate = rate; this.speed = speed; this.size = size;
            this.lifetime = lifetime; this.wind = wind; this.volume = volume;
        }
    }

    /// <summary>현재 지역 날씨를 카메라 추적 파티클과 낮은 합성 환경음 레이어로 표현한다.</summary>
    public class FieldWeatherEffects : MonoBehaviour
    {
        public const int SampleRate = 22050;
        [SerializeField] private string regionId;
        [SerializeField, Range(0f, 1f)] private float masterVolume = .16f;

        private ParticleSystem _particles;
        private ParticleSystemRenderer _particleRenderer;
        private Material _particleMaterial;
        private AudioSource _audio;
        private AudioClip _clip;
        private FieldWeather _weather = (FieldWeather)(-1);
        private float _weatherVolume;
        private Camera _camera;

        private void Start()
        {
            CreateRuntimeObjects();
            Refresh(true);
        }

        private void Update()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera != null && _particles != null)
                _particles.transform.position = _camera.transform.position + new Vector3(0f, 6f, 8f);
            Refresh(false);
            if (_audio != null) _audio.volume = masterVolume * _weatherVolume * GameSettings.AmbienceVolume;
        }

        private void OnDestroy()
        {
            if (_clip != null) Destroy(_clip);
            if (_particleMaterial != null) Destroy(_particleMaterial);
        }

        private void CreateRuntimeObjects()
        {
            var weatherObject = new GameObject("WeatherParticles", typeof(ParticleSystem));
            weatherObject.transform.SetParent(transform, false);
            _particles = weatherObject.GetComponent<ParticleSystem>();
            _particleRenderer = weatherObject.GetComponent<ParticleSystemRenderer>();
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _particleMaterial = new Material(shader) { name = "RuntimeWeatherParticle" };
                _particleRenderer.material = _particleMaterial;
            }
            _particleRenderer.sortingOrder = 80;

            var audioObject = new GameObject("WeatherAudio", typeof(AudioSource));
            audioObject.transform.SetParent(transform, false);
            _audio = audioObject.GetComponent<AudioSource>();
            _audio.loop = true; _audio.playOnAwake = false; _audio.spatialBlend = 0f;
        }

        private void Refresh(bool force)
        {
            RegionData region = RegionRepository.Get(regionId);
            FieldWeather next = FieldWorldState.Current(region).weather;
            if (!force && next == _weather) return;
            _weather = next;
            ConfigureParticles(Profile(next));
            ConfigureAudio(next);
        }

        private void ConfigureParticles(WeatherEffectProfile profile)
        {
            if (_particles == null) return;
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _particles.main;
            main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = profile.lifetime; main.startSpeed = 0f;
            main.startSize = profile.size; main.startColor = profile.color; main.maxParticles = 220;
            main.gravityModifier = 0f;
            var emission = _particles.emission; emission.rateOverTime = profile.rate;
            var shape = _particles.shape; shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(22f, 1f, 1f);
            var velocity = _particles.velocityOverLifetime;
            velocity.enabled = Mathf.Abs(profile.wind) > .01f || Mathf.Abs(profile.speed) > .01f;
            velocity.x = profile.wind; velocity.y = profile.speed;
            if (profile.rate > 0f) _particles.Play();
        }

        private void ConfigureAudio(FieldWeather weather)
        {
            if (_audio == null) return;
            _audio.Stop();
            if (_clip != null) Destroy(_clip);
            WeatherEffectProfile profile = Profile(weather);
            _weatherVolume = profile.volume;
            if (profile.volume <= 0f) { _clip = null; return; }
            float[] samples = SynthesizeSamples(weather);
            _clip = AudioClip.Create("Weather_" + weather, samples.Length, 1, SampleRate, false);
            _clip.SetData(samples, 0);
            _audio.clip = _clip;
            _audio.volume = masterVolume * profile.volume * GameSettings.AmbienceVolume;
            _audio.Play();
        }

        public static WeatherEffectProfile Profile(FieldWeather weather)
        {
            switch (weather)
            {
                case FieldWeather.Mist: return new WeatherEffectProfile(new Color(.8f,.84f,1f,.10f), 4f, -.15f, .8f, 7f, .12f, .25f);
                case FieldWeather.Rain: return new WeatherEffectProfile(new Color(.5f,.72f,1f,.58f), 48f, -7f, .055f, 2.2f, .35f, .75f);
                case FieldWeather.Ashfall: return new WeatherEffectProfile(new Color(.85f,.48f,.28f,.52f), 18f, -1.2f, .11f, 6f, .45f, .48f);
                case FieldWeather.Snow: return new WeatherEffectProfile(new Color(.9f,.96f,1f,.72f), 22f, -.85f, .14f, 8f, .5f, .38f);
                case FieldWeather.MoonWind: return new WeatherEffectProfile(new Color(.72f,.62f,1f,.38f), 12f, -.25f, .1f, 5f, 2.2f, .35f);
                case FieldWeather.ThreadDust: return new WeatherEffectProfile(new Color(1f,.58f,.76f,.46f), 15f, -.35f, .075f, 6f, 1.1f, .4f);
                case FieldWeather.InkStorm: return new WeatherEffectProfile(new Color(.25f,.12f,.4f,.68f), 34f, -2.4f, .16f, 4f, 1.8f, .82f);
                case FieldWeather.Starfall: return new WeatherEffectProfile(new Color(.7f,.9f,1f,.86f), 9f, -3.5f, .1f, 3.5f, -.7f, .45f);
                default: return new WeatherEffectProfile(Color.clear, 0f, 0f, 0f, 1f, 0f, 0f);
            }
        }

        public static float[] SynthesizeSamples(FieldWeather weather)
        {
            int count = SampleRate * 4;
            var samples = new float[count];
            uint noise = unchecked((uint)((int)weather + 1) * 2654435761u);
            float tone = weather == FieldWeather.InkStorm ? 46f : weather == FieldWeather.MoonWind ? 110f : 73f;
            for (int i = 0; i < count; i++)
            {
                noise = noise * 1664525u + 1013904223u;
                float hiss = ((noise >> 9) / 8388607f * 2f - 1f);
                float t = i / (float)SampleRate;
                float wave = Mathf.Sin(Mathf.PI * 2f * tone * t) * .18f;
                float mix = weather == FieldWeather.Rain || weather == FieldWeather.InkStorm
                    ? hiss * .34f + wave : hiss * .12f + wave;
                samples[i] = Mathf.Clamp(mix * Profile(weather).volume, -.7f, .7f);
            }
            return samples;
        }
    }
}
