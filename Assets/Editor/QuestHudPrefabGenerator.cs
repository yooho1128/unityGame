#if UNITY_EDITOR
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.EditorTools
{
    public static class QuestHudPrefabGenerator
    {
        private const string PrefabPath = "Assets/Prefabs/UI/QuestHUDCanvas.prefab";

        [MenuItem("Tools/Shadow Theater/Generate Quest HUD Prefab")]
        public static void Generate()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "UI");

            var root = new GameObject("QuestHUDCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(QuestManager), typeof(QuestHudController), typeof(QuestCompletionToast));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 25;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = CreateRect("QuestPanel", root.transform, new Vector2(0.48f, 0.78f),
                new Vector2(0.965f, 0.965f));
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.025f, 0.02f, 0.06f, 0.88f);
            panelImage.raycastTarget = false;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.42f, 0.30f, 0.75f, 0.75f);
            outline.effectDistance = new Vector2(2f, -2f);

            var label = CreateText("Label", panel, new Vector2(0.055f, 0.73f),
                new Vector2(0.27f, 0.94f), 22, FontStyle.Bold, TextAnchor.MiddleLeft);
            label.text = "MEMORY";
            label.color = new Color(0.58f, 0.43f, 0.92f, 1f);

            var title = CreateText("Title", panel, new Vector2(0.055f, 0.49f),
                new Vector2(0.94f, 0.76f), 34, FontStyle.Bold, TextAnchor.MiddleLeft);
            title.color = new Color(0.97f, 0.94f, 1f, 1f);

            var objective = CreateText("Objectives", panel, new Vector2(0.055f, 0.15f),
                new Vector2(0.94f, 0.51f), 25, FontStyle.Normal, TextAnchor.UpperLeft);
            objective.color = new Color(0.86f, 0.84f, 0.94f, 1f);
            objective.horizontalOverflow = HorizontalWrapMode.Wrap;
            objective.verticalOverflow = VerticalWrapMode.Truncate;

            var reward = CreateText("Reward", panel, new Vector2(0.055f, 0.015f),
                new Vector2(0.94f, 0.17f), 21, FontStyle.Normal, TextAnchor.MiddleRight);
            reward.color = new Color(1f, 0.76f, 0.30f, 1f);

            var complete = CreateRect("CompletionToast", root.transform, new Vector2(.16f,.61f), new Vector2(.84f,.75f));
            complete.gameObject.AddComponent<Image>().color = new Color(.045f,.025f,.09f,.97f);
            var completeGroup = complete.gameObject.AddComponent<CanvasGroup>();
            var completeTitle = CreateText("Title", complete, new Vector2(.05f,.48f), new Vector2(.95f,.92f), 32, FontStyle.Bold, TextAnchor.MiddleCenter);
            completeTitle.color = new Color(.88f,.76f,1f);
            var completeReward = CreateText("Reward", complete, new Vector2(.05f,.08f), new Vector2(.95f,.50f), 22, FontStyle.Normal, TextAnchor.MiddleCenter);
            completeReward.color = new Color(1f,.80f,.38f);

            var hud = root.GetComponent<QuestHudController>();
            var serialized = new SerializedObject(hud);
            serialized.FindProperty("panelRoot").objectReferenceValue = panel.gameObject;
            serialized.FindProperty("titleText").objectReferenceValue = title;
            serialized.FindProperty("objectiveText").objectReferenceValue = objective;
            serialized.FindProperty("rewardText").objectReferenceValue = reward;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var toastSerialized = new SerializedObject(root.GetComponent<QuestCompletionToast>());
            toastSerialized.FindProperty("root").objectReferenceValue = completeGroup;
            toastSerialized.FindProperty("titleText").objectReferenceValue = completeTitle;
            toastSerialized.FindProperty("rewardText").objectReferenceValue = completeReward;
            toastSerialized.ApplyModifiedPropertiesWithoutUndo();
            complete.gameObject.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            Debug.Log($"[Shadow Theater] 퀘스트 HUD 생성 완료: {PrefabPath}");
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
