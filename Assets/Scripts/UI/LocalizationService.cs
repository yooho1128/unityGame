using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    [Serializable]
    public class LocalizationCatalogData
    {
        public List<LocalizedStringEntry> strings = new List<LocalizedStringEntry>();
    }

    [Serializable]
    public class LocalizedStringEntry
    {
        public string key;
        public string ko;
        public string en;
    }

    /// <summary>키 기반 번역과 기존 한국어 문자열 호환을 함께 제공하는 경량 런타임 저장소.</summary>
    public static class L10n
    {
        private const string ResourcePath = "Data/LocalizationCatalog";
        private static Dictionary<string, LocalizedStringEntry> _byKey;
        private static Dictionary<string, LocalizedStringEntry> _byText;
        public static event Action Changed;
        public static bool IsEnglish => GameSettings.Language == GameLanguage.English;

        public static string Get(string key, string fallback = "")
        {
            EnsureLoaded();
            if (!string.IsNullOrEmpty(key) && _byKey.TryGetValue(key, out var entry))
                return Pick(entry, fallback);
            return fallback ?? string.Empty;
        }

        public static string Text(string source)
        {
            if (string.IsNullOrEmpty(source)) return source ?? string.Empty;
            EnsureLoaded();
            return _byText.TryGetValue(source, out var entry) ? Pick(entry, source) : source;
        }

        public static string Format(string key, string fallback, params object[] args) =>
            string.Format(Get(key, fallback), args);

        public static void Reload()
        {
            _byKey = null;
            _byText = null;
            EnsureLoaded();
            Changed?.Invoke();
        }

        public static void NotifyLanguageChanged() => Changed?.Invoke();

        private static string Pick(LocalizedStringEntry entry, string fallback)
        {
            string value = IsEnglish ? entry.en : entry.ko;
            return string.IsNullOrEmpty(value) ? fallback ?? string.Empty : value;
        }

        private static void EnsureLoaded()
        {
            if (_byKey != null) return;
            _byKey = new Dictionary<string, LocalizedStringEntry>(StringComparer.Ordinal);
            _byText = new Dictionary<string, LocalizedStringEntry>(StringComparer.Ordinal);
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) { Debug.LogError($"[Localization] Resources/{ResourcePath}.json 누락"); return; }
            LocalizationCatalogData catalog;
            try { catalog = JsonUtility.FromJson<LocalizationCatalogData>(asset.text); }
            catch (Exception e) { Debug.LogError($"[Localization] JSON 파싱 실패: {e.Message}"); return; }
            if (catalog?.strings == null) return;
            foreach (var entry in catalog.strings)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;
                _byKey[entry.key] = entry;
                if (!string.IsNullOrEmpty(entry.ko)) _byText[entry.ko] = entry;
                if (!string.IsNullOrEmpty(entry.en)) _byText[entry.en] = entry;
            }
        }
    }

    /// <summary>씬의 정적 UI 문자열과 언어별 OS 폰트를 즉시 교체한다.</summary>
    public class LocalizationRuntime : MonoBehaviour
    {
        private static Font _koreanFont;
        private static Font _englishFont;

        private void OnEnable()
        {
            GameSettings.Changed += Refresh;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Refresh();
        }

        private void OnDisable()
        {
            GameSettings.Changed -= Refresh;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private IEnumerator Start()
        {
            // 생성형 씬은 CoreSystems 뒤에 UI 프리팹을 인스턴스화하므로 한 프레임 뒤 다시 적용한다.
            yield return null;
            Refresh();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => StartCoroutine(RefreshNextFrame());

        private IEnumerator RefreshNextFrame()
        {
            yield return null;
            Refresh();
        }

        public void Refresh()
        {
            L10n.NotifyLanguageChanged();
            Font font = ResolveFont(L10n.IsEnglish);
            foreach (Text label in FindObjectsOfType<Text>(true))
            {
                if (label == null) continue;
                label.text = L10n.Text(label.text);
                if (font != null) label.font = font;
            }
        }

        private static Font ResolveFont(bool english)
        {
            if (english && _englishFont != null) return _englishFont;
            if (!english && _koreanFont != null) return _koreanFont;
            string[] preferred = english
                ? new[] { "Arial", "Roboto", "Liberation Sans" }
                : new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR" };
            var installed = new HashSet<string>(Font.GetOSInstalledFontNames(), StringComparer.OrdinalIgnoreCase);
            foreach (string name in preferred)
                if (installed.Contains(name))
                {
                    var created = Font.CreateDynamicFontFromOSFont(name, 24);
                    if (english) _englishFont = created; else _koreanFont = created;
                    return created;
                }
            var fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (english) _englishFont = fallback; else _koreanFont = fallback;
            return fallback;
        }
    }
}
