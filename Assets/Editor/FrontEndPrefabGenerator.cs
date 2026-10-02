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
                typeof(EndingManager));
            PrefabUtility.SaveAsPrefabAsset(systems, SystemsFolder + "/CoreSystems.prefab");
            Object.DestroyImmediate(systems);
        }

        private static void GenerateTitleCanvas()
        {
            var root = new GameObject("TitleCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(StarterSelectionController),
                typeof(TitleScreenController));
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

            var newButton = CreateButton("NewGameButton", titleRoot, new Vector2(0.19f, 0.30f),
                new Vector2(0.81f, 0.38f), "새로운 기억", new Color(0.38f, 0.20f, 0.65f, 1f));
            var continueButton = CreateButton("ContinueButton", titleRoot, new Vector2(0.19f, 0.20f),
                new Vector2(0.81f, 0.28f), "이어하기", new Color(0.10f, 0.08f, 0.18f, 1f));

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
            controllerSo.FindProperty("starterSelection").objectReferenceValue = selection;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.Events.UnityEventTools.AddPersistentListener(newButton.onClick, controller.NewGame);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(continueButton.onClick, controller.ContinueGame);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(backButton.onClick,
                controller.CancelStarterSelection);
            starterRoot.gameObject.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, UiFolder + "/TitleCanvas.prefab");
            Object.DestroyImmediate(root);
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
