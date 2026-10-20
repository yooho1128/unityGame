#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using ShadowTheater.Data;
using ShadowTheater.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ShadowTheater.EditorTools
{
    public static class ReleasePreviewExporter
    {
        public const string Folder = "Docs/Generated/ReleasePreviews";
        public static void Capture()
        {
            Directory.CreateDirectory(Folder);
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            var qualityPipeline = QualitySettings.renderPipeline;
            try
            {
                GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
                CaptureBattle("Ember", "ash_king");
                CaptureBattle("Archive", "infinite_archive_dragon");
                CaptureBattle("Cosmos", "rival_director");
            }
            finally { GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = qualityPipeline; }
            Debug.Log("[ReleasePreview] 실제 전투 프리팹 1920×1080 미리보기 3장 저장 · " + Folder);
        }

        private static void CaptureBattle(string theme, string enemyId)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("PreviewCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var render = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            camera.targetTexture = render;
            GameObject root = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/BattleCanvas.prefab")) as GameObject;
            if (root == null) throw new InvalidOperationException("BattleCanvas 누락");
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(behaviour is Graphic)) behaviour.enabled = false;
            Canvas canvas = root.GetComponent<Canvas>(); canvas.enabled = true;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.enabled = false; canvas.scaleFactor = 1;
            Image backdrop = root.GetComponentsInChildren<Image>(true).First(x => x.name == "ReleaseStageArt");
            backdrop.sprite = Resources.Load<Sprite>("Art/BattleBackgrounds/" + theme); backdrop.color = Color.white;
            var controller = new SerializedObject(root.GetComponent<BattleUIController>());
            foreach (string field in new[] { "optionRoot", "resultRoot", "tutorialRoot" })
                (controller.FindProperty(field)?.objectReferenceValue as GameObject)?.SetActive(false);
            (controller.FindProperty("actionRoot")?.objectReferenceValue as GameObject)?.SetActive(true);
            SetText(controller, "turnText", "TURN 01"); SetText(controller, "fpText", "FP 5 / 5\n열연 2 / 3");
            SetText(controller, "messageText", "기억을 되찾기 위한 무대가 열립니다.");
            foreach (BattleUnitPanel panel in root.GetComponentsInChildren<BattleUnitPanel>(true))
            {
                string id = panel.name == "Enemy" ? enemyId : "knight";
                ShadowData data = AssetDatabase.LoadAssetAtPath<ShadowData>($"Assets/Data/Generated/Shadows/{id}.asset");
                panel.Portrait.sprite = data.silhouetteSprite; panel.Portrait.color = Color.white; panel.Portrait.preserveAspect = true;
                var serialized = new SerializedObject(panel);
                SetText(serialized, "nameText", data.displayName); SetText(serialized, "levelText", "Lv.50");
                SetText(serialized, "hpText", "824 / 1020"); SetText(serialized, "statusText", "");
                var hpFill = serialized.FindProperty("hpFill").objectReferenceValue as Image;
                hpFill.fillAmount = .81f; hpFill.color = new Color(.35f, .92f, .65f);
                (serialized.FindProperty("accent").objectReferenceValue as Image).color = data.accentColor;
            }
            // Hide unbound party placeholders in the static preview.
            foreach (BattlePartySlot slot in root.GetComponentsInChildren<BattlePartySlot>(true))
                slot.gameObject.SetActive(false);
            Font font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 28);
            foreach (Text label in root.GetComponentsInChildren<Text>(true))
            {
                label.font = font;
                font.RequestCharactersInTexture(label.text, label.fontSize, label.fontStyle);
            }
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform); Canvas.ForceUpdateCanvases();
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.Render(); RenderTexture.active = render;
                var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
                File.WriteAllBytes($"{Folder}/Battle_{theme}.png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null; render.Release();
                UnityEngine.Object.DestroyImmediate(render); UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
        private static void SetText(SerializedObject target, string field, string value)
        { if (target.FindProperty(field)?.objectReferenceValue is Text label) label.text = value; }
    }
}
#endif
