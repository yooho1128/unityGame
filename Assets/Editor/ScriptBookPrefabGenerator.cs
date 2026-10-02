#if UNITY_EDITOR
using ShadowTheater.Field;
using ShadowTheater.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.EditorTools
{
    public static class ScriptBookPrefabGenerator
    {
        private const string PrefabPath = "Assets/Prefabs/UI/ScriptBookCanvas.prefab";

        [MenuItem("Tools/Shadow Theater/Generate Script Book UI")]
        public static void Generate()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "UI");

            var canvasGo = new GameObject("ScriptBookCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ScriptBookController));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 55;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var openButton = CreateButton("OpenScriptBook", canvasGo.transform, new Vector2(0.025f, 0.90f),
                new Vector2(0.20f, 0.955f), "각본집", new Color(0.12f, 0.075f, 0.22f, 0.92f));
            openButton.gameObject.AddComponent<VirtualScriptBookButton>();

            var root = CreateRect("BookRoot", canvasGo.transform, Vector2.zero, Vector2.one);
            var backdrop = root.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.015f, 0.010f, 0.035f, 0.985f);

            var header = CreateText("Header", root, new Vector2(0.055f, 0.91f),
                new Vector2(0.72f, 0.98f), 48, FontStyle.Bold, TextAnchor.MiddleLeft);
            header.text = "각본집 · 잊힌 배역";
            var progress = CreateText("Progress", root, new Vector2(0.60f, 0.91f),
                new Vector2(0.84f, 0.98f), 26, FontStyle.Bold, TextAnchor.MiddleRight);
            progress.color = new Color(0.72f, 0.58f, 1f, 1f);
            var close = CreateButton("Close", root, new Vector2(0.86f, 0.92f),
                new Vector2(0.96f, 0.975f), "×", new Color(0.17f, 0.10f, 0.27f, 1f));

            var allButton = CreateButton("All", root, new Vector2(0.055f, 0.85f),
                new Vector2(0.31f, 0.90f), "전체", new Color(0.25f, 0.14f, 0.42f, 1f));
            var seenButton = CreateButton("Seen", root, new Vector2(0.325f, 0.85f),
                new Vector2(0.58f, 0.90f), "조우", new Color(0.10f, 0.075f, 0.17f, 1f));
            var recordedButton = CreateButton("Recorded", root, new Vector2(0.595f, 0.85f),
                new Vector2(0.945f, 0.90f), "기록 완료", new Color(0.10f, 0.075f, 0.17f, 1f));

            var listPanel = CreateRect("ListPanel", root, new Vector2(0.055f, 0.58f),
                new Vector2(0.945f, 0.83f));
            var listImage = listPanel.gameObject.AddComponent<Image>();
            listImage.color = new Color(0.035f, 0.025f, 0.07f, 1f);
            var scroll = listPanel.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = CreateRect("Viewport", listPanel, Vector2.zero, Vector2.one);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var content = CreateRect("Content", viewport, new Vector2(0f, 1f), Vector2.one);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 120f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 8f;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;

            var template = CreateEntryTemplate(content);
            template.gameObject.SetActive(false);

            var detail = CreateRect("DetailPanel", root, new Vector2(0.055f, 0.04f),
                new Vector2(0.945f, 0.55f));
            var detailImage = detail.gameObject.AddComponent<Image>();
            detailImage.color = new Color(0.035f, 0.025f, 0.07f, 1f);
            var accent = CreateRect("AccentGlow", detail, new Vector2(0f, 0f), new Vector2(0.014f, 1f))
                .gameObject.AddComponent<Image>();
            accent.raycastTarget = false;
            var portrait = CreateRect("Portrait", detail, new Vector2(0.04f, 0.62f), new Vector2(0.31f, 0.94f))
                .gameObject.AddComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            var name = CreateText("Name", detail, new Vector2(0.35f, 0.82f),
                new Vector2(0.95f, 0.95f), 38, FontStyle.Bold, TextAnchor.MiddleLeft);
            var title = CreateText("Title", detail, new Vector2(0.35f, 0.71f),
                new Vector2(0.95f, 0.83f), 25, FontStyle.Normal, TextAnchor.MiddleLeft);
            title.color = new Color(0.74f, 0.69f, 0.82f, 1f);
            var type = CreateText("Type", detail, new Vector2(0.35f, 0.61f),
                new Vector2(0.95f, 0.72f), 23, FontStyle.Bold, TextAnchor.MiddleLeft);
            type.color = new Color(0.72f, 0.55f, 1f, 1f);
            var stage = CreateText("MemoryStage", detail, new Vector2(0.04f, 0.53f),
                new Vector2(0.96f, 0.62f), 24, FontStyle.Bold, TextAnchor.MiddleLeft);
            stage.color = new Color(0.58f, 0.84f, 1f, 1f);
            var stats = CreateText("Stats", detail, new Vector2(0.04f, 0.45f),
                new Vector2(0.96f, 0.54f), 22, FontStyle.Normal, TextAnchor.MiddleLeft);
            var restore = CreateButton("Restore", detail, new Vector2(0.04f, 0.36f),
                new Vector2(0.32f, 0.44f), "기억 복원", new Color(0.16f, 0.24f, 0.42f, 1f));
            var salvation = CreateButton("Salvation", detail, new Vector2(0.35f, 0.36f),
                new Vector2(0.64f, 0.44f), "구원 각성", new Color(0.13f, 0.32f, 0.29f, 1f));
            var grudge = CreateButton("Grudge", detail, new Vector2(0.67f, 0.36f),
                new Vector2(0.96f, 0.44f), "원한 각성", new Color(0.36f, 0.10f, 0.25f, 1f));
            var feedback = CreateText("AwakeningFeedback", detail, new Vector2(0.04f, 0.29f),
                new Vector2(0.96f, 0.36f), 20, FontStyle.Normal, TextAnchor.MiddleLeft);
            feedback.color = new Color(0.72f, 0.68f, 0.82f, 1f);
            var lore = CreateText("Lore", detail, new Vector2(0.04f, 0.045f),
                new Vector2(0.96f, 0.285f), 23, FontStyle.Normal, TextAnchor.UpperLeft);
            lore.horizontalOverflow = HorizontalWrapMode.Wrap;
            lore.verticalOverflow = VerticalWrapMode.Truncate;
            lore.color = new Color(0.84f, 0.81f, 0.90f, 1f);

            var controller = canvasGo.GetComponent<ScriptBookController>();
            var so = new SerializedObject(controller);
            so.FindProperty("root").objectReferenceValue = root.gameObject;
            so.FindProperty("contentRoot").objectReferenceValue = content;
            so.FindProperty("entryTemplate").objectReferenceValue = template;
            so.FindProperty("progressText").objectReferenceValue = progress;
            so.FindProperty("portrait").objectReferenceValue = portrait;
            so.FindProperty("accentGlow").objectReferenceValue = accent;
            so.FindProperty("nameText").objectReferenceValue = name;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("typeText").objectReferenceValue = type;
            so.FindProperty("statsText").objectReferenceValue = stats;
            so.FindProperty("loreText").objectReferenceValue = lore;
            so.FindProperty("stageText").objectReferenceValue = stage;
            so.FindProperty("awakeningFeedbackText").objectReferenceValue = feedback;
            so.FindProperty("restoreButton").objectReferenceValue = restore;
            so.FindProperty("salvationButton").objectReferenceValue = salvation;
            so.FindProperty("grudgeButton").objectReferenceValue = grudge;
            so.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.Events.UnityEventTools.AddPersistentListener(close.onClick, controller.Close);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(allButton.onClick, controller.ShowAll);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(seenButton.onClick, controller.ShowSeen);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(recordedButton.onClick, controller.ShowRecorded);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(restore.onClick, controller.RestoreSelected);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(salvation.onClick, controller.AwakenSalvation);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(grudge.onClick, controller.AwakenGrudge);
            root.gameObject.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(canvasGo, PrefabPath);
            Object.DestroyImmediate(canvasGo);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            Debug.Log($"[Shadow Theater] 각본집 UI 생성 완료: {PrefabPath}");
        }

        private static ScriptBookEntryView CreateEntryTemplate(Transform parent)
        {
            var rect = CreateRect("EntryTemplate", parent, Vector2.zero, Vector2.one);
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 108f;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.065f, 0.047f, 0.115f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var accent = CreateRect("Accent", rect, Vector2.zero, new Vector2(0.014f, 1f))
                .gameObject.AddComponent<Image>();
            var silhouette = CreateRect("Silhouette", rect, new Vector2(0.035f, 0.10f),
                new Vector2(0.17f, 0.90f)).gameObject.AddComponent<Image>();
            silhouette.preserveAspect = true;
            silhouette.raycastTarget = false;
            var number = CreateText("Number", rect, new Vector2(0.20f, 0.51f),
                new Vector2(0.37f, 0.88f), 21, FontStyle.Normal, TextAnchor.MiddleLeft);
            number.color = new Color(0.56f, 0.52f, 0.64f, 1f);
            var name = CreateText("Name", rect, new Vector2(0.20f, 0.12f),
                new Vector2(0.70f, 0.58f), 29, FontStyle.Bold, TextAnchor.MiddleLeft);
            var state = CreateText("State", rect, new Vector2(0.70f, 0.12f),
                new Vector2(0.95f, 0.88f), 22, FontStyle.Bold, TextAnchor.MiddleRight);
            state.color = new Color(0.72f, 0.58f, 1f, 1f);

            var view = rect.gameObject.AddComponent<ScriptBookEntryView>();
            var so = new SerializedObject(view);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("silhouette").objectReferenceValue = silhouette;
            so.FindProperty("accent").objectReferenceValue = accent;
            so.FindProperty("numberText").objectReferenceValue = number;
            so.FindProperty("nameText").objectReferenceValue = name;
            so.FindProperty("stateText").objectReferenceValue = state;
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
            var text = CreateText("Label", rect, Vector2.zero, Vector2.one, 25, FontStyle.Bold,
                TextAnchor.MiddleCenter);
            text.text = label;
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
