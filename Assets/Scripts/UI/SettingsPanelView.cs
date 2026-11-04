using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>Tabbed settings presentation using native navigable uGUI controls and the existing preferences.</summary>
    public sealed class SettingsPanelView : MonoBehaviour
    {
        private Font _font;
        private SettingsPanelController _owner;
        private readonly List<Button> _tabs = new List<Button>();
        private readonly List<ScrollRect> _pages = new List<ScrollRect>();
        private readonly List<Action> _refresh = new List<Action>();
        private readonly List<Action<float>> _animate = new List<Action<float>>();
        private readonly List<Action> _localize = new List<Action>();
        private int _selected;
        private GameObject _lastFocus;
        private bool _built;
        private bool _dropdownWasExpanded;
        private CanvasGroup _entryFade;
        private float _entryTime;

        public void Build(SettingsPanelController owner, Font font)
        {
            if (_built || font == null) return;
            _built = true; _owner = owner; _font = font;
            // Keep serialized controls intact for old prefabs and editor validation.
            foreach (Transform child in transform) child.gameObject.SetActive(false);
            var backdrop = GetComponent<Image>();
            if (backdrop != null) backdrop.color = TheatreUiStyle.Midnight;
            var shell = RuntimeUiControls.Rect(transform, "SettingsCard", new Vector2(.055f, .045f), new Vector2(.945f, .955f));
            TheatreUiStyle.Panel(shell.gameObject.AddComponent<Image>(), TheatreUiStyle.Surface);
            _entryFade = shell.gameObject.AddComponent<CanvasGroup>();
            _entryFade.alpha = 0f;
            var title = Label(shell, "settings.heading", "설정", 34, TheatreUiStyle.Ivory);
            Top(title.rectTransform, 28, 24, 160, 44);
            var hint = Label(shell, "settings.saved_hint", "변경 사항은 자동으로 저장됩니다", 17, TheatreUiStyle.Muted);
            StretchTop(hint.rectTransform, 28, 78, 28, 28);
            var tabRow = RuntimeUiControls.Rect(shell, "Tabs", Vector2.zero, Vector2.one);
            StretchTop(tabRow, 28, 126, 28, 48);
            AddTab(tabRow, shell, "settings.audio_tab", "사운드");
            if (!Application.isMobilePlatform) AddTab(tabRow, shell, "settings.display_tab", "화면");
            AddTab(tabRow, shell, "settings.play_tab", "플레이");
            for (int i = 0; i < _tabs.Count; i++)
                RuntimeUiControls.Place((RectTransform)_tabs[i].transform,
                    new Vector2((float)i / _tabs.Count + .006f, 0), new Vector2((float)(i + 1) / _tabs.Count - .006f, 1));

            Transform audio = _pages[0].content;
            AudioRow(audio, "settings.master", "전체 음량", () => GameSettings.MasterVolume, owner.SetMaster);
            AudioRow(audio, "settings.music", "음악", () => GameSettings.MusicVolume, owner.SetMusic);
            AudioRow(audio, "settings.ambience", "환경음", () => GameSettings.AmbienceVolume, owner.SetAmbience);
            AudioRow(audio, "settings.sfx", "효과음", () => GameSettings.SfxVolume, owner.SetSfx);
            if (!Application.isMobilePlatform)
            {
                Transform display = _pages[1].content;
                Segments(display, "settings.screen_mode", "화면 모드",
                    new[] { "settings.fullscreen", "settings.windowed" }, new[] { "전체 화면", "창 모드" },
                    () => GameSettings.Fullscreen ? 0 : 1,
                    index => { if ((index == 0) != GameSettings.Fullscreen) owner.ToggleFullscreen(); });
                var row = Row(display, "settings.resolution", "해상도");
                var options = new List<string>();
                for (int i = 0; i < GameSettings.ResolutionCount; i++) options.Add(GameSettings.GetResolutionLabel(i));
                var dropdown = RuntimeUiControls.Dropdown(row, _font, options, new Vector2(.04f, .13f), new Vector2(.96f, .53f));
                TheatreUiStyle.SurfaceImage(dropdown.GetComponent<Image>(), TheatreUiStyle.Raised);
                dropdown.onValueChanged.AddListener(index => GameSettings.Instance?.SetResolution(index));
                _refresh.Add(() => dropdown.SetValueWithoutNotify(GameSettings.ResolutionIndex));
                Switch(display, "settings.vsync", "수직 동기화", () => GameSettings.VSync, owner.ToggleVSync);
            }
            Transform play = _pages[_pages.Count - 1].content;
            Switch(play, "settings.dialogue_sounds", "대화 효과음", () => GameSettings.DialogueSounds,
                () => GameSettings.Instance?.SetDialogueSounds(!GameSettings.DialogueSounds));
            Switch(play, "settings.reduced_motion", "연출 움직임 줄이기", () => GameSettings.ReducedMotion,
                () => GameSettings.Instance?.SetReducedMotion(!GameSettings.ReducedMotion));
            Segments(play, "settings.text_speed", "대화 속도",
                new[] { "settings.slow", "settings.normal", "settings.fast" }, new[] { "느리게", "보통", "빠르게" },
                () => GameSettings.TextSpeed < 33f ? 0 : GameSettings.TextSpeed < 60f ? 1 : 2,
                index => GameSettings.Instance?.SetTextSpeed(index == 0 ? 24f : index == 1 ? 42f : 72f));
            if (Application.isMobilePlatform)
                Switch(play, "settings.vibration", "진동", () => GameSettings.Vibration, owner.ToggleVibration);
            if (GameSettings.EnglishEnabled)
                Segments(play, "settings.language", "언어", new[] { "settings.korean", "settings.english" },
                    new[] { "한국어", "English" }, () => GameSettings.Language == GameLanguage.Korean ? 0 : 1,
                    index => GameSettings.Instance?.SetLanguage(index == 0 ? GameLanguage.Korean : GameLanguage.English));
            var back = RuntimeUiControls.Button(shell, _font, "돌아가기", new Vector2(.04f, .025f), new Vector2(.96f, .085f), owner.Close);
            Localize(back.GetComponentInChildren<Text>(), "settings.back", "돌아가기");
            SelectTab(0);
            RefreshAll();
        }

        private void OnEnable()
        {
            _entryTime = 0f;
            if (_entryFade != null) _entryFade.alpha = 0f;
            GameSettings.Changed += RefreshAll;
            L10n.Changed += RefreshAll;
            RefreshAll();
        }
        private void OnDisable()
        {
            GameSettings.Changed -= RefreshAll;
            L10n.Changed -= RefreshAll;
            _lastFocus = null;
        }
        public void RefreshAll()
        {
            foreach (var refresh in _localize) refresh();
            foreach (var refresh in _refresh) refresh();
        }

        private void AddTab(Transform tabs, Transform shell, string key, string fallback)
        {
            int index = _tabs.Count;
            var button = RuntimeUiControls.Button(tabs, _font, fallback, Vector2.zero, Vector2.one, () => SelectTab(index));
            Localize(button.GetComponentInChildren<Text>(), key, fallback);
            _tabs.Add(button);
            var viewport = RuntimeUiControls.Rect(shell, fallback + "Viewport", Vector2.zero, Vector2.one);
            viewport.offsetMin = new Vector2(28, 92); viewport.offsetMax = new Vector2(-28, -194);
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .001f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = RuntimeUiControls.Rect(viewport, "Content", new Vector2(0, 1), Vector2.one);
            content.pivot = new Vector2(.5f, 1);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12; layout.padding = new RectOffset(1, 1, 1, 1);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content; scroll.viewport = viewport; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
            _pages.Add(scroll);
        }

        private void SelectTab(int index)
        {
            _selected = index;
            for (int i = 0; i < _pages.Count; i++)
            {
                _pages[i].gameObject.SetActive(i == index);
                TheatreUiStyle.Button(_tabs[i], i == index);
            }
            _lastFocus = null;
        }

        private RectTransform Row(Transform parent, string key, string fallback)
        {
            var row = RuntimeUiControls.Rect(parent, key, Vector2.zero, Vector2.one);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 112;
            TheatreUiStyle.SurfaceImage(row.gameObject.AddComponent<Image>(), new Color(.095f, .125f, .175f), 12, false);
            var label = Label(row, key, fallback, 21, TheatreUiStyle.Ivory);
            RuntimeUiControls.Place(label.rectTransform, new Vector2(.04f, .62f), new Vector2(.76f, .92f));
            return row;
        }

        private void AudioRow(Transform parent, string key, string fallback, Func<float> value, UnityEngine.Events.UnityAction<float> set)
        {
            var row = Row(parent, key, fallback);
            var number = RuntimeUiControls.Label(row, _font, "", 20);
            number.alignment = TextAnchor.MiddleRight; number.color = TheatreUiStyle.Brass;
            RuntimeUiControls.Place(number.rectTransform, new Vector2(.78f, .62f), new Vector2(.96f, .92f));
            var rect = RuntimeUiControls.Rect(row, "Slider", new Vector2(.025f, .10f), new Vector2(.975f, .54f));
            rect.gameObject.AddComponent<Image>();
            var slider = rect.gameObject.AddComponent<Slider>();
            var fill = RuntimeUiControls.Rect(rect, "Fill", Vector2.zero, Vector2.one); fill.gameObject.AddComponent<Image>();
            var handle = RuntimeUiControls.Rect(rect, "Handle", Vector2.zero, Vector2.one); handle.gameObject.AddComponent<Image>();
            slider.fillRect = fill; slider.handleRect = handle; slider.minValue = 0; slider.maxValue = 1;
            TheatreUiStyle.Slider(slider);
            slider.onValueChanged.AddListener(set);
            _refresh.Add(() => { slider.SetValueWithoutNotify(value()); number.text = $"{Mathf.RoundToInt(value() * 100)}%"; });
        }

        private void Segments(Transform parent, string key, string fallback, string[] keys, string[] names, Func<int> selected, Action<int> select)
        {
            var row = Row(parent, key, fallback);
            var buttons = new List<Button>();
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                float step = .92f / names.Length;
                var button = RuntimeUiControls.Button(row, _font, names[i],
                    new Vector2(.04f + i * step, .12f), new Vector2(.04f + (i + 1) * step - .008f, .55f), () => select(index));
                Localize(button.GetComponentInChildren<Text>(), keys[i], names[i]);
                buttons.Add(button);
            }
            _refresh.Add(() => { for (int i = 0; i < buttons.Count; i++) TheatreUiStyle.Button(buttons[i], i == selected()); });
        }

        private void Switch(Transform parent, string key, string fallback, Func<bool> value, UnityEngine.Events.UnityAction toggle)
        {
            var row = Row(parent, key, fallback);
            var state = RuntimeUiControls.Label(row, _font, "", 18);
            state.alignment = TextAnchor.MiddleLeft;
            RuntimeUiControls.Place(state.rectTransform, new Vector2(.04f, .12f), new Vector2(.60f, .52f));
            var rect = RuntimeUiControls.Rect(row, "Switch", new Vector2(1, .12f), new Vector2(1, .56f));
            rect.pivot = new Vector2(1, .5f); rect.sizeDelta = new Vector2(84, 0); rect.anchoredPosition = new Vector2(-20, 0);
            var image = rect.gameObject.AddComponent<Image>();
            TheatreUiStyle.SurfaceImage(image, TheatreUiStyle.Raised, 24, false);
            var control = rect.gameObject.AddComponent<Toggle>(); control.targetGraphic = image;
            var knob = RuntimeUiControls.Rect(rect, "Knob", new Vector2(.25f, .5f), new Vector2(.25f, .5f));
            knob.sizeDelta = new Vector2(32, 32);
            var knobImage = knob.gameObject.AddComponent<Image>(); knobImage.raycastTarget = false;
            TheatreUiStyle.SurfaceImage(knobImage, TheatreUiStyle.Ivory, 16, false);
            control.onValueChanged.AddListener(on => { if (on != value()) toggle(); });
            _refresh.Add(() =>
            {
                control.SetIsOnWithoutNotify(value());
                image.color = value() ? new Color(.23f, .52f, .45f) : TheatreUiStyle.Raised;
                state.text = value() ? L10n.Get("settings.on", "켜짐") : L10n.Get("settings.off", "꺼짐");
                state.color = value() ? TheatreUiStyle.Mint : TheatreUiStyle.Muted;
            });
            _animate.Add(dt =>
            {
                float x = Mathf.MoveTowards(knob.anchorMin.x, value() ? .75f : .25f, dt * 5f);
                knob.anchorMin = knob.anchorMax = new Vector2(x, .5f);
            });
        }

        private void Update()
        {
            if (_entryFade != null)
            {
                _entryTime += Time.unscaledDeltaTime;
                _entryFade.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_entryTime / .18f));
            }
            foreach (var animate in _animate) animate(Time.unscaledDeltaTime);
