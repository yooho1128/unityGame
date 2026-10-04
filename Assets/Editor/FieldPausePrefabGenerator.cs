#if UNITY_EDITOR
using ShadowTheater.Field;
using ShadowTheater.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.EditorTools
{
    /// <summary>모든 필드에서 공유하는 모바일 메뉴 프리팹을 생성한다.</summary>
    public static class FieldPausePrefabGenerator
    {
        private const string Path = "Assets/Prefabs/UI/FieldPauseCanvas.prefab";

        [MenuItem("Tools/Shadow Theater/Generate Field Pause Menu")]
        public static void Generate()
        {
            Ensure("Assets", "Prefabs"); Ensure("Assets/Prefabs", "UI");
            var canvasRoot = new GameObject("FieldPauseCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(FieldPauseMenuController), typeof(SaveFeedbackController));
            var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 74;
            var scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = .5f;

            var open = Button("OpenMenu", canvasRoot.transform, new Vector2(.84f,.90f), new Vector2(.97f,.955f), "메뉴", Accent());
            open.gameObject.AddComponent<VirtualPauseButton>();

            var saveToast = Rect("SaveToast", canvasRoot.transform, new Vector2(.60f,.835f), new Vector2(.97f,.89f));
            saveToast.gameObject.AddComponent<Image>().color = new Color(.035f,.022f,.075f,.94f);
            var saveToastGroup = saveToast.gameObject.AddComponent<CanvasGroup>();
            var saveToastLabel = Text("Label", saveToast, new Vector2(.06f,0), new Vector2(.96f,1), 21,
                "기억을 기록했습니다", TextAnchor.MiddleCenter);

            var root = Rect("Root", canvasRoot.transform, Vector2.zero, Vector2.one);
            root.gameObject.AddComponent<Image>().color = new Color(.006f,.004f,.018f,.94f);
            var card = Rect("Card", root, new Vector2(.055f,.075f), new Vector2(.945f,.925f));
            card.gameObject.AddComponent<Image>().color = new Color(.035f,.022f,.075f,.99f);
            Text("Header", card, new Vector2(.07f,.91f), new Vector2(.78f,.98f), 44, "공연 기록", TextAnchor.MiddleLeft);
            var close = Button("Close", card, new Vector2(.86f,.92f), new Vector2(.96f,.975f), "×", Dark());

            var region = Text("Region", card, new Vector2(.07f,.835f), new Vector2(.93f,.89f), 25, "현재 지역", TextAnchor.MiddleLeft);
            var playTime = Text("PlayTime", card, new Vector2(.07f,.785f), new Vector2(.62f,.835f), 22, "플레이 시간", TextAnchor.MiddleLeft);
            var gold = Text("Gold", card, new Vector2(.62f,.785f), new Vector2(.93f,.835f), 22, "보유 금화", TextAnchor.MiddleRight);

            Text("AudioHeader", card, new Vector2(.07f,.71f), new Vector2(.93f,.76f), 27, "음향 설정", TextAnchor.MiddleLeft);
            CreateSliderRow(card, .64f, "전체 음량", out var master, out var masterValue);
            CreateSliderRow(card, .55f, "음악", out var music, out var musicValue);
            CreateSliderRow(card, .46f, "환경음", out var ambience, out var ambienceValue);
            CreateSliderRow(card, .37f, "효과음", out var sfx, out var sfxValue);

            var feedback = Text("Feedback", card, new Vector2(.07f,.30f), new Vector2(.93f,.35f), 21, "", TextAnchor.MiddleCenter);
            feedback.color = new Color(.55f,1f,.84f);
            var party = Button("Party", card, new Vector2(.07f,.21f), new Vector2(.48f,.285f), "파티 · 서고", new Color(.18f,.12f,.34f));
            var inventory = Button("Inventory", card, new Vector2(.52f,.21f), new Vector2(.93f,.285f), "도구 가방", new Color(.16f,.18f,.31f));
            var save = Button("Save", card, new Vector2(.07f,.09f), new Vector2(.48f,.18f), "진행 상황 저장", new Color(.13f,.30f,.29f));
            var title = Button("ReturnTitle", card, new Vector2(.52f,.09f), new Vector2(.93f,.18f), "타이틀로 돌아가기", new Color(.29f,.10f,.19f));

            var confirm = Rect("TitleConfirm", root, new Vector2(.10f,.35f), new Vector2(.90f,.65f));
            confirm.gameObject.AddComponent<Image>().color = new Color(.045f,.025f,.085f,1f);
            Text("Message", confirm, new Vector2(.08f,.55f), new Vector2(.92f,.90f), 28,
                "타이틀로 돌아가시겠습니까?\n현재 진행 상황은 저장됩니다.", TextAnchor.MiddleCenter);
            var cancel = Button("Cancel", confirm, new Vector2(.08f,.13f), new Vector2(.47f,.39f), "취소", Dark());
            var confirmButton = Button("Confirm", confirm, new Vector2(.53f,.13f), new Vector2(.92f,.39f), "돌아가기", new Color(.34f,.10f,.20f));

            var controller = canvasRoot.GetComponent<FieldPauseMenuController>();
            var so = new SerializedObject(controller);
            Set(so,"root",root.gameObject); Set(so,"titleConfirmRoot",confirm.gameObject);
            Set(so,"regionText",region); Set(so,"playTimeText",playTime); Set(so,"goldText",gold); Set(so,"feedbackText",feedback);
            Set(so,"masterSlider",master); Set(so,"musicSlider",music); Set(so,"ambienceSlider",ambience); Set(so,"sfxSlider",sfx);
            Set(so,"masterValue",masterValue); Set(so,"musicValue",musicValue); Set(so,"ambienceValue",ambienceValue); Set(so,"sfxValue",sfxValue);
            so.ApplyModifiedPropertiesWithoutUndo();

            var toastSo = new SerializedObject(canvasRoot.GetComponent<SaveFeedbackController>());
            Set(toastSo,"root",saveToastGroup); Set(toastSo,"label",saveToastLabel);
            toastSo.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddPersistentListener(close.onClick, controller.Close);
            UnityEventTools.AddPersistentListener(party.onClick, controller.OpenPartyManagement);
            UnityEventTools.AddPersistentListener(inventory.onClick, controller.OpenInventory);
            UnityEventTools.AddPersistentListener(save.onClick, controller.SaveNow);
            UnityEventTools.AddPersistentListener(title.onClick, controller.RequestReturnToTitle);
            UnityEventTools.AddPersistentListener(cancel.onClick, controller.CancelReturnToTitle);
            UnityEventTools.AddPersistentListener(confirmButton.onClick, controller.ConfirmReturnToTitle);
            UnityEventTools.AddPersistentListener(master.onValueChanged, controller.SetMaster);
            UnityEventTools.AddPersistentListener(music.onValueChanged, controller.SetMusic);
            UnityEventTools.AddPersistentListener(ambience.onValueChanged, controller.SetAmbience);
            UnityEventTools.AddPersistentListener(sfx.onValueChanged, controller.SetSfx);

            confirm.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(canvasRoot, Path);
            Object.DestroyImmediate(canvasRoot);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log($"[Shadow Theater] 필드 메뉴 생성 완료: {Path}");
        }

        private static void CreateSliderRow(Transform parent, float y, string label, out Slider slider, out Text value)
        {
            Text(label + "Label", parent, new Vector2(.07f,y), new Vector2(.31f,y+.065f), 23, label, TextAnchor.MiddleLeft);
            slider = CreateSlider(label + "Slider", parent, new Vector2(.32f,y+.012f), new Vector2(.79f,y+.053f));
            value = Text(label + "Value", parent, new Vector2(.81f,y), new Vector2(.93f,y+.065f), 22, "100%", TextAnchor.MiddleRight);
        }

        private static Slider CreateSlider(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var root = Rect(name, parent, min, max);
            var background = root.gameObject.AddComponent<Image>(); background.color = new Color(.10f,.075f,.16f);
            var fillArea = Rect("FillArea", root, new Vector2(.02f,.18f), new Vector2(.98f,.82f));
            var fill = Rect("Fill", fillArea, Vector2.zero, Vector2.one); fill.gameObject.AddComponent<Image>().color = new Color(.54f,.32f,.86f);
            var handleArea = Rect("HandleArea", root, new Vector2(.02f,0), new Vector2(.98f,1));
            var handle = Rect("Handle", handleArea, new Vector2(.5f,.5f), new Vector2(.5f,.5f)); handle.sizeDelta = new Vector2(30,56);
            var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = new Color(.92f,.82f,1f);
            var slider = root.gameObject.AddComponent<Slider>(); slider.fillRect = fill; slider.handleRect = handle;
            slider.targetGraphic = handleImage; slider.direction = Slider.Direction.LeftToRight; slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
            return slider;
        }

        private static Button Button(string name, Transform parent, Vector2 min, Vector2 max, string label, Color color)
        {
            var rect = Rect(name,parent,min,max); var image = rect.gameObject.AddComponent<Image>(); image.color = color;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            Text("Label",rect,Vector2.zero,Vector2.one,25,label,TextAnchor.MiddleCenter);
            return button;
        }

        private static Text Text(string name, Transform parent, Vector2 min, Vector2 max, int size, string value, TextAnchor align)
        {
            var rect = Rect(name,parent,min,max); var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size; text.text = value;
            text.alignment = align; text.color = new Color(.96f,.93f,1f); text.raycastTarget = false;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false); var rect = (RectTransform)go.transform;
            rect.anchorMin=min; rect.anchorMax=max; rect.offsetMin=Vector2.zero; rect.offsetMax=Vector2.zero; return rect;
        }

        private static Color Accent() => new Color(.18f,.10f,.34f,.94f);
        private static Color Dark() => new Color(.10f,.07f,.17f);
        private static void Set(SerializedObject so,string name,Object value) => so.FindProperty(name).objectReferenceValue=value;
        private static void Ensure(string parent,string name) { if(!AssetDatabase.IsValidFolder(parent+"/"+name)) AssetDatabase.CreateFolder(parent,name); }
    }
}
#endif
