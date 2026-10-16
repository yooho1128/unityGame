using System;
using System.Collections.Generic;
using ShadowTheater.Data;
using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using ShadowData = ShadowTheater.Data.ShadowData;

namespace ShadowTheater.Field
{
    /// <summary>시간대·날씨가 바뀔 때 필드 2D 조명의 색과 밝기를 갱신한다.</summary>
    public class FieldWorldStateController : MonoBehaviour
    {
        [SerializeField] private string regionId;
        [SerializeField, Min(.2f)] private float refreshInterval = 2f;

        private readonly List<LightBaseline> _lights = new List<LightBaseline>();
        private FieldWorldSnapshot _state;
        private bool _hasState;
        private float _nextRefresh;

        private void Start()
        {
            foreach (Light2D light in GetComponentsInChildren<Light2D>(true))
                _lights.Add(new LightBaseline(light, light.color, light.intensity));
            Refresh(true);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            Refresh(false);
        }

        private void Refresh(bool force)
        {
            _nextRefresh = Time.unscaledTime + refreshInterval;
            RegionData region = RegionRepository.Get(regionId);
            if (region == null) return;
            FieldWorldSnapshot next = FieldWorldState.Current(region);
            if (!force && _hasState && next.timeOfDay == _state.timeOfDay && next.weather == _state.weather) return;
            _state = next;
            _hasState = true;
            ApplyLighting(next);
        }

        private void ApplyLighting(FieldWorldSnapshot state)
        {
            Color timeTint;
            float intensity;
            switch (state.timeOfDay)
            {
                case FieldTimeOfDay.Dawn: timeTint = new Color(1f, .77f, .68f); intensity = .88f; break;
                case FieldTimeOfDay.Day: timeTint = Color.white; intensity = 1f; break;
                case FieldTimeOfDay.Evening: timeTint = new Color(.88f, .62f, .76f); intensity = .82f; break;
                default: timeTint = new Color(.48f, .58f, .92f); intensity = .68f; break;
            }
            Color weatherTint = WeatherTint(state.weather);
            float weatherIntensity = state.weather == FieldWeather.Mist || state.weather == FieldWeather.InkStorm ? .88f : 1f;
            foreach (LightBaseline baseline in _lights)
            {
                if (baseline.light == null) continue;
                baseline.light.color = Color.Lerp(baseline.color, timeTint * weatherTint, .28f);
                baseline.light.intensity = baseline.intensity * intensity * weatherIntensity;
            }
        }

        private static Color WeatherTint(FieldWeather weather)
        {
            switch (weather)
            {
                case FieldWeather.Ashfall: return new Color(1f, .7f, .52f);
                case FieldWeather.Snow: return new Color(.72f, .88f, 1f);
                case FieldWeather.Rain: return new Color(.66f, .78f, .95f);
                case FieldWeather.MoonWind: return new Color(.76f, .68f, 1f);
                case FieldWeather.ThreadDust: return new Color(1f, .78f, .86f);
                case FieldWeather.InkStorm: return new Color(.58f, .48f, .72f);
                case FieldWeather.Starfall: return new Color(.72f, .86f, 1f);
                default: return Color.white;
            }
        }

        [Serializable]
        private sealed class LightBaseline
        {
            public readonly Light2D light;
            public readonly Color color;
            public readonly float intensity;
            public LightBaseline(Light2D light, Color color, float intensity)
            { this.light = light; this.color = color; this.intensity = intensity; }
        }
    }
}
