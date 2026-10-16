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
    public enum FieldTimeOfDay { Dawn, Day, Evening, Night }
    public enum FieldWeather { Clear, Mist, Rain, Ashfall, Snow, MoonWind, ThreadDust, InkStorm, Starfall }

    public readonly struct FieldWorldSnapshot
    {
        public readonly FieldTimeOfDay timeOfDay;
        public readonly FieldWeather weather;
        public readonly int dayIndex;

        public FieldWorldSnapshot(FieldTimeOfDay timeOfDay, FieldWeather weather, int dayIndex)
        {
            this.timeOfDay = timeOfDay;
            this.weather = weather;
            this.dayIndex = dayIndex;
        }
    }

    /// <summary>현실 시계와 무관하게 누적 플레이 시간과 지역 ID로 재현 가능한 시간·날씨를 계산한다.</summary>
    public static class FieldWorldState
    {
        public const float SegmentSeconds = 360f;
        public const float DaySeconds = SegmentSeconds * 4f;

        public static FieldWorldSnapshot Current(RegionData region)
        {
            return At(region, SaveManager.Current?.playTimeSeconds ?? 0f);
        }

        public static FieldWorldSnapshot At(RegionData region, float playTimeSeconds)
        {
            float safeTime = Mathf.Max(0f, playTimeSeconds);
            int segment = Mathf.FloorToInt(safeTime / SegmentSeconds);
            int day = Mathf.FloorToInt(safeTime / DaySeconds);
            var time = (FieldTimeOfDay)(segment % 4);
            FieldWeather[] weather = WeatherTable(region?.act ?? 1);
            int seed = StableHash(region?.regionId ?? string.Empty);
            int index = PositiveModulo(seed + day, weather.Length);
            return new FieldWorldSnapshot(time, weather[index], day);
        }

        public static float EncounterWeightMultiplier(ShadowData shadow, FieldWorldSnapshot state)
        {
            if (shadow == null) return 0f;
            float value = 1f;
            switch (state.timeOfDay)
            {
                case FieldTimeOfDay.Dawn when shadow.element == ShadowElement.Frost: value *= 1.15f; break;
                case FieldTimeOfDay.Day when shadow.element == ShadowElement.None: value *= 1.12f; break;
                case FieldTimeOfDay.Evening when shadow.element == ShadowElement.Flame: value *= 1.15f; break;
                case FieldTimeOfDay.Night when shadow.element == ShadowElement.Shade: value *= 1.25f; break;
            }
            switch (state.weather)
            {
                case FieldWeather.Mist when shadow.element == ShadowElement.Shade: value *= 1.18f; break;
                case FieldWeather.Rain:
                    if (shadow.element == ShadowElement.Frost) value *= 1.2f;
                    else if (shadow.element == ShadowElement.Flame) value *= .9f;
                    break;
                case FieldWeather.Ashfall:
                    if (shadow.element == ShadowElement.Flame) value *= 1.25f;
                    else if (shadow.element == ShadowElement.Frost) value *= .85f;
                    break;
                case FieldWeather.Snow:
                    if (shadow.element == ShadowElement.Frost) value *= 1.25f;
                    else if (shadow.element == ShadowElement.Flame) value *= .85f;
                    break;
                case FieldWeather.MoonWind when shadow.element == ShadowElement.Shade: value *= 1.2f; break;
                case FieldWeather.ThreadDust when shadow.element == ShadowElement.None: value *= 1.18f; break;
                case FieldWeather.InkStorm:
                    if (shadow.element == ShadowElement.Shade) value *= 1.25f;
                    else if (shadow.element == ShadowElement.None) value *= .9f;
                    break;
                case FieldWeather.Starfall when shadow.growthTier == GrowthTier.Rare: value *= 1.15f; break;
            }
            return Mathf.Clamp(value, .6f, 1.6f);
        }

        public static float EncounterRateMultiplier(FieldWorldSnapshot state)
        {
            float value = state.timeOfDay == FieldTimeOfDay.Night ? 1.12f
                : state.timeOfDay == FieldTimeOfDay.Dawn ? .94f : 1f;
            if (state.weather == FieldWeather.Mist) value *= 1.15f;
            else if (state.weather == FieldWeather.InkStorm) value *= 1.25f;
            else if (state.weather == FieldWeather.Starfall) value *= 1.1f;
            else if (state.weather == FieldWeather.Clear) value *= .92f;
            return value;
        }

        public static float MovementDurationMultiplier(FieldWorldSnapshot state)
        {
            float value = state.timeOfDay == FieldTimeOfDay.Night ? 1.04f : 1f;
            if (state.weather == FieldWeather.Rain || state.weather == FieldWeather.Snow) value *= 1.12f;
            else if (state.weather == FieldWeather.InkStorm) value *= 1.16f;
            else if (state.weather == FieldWeather.MoonWind) value *= .92f;
            else if (state.weather == FieldWeather.ThreadDust) value *= .96f;
            return value;
        }

        public static float FieldHealingMultiplier(FieldWorldSnapshot state)
        {
            float value = state.timeOfDay == FieldTimeOfDay.Dawn ? 1.1f : 1f;
            if (state.weather == FieldWeather.Rain) value *= 1.05f;
            else if (state.weather == FieldWeather.Ashfall || state.weather == FieldWeather.InkStorm) value *= .9f;
            return value;
        }

        public static string RareEncounterSignal(FieldWorldSnapshot state)
        {
            string sky = state.timeOfDay == FieldTimeOfDay.Night ? "어둠 속에서" : "바람 너머에서";
            switch (state.weather)
            {
                case FieldWeather.Mist: return "기억 안개 속에서 낯선 숨결이 가까워진다…";
                case FieldWeather.Starfall: return "별비가 한 점에 모인다. 희귀한 그림자가 응답했다…";
                case FieldWeather.InkStorm: return "먹물 폭풍 사이로 지워지지 않는 형체가 드러난다…";
                default: return sky + " 좀처럼 마주치기 힘든 기척이 느껴진다…";
            }
        }

        public static string TimeLabel(FieldTimeOfDay value)
        {
            switch (value)
            {
                case FieldTimeOfDay.Dawn: return "새벽";
                case FieldTimeOfDay.Day: return "낮";
                case FieldTimeOfDay.Evening: return "해질녘";
                default: return "밤";
            }
        }

        public static string WeatherLabel(FieldWeather value)
        {
            switch (value)
            {
                case FieldWeather.Clear: return "맑음";
                case FieldWeather.Mist: return "기억 안개";
                case FieldWeather.Rain: return "잔향비";
                case FieldWeather.Ashfall: return "잿가루";
                case FieldWeather.Snow: return "서리눈";
                case FieldWeather.MoonWind: return "달바람";
                case FieldWeather.ThreadDust: return "실먼지";
                case FieldWeather.InkStorm: return "먹물 폭풍";
                default: return "별비";
            }
        }

        private static FieldWeather[] WeatherTable(int act)
        {
            switch (act)
            {
                case 2: return new[] { FieldWeather.Ashfall, FieldWeather.Clear, FieldWeather.Mist };
                case 3: return new[] { FieldWeather.Snow, FieldWeather.Mist, FieldWeather.Rain };
                case 4: return new[] { FieldWeather.MoonWind, FieldWeather.Mist, FieldWeather.Rain };
                case 5: return new[] { FieldWeather.ThreadDust, FieldWeather.Clear, FieldWeather.Mist };
                case 6: return new[] { FieldWeather.InkStorm, FieldWeather.Mist, FieldWeather.Clear };
                case 7: return new[] { FieldWeather.Rain, FieldWeather.Mist, FieldWeather.Starfall };
                case 8: return new[] { FieldWeather.Starfall, FieldWeather.Mist, FieldWeather.MoonWind };
                default: return new[] { FieldWeather.Clear, FieldWeather.Mist, FieldWeather.MoonWind };
            }
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in value ?? string.Empty) { hash ^= c; hash *= 16777619; }
                return (int)(hash & 0x7fffffff);
            }
        }

        private static int PositiveModulo(int value, int divisor) => (value % divisor + divisor) % divisor;
    }

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
