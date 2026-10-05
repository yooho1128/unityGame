#if UNITY_EDITOR
using ShadowTheater.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.EditorTools
{
    public static class DialogueUIPrefabGenerator
    {
        private const string PrefabFolder = "Assets/Prefabs/UI";
        private const string PrefabPath = PrefabFolder + "/DialogueCanvas.prefab";

        [MenuItem("Tools/Shadow Theater/Generate Dialogue UI Prefab")]
        public static void Generate()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "UI");

            var canvasGo = new GameObject("DialogueCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(DialogueController));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = CreateRect("Panel", canvasGo.transform, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            var group = panel.gameObject.AddComponent<CanvasGroup>();

            var dim = panel.gameObject.AddComponent<Image>();
            dim.color = new Color(0.015f, 0.012f, 0.035f, 0.22f);

            var box = CreateRect("DialogueBox", panel, new Vector2(0.045f, 0.035f),
                new Vector2(0.955f, 0.285f), Vector2.zero, Vector2.zero);
            var boxImage = box.gameObject.AddComponent<Image>();
            boxImage.color = new Color(0.035f, 0.026f, 0.075f, 0.96f);
            boxImage.raycastTarget = false;
            var outline = box.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.57f, 0.36f, 0.91f, 0.8f);
            outline.effectDistance = new Vector2(3f, -3f);

            var accent = CreateRect("Accent", box, Vector2.zero, new Vector2(0.018f, 1f),
                Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            accent.color = new Color(0.71f, 0.38f, 1f, 1f);
            accent.raycastTarget = false;

            var speaker = CreateText("Speaker", box, new Vector2(0.06f, 0.69f),
                new Vector2(0.92f, 0.93f), 42, FontStyle.Bold, TextAnchor.MiddleLeft);
            speaker.color = new Color(0.87f, 0.75f, 1f, 1f);

            var body = CreateText("Body", box, new Vector2(0.06f, 0.18f),
                new Vector2(0.92f, 0.71f), 35, FontStyle.Normal, TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.color = new Color(0.97f, 0.96f, 1f, 1f);

            var next = CreateText("Continue", box, new Vector2(0.78f, 0.02f),
                new Vector2(0.94f, 0.18f), 27, FontStyle.Bold, TextAnchor.MiddleRight);
            next.text = "▼  계속";
            next.color = new Color(0.72f, 0.63f, 0.95f, 1f);

            var choices = CreateRect("Choices", panel, new Vector2(0.09f, 0.31f),
                new Vector2(0.91f, 0.61f), Vector2.zero, Vector2.zero);
            var choiceViews = new DialogueChoiceView[3];
            for (int i = 0; i < choiceViews.Length; i++)
            {
                float top = 1f - i * 0.34f;
                float bottom = top - 0.28f;
                choiceViews[i] = CreateChoice($"Choice_{i + 1}", choices,
                    new Vector2(0f, bottom), new Vector2(1f, top));
            }
            choices.gameObject.SetActive(false);

            var tap = panel.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;

            var controller = canvasGo.GetComponent<DialogueController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("panelRoot").objectReferenceValue = panel.gameObject;
            serialized.FindProperty("canvasGroup").objectReferenceValue = group;
            serialized.FindProperty("speakerText").objectReferenceValue = speaker;
            serialized.FindProperty("bodyText").objectReferenceValue = body;
            serialized.FindProperty("continueText").objectReferenceValue = next;
            serialized.FindProperty("accentBar").objectReferenceValue = accent;
            serialized.FindProperty("dialogueBox").objectReferenceValue = box;
            serialized.FindProperty("dialogueBoxImage").objectReferenceValue = boxImage;
            serialized.FindProperty("choiceRoot").objectReferenceValue = choices.gameObject;
            var viewsProperty = serialized.FindProperty("choiceViews");
            viewsProperty.arraySize = choiceViews.Length;
            for (int i = 0; i < choiceViews.Length; i++)
                viewsProperty.GetArrayElementAtIndex(i).objectReferenceValue = choiceViews[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.Events.UnityEventTools.AddPersistentListener(tap.onClick, controller.Advance);

            PrefabUtility.SaveAsPrefabAsset(canvasGo, PrefabPath);
            Object.DestroyImmediate(canvasGo);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            Debug.Log($"[Shadow Theater] 대화 UI 생성 완료: {PrefabPath}");
        }

        private static DialogueChoiceView CreateChoice(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = CreateRect(name, parent, min, max, Vector2.zero, Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.08f, 0.055f, 0.15f, 0.97f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.08f, 1.25f, 1f);
            colors.pressedColor = new Color(0.72f, 0.62f, 0.9f, 1f);
            button.colors = colors;

            var label = CreateText("Label", rect, new Vector2(0.045f, 0f), new Vector2(0.955f, 1f),
                29, FontStyle.Normal, TextAnchor.MiddleLeft);
            label.color = new Color(0.96f, 0.93f, 1f, 1f);

            var view = rect.gameObject.AddComponent<DialogueChoiceView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("button").objectReferenceValue = button;
            serialized.FindProperty("label").objectReferenceValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static Text CreateText(string name, Transform parent, Vector2 anchorMin,
            Vector2 anchorMax, int size, FontStyle style, TextAnchor alignment)
        {
            var rect = CreateRect(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
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
