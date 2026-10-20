#if UNITY_EDITOR
using ShadowTheater.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.EditorTools
{
    public static class FieldInventoryPrefabGenerator
    {
        private const string Path = "Assets/Prefabs/UI/FieldInventoryCanvas.prefab";

        [MenuItem("Tools/Shadow Theater/Generate Field Inventory UI")]
        public static void Generate()
        {
            Ensure("Assets", "Prefabs"); Ensure("Assets/Prefabs", "UI");
            var canvasGo = new GameObject("FieldInventoryCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(FieldInventoryController));
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 57;
            var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = .5f;

            var root = Rect("Root", canvasGo.transform, Vector2.zero, Vector2.one);
            root.gameObject.AddComponent<MobileSafeArea>();
            root.gameObject.AddComponent<Image>().color = new Color(.045f, .075f, .14f, .995f);
            var topBand = Rect("TopBand", root, new Vector2(0, .90f), Vector2.one).gameObject.AddComponent<Image>();
            topBand.color = HandheldUiKit.Cyan;
            var title = Text("Header", root, new Vector2(.055f,.925f), new Vector2(.54f,.982f), 46,
                "도구 가방", TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            title.resizeTextForBestFit = true; title.resizeTextMinSize = 28;
            Text("Subtitle", root, new Vector2(.055f,.895f), new Vector2(.52f,.93f), 18,
                "ITEMS & PARTY", TextAnchor.MiddleLeft, new Color(1f,1f,1f,.78f), FontStyle.Bold);
            var goldChip = Panel("GoldChip", root, new Vector2(.53f,.925f), new Vector2(.83f,.98f), HandheldUiKit.Yellow, false);
            var gold = Text("Gold", goldChip, new Vector2(.06f,0), new Vector2(.94f,1), 22,
                "보유 금화", TextAnchor.MiddleRight, HandheldUiKit.Ink, FontStyle.Bold);
            var close = Button("Close", root, new Vector2(.86f,.928f), new Vector2(.955f,.982f), "×", HandheldUiKit.Coral);

            var itemsCard = Panel("ItemsCard", root, new Vector2(.04f,.395f), new Vector2(.485f,.88f), HandheldUiKit.Paper);
            var targetsCard = Panel("TargetsCard", root, new Vector2(.515f,.395f), new Vector2(.96f,.88f), HandheldUiKit.Paper);
            Badge("ItemsBadge", itemsCard, new Vector2(.035f,.89f), new Vector2(.965f,.985f), "◆  보유 도구", HandheldUiKit.Yellow);
            Badge("TargetsBadge", targetsCard, new Vector2(.035f,.89f), new Vector2(.965f,.985f), "●  함께하는 그림자", HandheldUiKit.Mint);
            Scroll("Items", itemsCard, new Vector2(.025f,.025f), new Vector2(.975f,.875f), out var itemContent);
            Scroll("Targets", targetsCard, new Vector2(.025f,.025f), new Vector2(.975f,.875f), out var targetContent);
            var itemTemplate = ItemEntry(itemContent); itemTemplate.gameObject.SetActive(false);
            var targetTemplate = TargetEntry(targetContent); targetTemplate.gameObject.SetActive(false);

            var detail = Panel("SelectionCard", root, new Vector2(.04f,.075f), new Vector2(.96f,.365f), HandheldUiKit.White);
            var stripe = Rect("AccentStripe", detail, new Vector2(0,0), new Vector2(.018f,1)).gameObject.AddComponent<Image>(); stripe.color = HandheldUiKit.Violet;
            Text("SelectionLabel", detail, new Vector2(.045f,.80f), new Vector2(.28f,.96f), 18,
                "현재 선택", TextAnchor.MiddleLeft, HandheldUiKit.Violet, FontStyle.Bold);
            var selectedItem = Text("SelectedItem", detail, new Vector2(.045f,.61f), new Vector2(.48f,.84f), 28,
                "도구를 선택하세요", TextAnchor.MiddleLeft, HandheldUiKit.Ink, FontStyle.Bold);
            var selectedTarget = Text("SelectedTarget", detail, new Vector2(.52f,.61f), new Vector2(.955f,.84f), 23,
                "사용할 그림자를 선택하세요", TextAnchor.MiddleLeft, HandheldUiKit.Ink, FontStyle.Bold);
            var divider = Rect("Divider", detail, new Vector2(.045f,.58f), new Vector2(.955f,.59f)).gameObject.AddComponent<Image>();
            divider.color = new Color(.10f,.14f,.22f,.12f);
            var description = Text("Description", detail, new Vector2(.045f,.23f), new Vector2(.955f,.56f), 20,
                "", TextAnchor.UpperLeft, new Color(.20f,.23f,.30f), FontStyle.Normal);
            var feedback = Text("Feedback", detail, new Vector2(.045f,.035f), new Vector2(.68f,.22f), 21,
                "", TextAnchor.MiddleLeft, HandheldUiKit.Coral, FontStyle.Bold);
            var use = Button("Use", detail, new Vector2(.72f,.055f), new Vector2(.955f,.22f), "사용하기", HandheldUiKit.Coral);

            var controller = canvasGo.GetComponent<FieldInventoryController>(); var so = new SerializedObject(controller);
            Set(so,"root",root.gameObject); Set(so,"itemContent",itemContent); Set(so,"targetContent",targetContent);
            Set(so,"itemTemplate",itemTemplate); Set(so,"targetTemplate",targetTemplate); Set(so,"goldText",gold);
            Set(so,"selectedItemText",selectedItem); Set(so,"selectedTargetText",selectedTarget);
            Set(so,"descriptionText",description); Set(so,"feedbackText",feedback); Set(so,"useButton",use);
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(close.onClick, controller.Close);
            UnityEventTools.AddPersistentListener(use.onClick, controller.UseSelected);
            root.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(canvasGo, Path); Object.DestroyImmediate(canvasGo);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log($"[Shadow Theater] 휴대용 RPG 스타일 도구 가방 UI 생성 완료: {Path}");
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color, bool shadow = true)
        {
            var rect = Rect(name, parent, min, max); var image = rect.gameObject.AddComponent<Image>();
            HandheldUiKit.Panel(image, color, shadow); return rect;
        }

        private static void Badge(string name, Transform parent, Vector2 min, Vector2 max, string label, Color color)
        {
            var rect = Panel(name, parent, min, max, color, false);
            Text("Label", rect, new Vector2(.06f,0), new Vector2(.94f,1), 23, label,
                TextAnchor.MiddleLeft, HandheldUiKit.Ink, FontStyle.Bold);
        }

        private static void Scroll(string name, Transform parent, Vector2 min, Vector2 max, out RectTransform content)
        {
            var root = Rect(name,parent,min,max); var viewport = Rect("Viewport",root,Vector2.zero,Vector2.one);
            viewport.gameObject.AddComponent<Image>().color = new Color(1,1,1,.01f); viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            content = Rect("Content",viewport,new Vector2(0,1),Vector2.one); content.pivot = new Vector2(.5f,1);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.childControlWidth = true; layout.childForceExpandWidth = true;
            layout.childControlHeight = true; layout.childForceExpandHeight = false; layout.padding = new RectOffset(8,8,8,8); layout.spacing = 10;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Elastic; scroll.elasticity = .08f;
        }

        private static InventoryItemEntryView ItemEntry(Transform parent)
        {
            var root = Rect("ItemTemplate",parent,Vector2.zero,Vector2.one); root.gameObject.AddComponent<LayoutElement>().preferredHeight = 116;
            var image = root.gameObject.AddComponent<Image>(); var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            HandheldUiKit.Button(button, HandheldUiKit.White, false);
            var iconPlate = Panel("IconPlate",root,new Vector2(.025f,.13f),new Vector2(.205f,.87f),new Color(.87f,.91f,.96f),false);
            var icon = Rect("Icon",iconPlate,new Vector2(.14f,.14f),new Vector2(.86f,.86f)).gameObject.AddComponent<Image>();
            var name = Text("Name",root,new Vector2(.235f,.44f),new Vector2(.72f,.94f),23,"도구",TextAnchor.MiddleLeft,HandheldUiKit.Ink,FontStyle.Bold);
            var count = Text("Count",root,new Vector2(.73f,.48f),new Vector2(.94f,.94f),22,"x0",TextAnchor.MiddleRight,HandheldUiKit.Violet,FontStyle.Bold);
            var use = Text("UseType",root,new Vector2(.235f,.07f),new Vector2(.94f,.45f),16,"",TextAnchor.MiddleLeft,new Color(.30f,.34f,.42f),FontStyle.Normal);
            var view = root.gameObject.AddComponent<InventoryItemEntryView>(); var so = new SerializedObject(view);
            Set(so,"button",button); Set(so,"icon",icon); Set(so,"nameText",name); Set(so,"countText",count); Set(so,"useText",use);
            so.ApplyModifiedPropertiesWithoutUndo(); return view;
        }

        private static InventoryTargetEntryView TargetEntry(Transform parent)
        {
            var root = Rect("TargetTemplate",parent,Vector2.zero,Vector2.one); root.gameObject.AddComponent<LayoutElement>().preferredHeight = 126;
            var image = root.gameObject.AddComponent<Image>(); var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            HandheldUiKit.Button(button, HandheldUiKit.White, false);
            var plate = Panel("PortraitPlate",root,new Vector2(.02f,.09f),new Vector2(.215f,.91f),new Color(.87f,.91f,.96f),false);
            var portrait = Rect("Portrait",plate,new Vector2(.10f,.08f),new Vector2(.90f,.92f)).gameObject.AddComponent<Image>();
            var name = Text("Name",root,new Vector2(.245f,.52f),new Vector2(.95f,.94f),23,"그림자",TextAnchor.MiddleLeft,HandheldUiKit.Ink,FontStyle.Bold);
            var hp = Text("HP",root,new Vector2(.245f,.18f),new Vector2(.95f,.51f),16,"HP",TextAnchor.MiddleLeft,new Color(.30f,.34f,.42f),FontStyle.Normal);
            var bar = Panel("HPBar",root,new Vector2(.245f,.08f),new Vector2(.95f,.17f),new Color(.12f,.16f,.23f,.18f),false);
            var fill = Rect("Fill",bar,new Vector2(.015f,.16f),new Vector2(.985f,.84f)).gameObject.AddComponent<Image>();
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.color = HandheldUiKit.Mint;
            var view = root.gameObject.AddComponent<InventoryTargetEntryView>(); var so = new SerializedObject(view);
            Set(so,"button",button); Set(so,"portrait",portrait); Set(so,"hpFill",fill); Set(so,"nameText",name); Set(so,"hpText",hp);
            so.ApplyModifiedPropertiesWithoutUndo(); return view;
        }

        private static Button Button(string name, Transform parent, Vector2 min, Vector2 max, string label, Color color)
        {
            var root = Rect(name,parent,min,max); var image = root.gameObject.AddComponent<Image>(); var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image; HandheldUiKit.Button(button,color);
            Text("Label",root,Vector2.zero,Vector2.one,23,label,TextAnchor.MiddleCenter,Color.white,FontStyle.Bold); return button;
        }

        private static Text Text(string name, Transform parent, Vector2 min, Vector2 max, int size,
            string value, TextAnchor anchor, Color color, FontStyle style)
        {
            var text = Rect(name,parent,min,max).gameObject.AddComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size; text.text = value; text.alignment = anchor; text.color = color; text.fontStyle = style; text.raycastTarget = false; return text;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false); var rect = (RectTransform)go.transform;
            rect.anchorMin=min; rect.anchorMax=max; rect.offsetMin=Vector2.zero; rect.offsetMax=Vector2.zero; return rect;
        }

        private static void Set(SerializedObject so,string name,Object value) => so.FindProperty(name).objectReferenceValue=value;
        private static void Ensure(string parent,string name) { if(!AssetDatabase.IsValidFolder(parent+"/"+name)) AssetDatabase.CreateFolder(parent,name); }
    }
}
#endif