#if ENABLE_LEGACY_INPUT_MANAGER
            if (_owner != null && (Input.GetKeyDown(KeyCode.Escape) || Input.GetButtonDown("Cancel")))
            {
                // Let an open native dropdown consume Cancel before closing settings.
                if (!_dropdownWasExpanded && GetComponentInChildren<Dropdown>()?.IsExpanded != true) _owner.Close();
            }
#endif
        }

        private void LateUpdate()
        {
            _dropdownWasExpanded = GetComponentInChildren<Dropdown>()?.IsExpanded == true;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null || selected == _lastFocus || _pages.Count == 0) return;
            _lastFocus = selected;
            var scroll = _pages[_selected];
            if (!selected.transform.IsChildOf(scroll.content)) return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, selected.transform);
            Rect visible = scroll.viewport.rect;
            float shift = bounds.min.y < visible.yMin ? visible.yMin - bounds.min.y
                : bounds.max.y > visible.yMax ? visible.yMax - bounds.max.y : 0f;
            var position = scroll.content.anchoredPosition;
            position.y = Mathf.Clamp(position.y + shift, 0, Mathf.Max(0, scroll.content.rect.height - visible.height));
            scroll.content.anchoredPosition = position;
        }

        private Text Label(Transform parent, string key, string fallback, int size, Color color)
        {
            var label = RuntimeUiControls.Label(parent, _font, fallback, size);
            label.alignment = TextAnchor.MiddleLeft; label.color = color;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 14; label.resizeTextMaxSize = size;
            Localize(label, key, fallback); return label;
        }
        private void Localize(Text label, string key, string fallback)
        { _localize.Add(() => label.text = L10n.Get(key, fallback)); label.text = L10n.Get(key, fallback); }
        private static void Top(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
        private static void StretchTop(RectTransform rect, float left, float top, float right, float height)
        {
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, -top - height); rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
