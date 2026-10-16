using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
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
