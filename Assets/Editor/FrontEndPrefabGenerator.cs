#if UNITY_EDITOR
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.EditorTools
{
    public static class FrontEndPrefabGenerator
    {
        private const string UiFolder = "Assets/Prefabs/UI";
        private const string SystemsFolder = "Assets/Prefabs/Systems";

        [MenuItem("Tools/Shadow Theater/Generate Title and Starter UI")]
        public static void Generate()
        {
            MusicAssetGenerator.Generate();
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "UI");
            EnsureFolder("Assets/Prefabs", "Systems");
            GenerateCoreSystems();
            GenerateTitleCanvas();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(UiFolder + "/TitleCanvas.prefab"));
            Debug.Log("[Shadow Theater] CoreSystems / TitleCanvas 프리팹 생성 완료");
        }

        private static void GenerateCoreSystems()
        {
            var systems = new GameObject("CoreSystems", typeof(SaveManager), typeof(MapLoader),
                typeof(EndingManager), typeof(GameSettings), typeof(LocalizationRuntime),
                typeof(MobilePerformanceController), typeof(AdaptiveMusicDirector));
            var musicSo = new SerializedObject(systems.GetComponent<AdaptiveMusicDirector>());
            AudioClip[] music = MusicAssetGenerator.LoadAll();
            var musicClips = musicSo.FindProperty("clips");
            musicClips.arraySize = music.Length;
            for (int i = 0; i < music.Length; i++) musicClips.GetArrayElementAtIndex(i).objectReferenceValue = music[i];
            musicSo.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(systems, SystemsFolder + "/CoreSystems.prefab");
            Object.DestroyImmediate(systems);
        }

        private static void GenerateTitleCanvas()
        {
            var root = new GameObject("TitleCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(StarterSelectionController),
                typeof(TitleScreenController), typeof(EndingGalleryController), typeof(SettingsPanelController));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var background = CreateRect("Background", root.transform, Vector2.zero, Vector2.one);
            var backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.018f, 0.012f, 0.045f, 1f);

            var titleRoot = CreateRect("TitleRoot", root.transform, Vector2.zero, Vector2.one);
            var eyebrow = CreateText("Eyebrow", titleRoot, new Vector2(0.1f, 0.74f),
                new Vector2(0.9f, 0.82f), 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            eyebrow.text = "SHADOW THEATER";
            eyebrow.color = new Color(0.62f, 0.43f, 0.95f, 1f);
            var title = CreateText("Title", titleRoot, new Vector2(0.07f, 0.57f),
                new Vector2(0.93f, 0.75f), 72, FontStyle.Bold, TextAnchor.MiddleCenter);
            title.text = "그림자 극장\n잔영";
            title.color = new Color(0.96f, 0.92f, 1f, 1f);
            var subtitle = CreateText("Subtitle", titleRoot, new Vector2(0.12f, 0.49f),
                new Vector2(0.88f, 0.57f), 25, FontStyle.Normal, TextAnchor.MiddleCenter);
            subtitle.text = "잊힌 비극의 마지막 장면을 기록하라";
            subtitle.color = new Color(0.66f, 0.62f, 0.76f, 1f);

            var newButton = CreateButton("NewGameButton", titleRoot, new Vector2(0.19f, 0.34f),
                new Vector2(0.81f, 0.41f), "새로운 기억", new Color(0.38f, 0.20f, 0.65f, 1f));
            var continueButton = CreateButton("ContinueButton", titleRoot, new Vector2(0.19f, 0.25f),
                new Vector2(0.81f, 0.32f), "이어하기", new Color(0.10f, 0.08f, 0.18f, 1f));
            var cycleButton = CreateButton("NewCycleButton", titleRoot, new Vector2(0.19f, 0.16f),
                new Vector2(0.81f, 0.23f), "다음 회차 시작", new Color(0.17f, 0.35f, 0.34f, 1f));
            var galleryButton = CreateButton("EndingGalleryButton", titleRoot, new Vector2(0.19f, 0.07f),
                new Vector2(0.49f, 0.14f), "엔딩 기록관", new Color(0.10f, 0.08f, 0.18f, 1f));
            var settingsButton = CreateButton("SettingsButton", titleRoot, new Vector2(0.51f, 0.07f),
                new Vector2(0.81f, 0.14f), "설정", new Color(0.10f, 0.08f, 0.18f, 1f));

            var galleryRoot = CreateRect("EndingGalleryRoot", root.transform, Vector2.zero, Vector2.one);
            galleryRoot.gameObject.AddComponent<Image>().color = new Color(.018f,.012f,.045f,.98f);
            var galleryTitle = CreateText("Header", galleryRoot, new Vector2(.07f,.88f), new Vector2(.93f,.96f),
                48, FontStyle.Bold, TextAnchor.MiddleCenter);
            galleryTitle.text = "엔딩 기록관";
            var progress = CreateText("Progress", galleryRoot, new Vector2(.07f,.82f), new Vector2(.50f,.87f),
                23, FontStyle.Bold, TextAnchor.MiddleLeft);
            progress.color = new Color(.78f,.66f,1f,1f);
            var cycle = CreateText("Cycle", galleryRoot, new Vector2(.50f,.82f), new Vector2(.93f,.87f),
                23, FontStyle.Normal, TextAnchor.MiddleRight);
            cycle.color = new Color(.68f,.65f,.76f,1f);

            var listPanel = CreateRect("ListPanel", galleryRoot, new Vector2(.05f,.18f), new Vector2(.49f,.80f));
            listPanel.gameObject.AddComponent<Image>().color = new Color(.04f,.028f,.075f,.96f);
            var content = CreateRect("Content", listPanel, new Vector2(.04f,.04f), new Vector2(.96f,.96f));
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            var entryTemplate = CreateEndingEntry("EndingEntryTemplate", content);
            entryTemplate.gameObject.SetActive(false);

            var detailPanel = CreateRect("DetailPanel", galleryRoot, new Vector2(.52f,.28f), new Vector2(.95f,.80f));
            detailPanel.gameObject.AddComponent<Image>().color = new Color(.055f,.038f,.10f,.98f);
            var detailEyebrow = CreateText("Eyebrow", detailPanel, new Vector2(.08f,.78f), new Vector2(.92f,.93f),
                20, FontStyle.Bold, TextAnchor.MiddleCenter);
            detailEyebrow.text = "FINAL CURTAIN"; detailEyebrow.color = new Color(.64f,.46f,.94f,1f);
            var detailTitle = CreateText("Title", detailPanel, new Vector2(.08f,.53f), new Vector2(.92f,.79f),
                38, FontStyle.Bold, TextAnchor.MiddleCenter);
            var detailSubtitle = CreateText("Subtitle", detailPanel, new Vector2(.10f,.24f), new Vector2(.90f,.54f),
                24, FontStyle.Normal, TextAnchor.MiddleCenter);
            detailSubtitle.color = new Color(.74f,.70f,.82f,1f);
            var detailState = CreateText("State", detailPanel, new Vector2(.08f,.08f), new Vector2(.92f,.22f),
                19, FontStyle.Bold, TextAnchor.MiddleCenter);
            detailState.color = new Color(.48f,.88f,.78f,1f);
            var galleryBack = CreateButton("BackButton", galleryRoot, new Vector2(.30f,.07f),
                new Vector2(.70f,.13f), "돌아가기", new Color(.08f,.065f,.14f,1f));

            var settingsRoot = CreateRect("SettingsRoot", root.transform, Vector2.zero, Vector2.one);
            settingsRoot.gameObject.AddComponent<Image>().color = new Color(.018f,.012f,.045f,.98f);
            var settingsTitle = CreateText("Header", settingsRoot, new Vector2(.07f,.88f),
                new Vector2(.93f,.96f), 48, FontStyle.Bold, TextAnchor.MiddleCenter);
            settingsTitle.text = "설정";
            var settingsHint = CreateText("Hint", settingsRoot, new Vector2(.08f,.82f),
                new Vector2(.92f,.87f), 21, FontStyle.Normal, TextAnchor.MiddleCenter);
            settingsHint.text = "공연 환경은 모든 저장 데이터에 공통으로 적용됩니다";
            settingsHint.color = new Color(.68f,.64f,.76f,1f);
            var settingsPanel = CreateRect("Panel", settingsRoot, new Vector2(.10f,.19f), new Vector2(.90f,.79f));
            settingsPanel.gameObject.AddComponent<Image>().color = new Color(.045f,.03f,.085f,.97f);

            Text masterValue, ambienceValue, musicValue, sfxValue;
            var masterSlider = CreateSettingsSlider("Master", settingsPanel, .92f, "전체 음량", out masterValue);
            var musicSlider = CreateSettingsSlider("Music", settingsPanel, .78f, "음악", out musicValue);
            var ambienceSlider = CreateSettingsSlider("Ambience", settingsPanel, .64f, "환경음", out ambienceValue);
            var sfxSlider = CreateSettingsSlider("Sfx", settingsPanel, .50f, "효과음", out sfxValue);
            Text vibrationValue, textSpeedValue, languageValue;
            var vibrationButton = CreateSettingsChoice("Vibration", settingsPanel, .34f, "진동", out vibrationValue);
            var speedButton = CreateSettingsChoice("TextSpeed", settingsPanel, .20f, "대화 속도", out textSpeedValue);
            var languageButton = CreateSettingsChoice("Language", settingsPanel, .06f, "언어", out languageValue);
            var settingsBack = CreateButton("BackButton", settingsRoot, new Vector2(.30f,.07f),
                new Vector2(.70f,.13f), "돌아가기", new Color(.08f,.065f,.14f,1f));

            var starterRoot = CreateRect("StarterRoot", root.transform, Vector2.zero, Vector2.one);
            var starterTitle = CreateText("Header", starterRoot, new Vector2(0.08f, 0.86f),
                new Vector2(0.92f, 0.95f), 45, FontStyle.Bold, TextAnchor.MiddleCenter);
            starterTitle.text = "첫 번째 그림자를 선택하세요";
            starterTitle.color = new Color(0.96f, 0.92f, 1f, 1f);
            var starterHint = CreateText("Hint", starterRoot, new Vector2(0.1f, 0.81f),
                new Vector2(0.9f, 0.86f), 23, FontStyle.Normal, TextAnchor.MiddleCenter);
            starterHint.text = "함께 기억의 여정을 시작할 단 한 명의 배역";
            starterHint.color = new Color(0.66f, 0.62f, 0.76f, 1f);

            var cards = new StarterCardView[3];
            for (int i = 0; i < cards.Length; i++)
            {
                float top = 0.78f - i * 0.205f;
                cards[i] = CreateStarterCard($"StarterCard_{i + 1}", starterRoot,
                    new Vector2(0.075f, top - 0.17f), new Vector2(0.925f, top));
            }
            var backButton = CreateButton("BackButton", starterRoot, new Vector2(0.30f, 0.08f),
                new Vector2(0.70f, 0.14f), "돌아가기", new Color(0.08f, 0.065f, 0.14f, 1f));

            var selection = root.GetComponent<StarterSelectionController>();
            var selectionSo = new SerializedObject(selection);
            selectionSo.FindProperty("root").objectReferenceValue = starterRoot.gameObject;
            var cardsProp = selectionSo.FindProperty("cards");
            cardsProp.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
                cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            selectionSo.ApplyModifiedPropertiesWithoutUndo();

            var controller = root.GetComponent<TitleScreenController>();
            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("titleRoot").objectReferenceValue = titleRoot.gameObject;
            controllerSo.FindProperty("continueButton").objectReferenceValue = continueButton;
            controllerSo.FindProperty("newCycleButton").objectReferenceValue = cycleButton;
            controllerSo.FindProperty("starterSelection").objectReferenceValue = selection;
            controllerSo.FindProperty("endingGallery").objectReferenceValue = root.GetComponent<EndingGalleryController>();
            controllerSo.FindProperty("settingsPanel").objectReferenceValue = root.GetComponent<SettingsPanelController>();
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            var gallery = root.GetComponent<EndingGalleryController>();
            var gallerySo = new SerializedObject(gallery);
            gallerySo.FindProperty("root").objectReferenceValue = galleryRoot.gameObject;
            gallerySo.FindProperty("titleRoot").objectReferenceValue = titleRoot.gameObject;
            gallerySo.FindProperty("content").objectReferenceValue = content;
            gallerySo.FindProperty("entryTemplate").objectReferenceValue = entryTemplate;
            gallerySo.FindProperty("progressText").objectReferenceValue = progress;
            gallerySo.FindProperty("cycleText").objectReferenceValue = cycle;
            gallerySo.FindProperty("detailTitle").objectReferenceValue = detailTitle;
            gallerySo.FindProperty("detailSubtitle").objectReferenceValue = detailSubtitle;
            gallerySo.FindProperty("detailState").objectReferenceValue = detailState;
            gallerySo.ApplyModifiedPropertiesWithoutUndo();

            var settings = root.GetComponent<SettingsPanelController>();
            var settingsSo = new SerializedObject(settings);
            settingsSo.FindProperty("root").objectReferenceValue = settingsRoot.gameObject;
            settingsSo.FindProperty("titleRoot").objectReferenceValue = titleRoot.gameObject;
            settingsSo.FindProperty("masterSlider").objectReferenceValue = masterSlider;
            settingsSo.FindProperty("ambienceSlider").objectReferenceValue = ambienceSlider;
            settingsSo.FindProperty("musicSlider").objectReferenceValue = musicSlider;
            settingsSo.FindProperty("sfxSlider").objectReferenceValue = sfxSlider;
            settingsSo.FindProperty("masterValue").objectReferenceValue = masterValue;
            settingsSo.FindProperty("ambienceValue").objectReferenceValue = ambienceValue;
            settingsSo.FindProperty("musicValue").objectReferenceValue = musicValue;
            settingsSo.FindProperty("sfxValue").objectReferenceValue = sfxValue;
            settingsSo.FindProperty("vibrationValue").objectReferenceValue = vibrationValue;
            settingsSo.FindProperty("textSpeedValue").objectReferenceValue = textSpeedValue;
            settingsSo.FindProperty("languageValue").objectReferenceValue = languageValue;
            settingsSo.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.Events.UnityEventTools.AddPersistentListener(newButton.onClick, controller.NewGame);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(continueButton.onClick, controller.ContinueGame);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(cycleButton.onClick, controller.NewCycle);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(galleryButton.onClick, gallery.Open);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(galleryBack.onClick, gallery.Close);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(settingsButton.onClick, settings.Open);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(settingsBack.onClick, settings.Close);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(masterSlider.onValueChanged, settings.SetMaster);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(ambienceSlider.onValueChanged, settings.SetAmbience);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(musicSlider.onValueChanged, settings.SetMusic);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(sfxSlider.onValueChanged, settings.SetSfx);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(vibrationButton.onClick, settings.ToggleVibration);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(speedButton.onClick, settings.CycleTextSpeed);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(languageButton.onClick, settings.ToggleLanguage);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(backButton.onClick,
                controller.CancelStarterSelection);
            starterRoot.gameObject.SetActive(false);
            galleryRoot.gameObject.SetActive(false);
            settingsRoot.gameObject.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, UiFolder + "/TitleCanvas.prefab");
            Object.DestroyImmediate(root);
        }

        private static EndingGalleryEntryView CreateEndingEntry(string name, Transform parent)
        {
            var rect = CreateRect(name, parent, Vector2.zero, Vector2.one);
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 118f;
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.075f,.052f,.13f,1f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var accent = CreateRect("Accent", rect, Vector2.zero, new Vector2(.018f,1f)).gameObject.AddComponent<Image>();
            var number = CreateText("Number", rect, new Vector2(.05f,.63f), new Vector2(.32f,.94f), 17,
                FontStyle.Bold, TextAnchor.MiddleLeft); number.color = new Color(.65f,.52f,.88f,1f);
            var title = CreateText("Title", rect, new Vector2(.05f,.28f), new Vector2(.78f,.68f), 26,
                FontStyle.Bold, TextAnchor.MiddleLeft);
            var subtitle = CreateText("Subtitle", rect, new Vector2(.05f,.04f), new Vector2(.86f,.31f), 17,
                FontStyle.Normal, TextAnchor.MiddleLeft); subtitle.color = new Color(.68f,.64f,.75f,1f);
            var lockText = CreateText("Lock", rect, new Vector2(.76f,.56f), new Vector2(.96f,.91f), 17,
                FontStyle.Bold, TextAnchor.MiddleRight);
            var view = rect.gameObject.AddComponent<EndingGalleryEntryView>();
            var so = new SerializedObject(view);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("accent").objectReferenceValue = accent;
            so.FindProperty("numberText").objectReferenceValue = number;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("subtitleText").objectReferenceValue = subtitle;
            so.FindProperty("lockText").objectReferenceValue = lockText;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static StarterCardView CreateStarterCard(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = CreateRect(name, parent, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.055f, 0.04f, 0.10f, 0.98f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var glow = CreateRect("Accent", rect, Vector2.zero, new Vector2(0.018f, 1f))
                .gameObject.AddComponent<Image>();
            glow.raycastTarget = false;
            var portrait = CreateRect("Silhouette", rect, new Vector2(0.04f, 0.08f), new Vector2(0.25f, 0.92f))
                .gameObject.AddComponent<Image>();
            portrait.raycastTarget = false;
            var nameText = CreateText("Name", rect, new Vector2(0.29f, 0.58f),
                new Vector2(0.95f, 0.90f), 34, FontStyle.Bold, TextAnchor.MiddleLeft);
            var titleText = CreateText("Title", rect, new Vector2(0.29f, 0.36f),
                new Vector2(0.95f, 0.62f), 23, FontStyle.Normal, TextAnchor.MiddleLeft);
            titleText.color = new Color(0.73f, 0.68f, 0.81f, 1f);
            var roleText = CreateText("Role", rect, new Vector2(0.29f, 0.17f),
                new Vector2(0.65f, 0.38f), 22, FontStyle.Bold, TextAnchor.MiddleLeft);
            roleText.color = new Color(0.76f, 0.58f, 1f, 1f);
            var statsText = CreateText("Stats", rect, new Vector2(0.29f, 0.03f),
                new Vector2(0.95f, 0.20f), 20, FontStyle.Normal, TextAnchor.MiddleLeft);
            statsText.color = new Color(0.70f, 0.68f, 0.76f, 1f);

            var view = rect.gameObject.AddComponent<StarterCardView>();
            var so = new SerializedObject(view);
            so.FindProperty("selectButton").objectReferenceValue = button;
            so.FindProperty("silhouette").objectReferenceValue = portrait;
            so.FindProperty("accentGlow").objectReferenceValue = glow;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("roleText").objectReferenceValue = roleText;
            so.FindProperty("statsText").objectReferenceValue = statsText;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static Button CreateButton(string name, Transform parent, Vector2 min, Vector2 max,
            string label, Color color)
        {
            var rect = CreateRect(name, parent, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = CreateText("Label", rect, Vector2.zero, Vector2.one, 31, FontStyle.Bold,
                TextAnchor.MiddleCenter);
            text.text = label;
            return button;
        }

        private static Slider CreateSettingsSlider(string name, Transform parent, float top, string label,
            out Text valueText)
        {
            var labelText = CreateText(name + "Label", parent, new Vector2(.07f, top - .10f),
                new Vector2(.34f, top), 25, FontStyle.Bold, TextAnchor.MiddleLeft);
            labelText.text = label;
            var track = CreateRect(name + "Slider", parent, new Vector2(.38f, top - .075f),
                new Vector2(.79f, top - .025f));
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(.11f,.085f,.18f,1f);
            var slider = track.gameObject.AddComponent<Slider>();
            slider.minValue = 0f; slider.maxValue = 1f;
            var fill = CreateRect("Fill", track, new Vector2(.015f,.18f), new Vector2(.985f,.82f));
            var fillImage = fill.gameObject.AddComponent<Image>(); fillImage.color = new Color(.55f,.30f,.88f,1f);
            var handle = CreateRect("Handle", track, new Vector2(0f,-.12f), new Vector2(.06f,1.12f));
            var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = new Color(.93f,.86f,1f,1f);
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handleImage;
            valueText = CreateText(name + "Value", parent, new Vector2(.81f, top - .10f),
                new Vector2(.94f, top), 23, FontStyle.Bold, TextAnchor.MiddleRight);
            valueText.color = new Color(.78f,.66f,1f,1f);
            return slider;
        }

        private static Button CreateSettingsChoice(string name, Transform parent, float top, string label,
            out Text valueText)
        {
            var labelText = CreateText(name + "Label", parent, new Vector2(.07f, top - .09f),
                new Vector2(.48f, top), 25, FontStyle.Bold, TextAnchor.MiddleLeft);
            labelText.text = label;
            var button = CreateButton(name + "Button", parent, new Vector2(.55f, top - .085f),
                new Vector2(.94f, top), "", new Color(.095f,.07f,.16f,1f));
            valueText = button.GetComponentInChildren<Text>();
            valueText.fontSize = 23;
            valueText.color = new Color(.78f,.66f,1f,1f);
            return button;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Text CreateText(string name, Transform parent, Vector2 min, Vector2 max,
            int size, FontStyle style, TextAnchor alignment)
        {
            var rect = CreateRect(name, parent, min, max);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = new Color(0.96f, 0.93f, 1f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
