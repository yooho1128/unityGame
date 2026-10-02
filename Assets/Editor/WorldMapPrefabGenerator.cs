#if UNITY_EDITOR
using ShadowTheater.Field;
using ShadowTheater.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.EditorTools
{
    public static class WorldMapPrefabGenerator
    {
        private const string PrefabPath = "Assets/Prefabs/UI/WorldMapCanvas.prefab";

        [MenuItem("Tools/Shadow Theater/Generate World Map UI")]
        public static void Generate()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "UI");
            var canvasGo = new GameObject("WorldMapCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(WorldMapController));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 54;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = .5f;

            var open = Button("OpenWorldMap", canvasGo.transform, new Vector2(.415f,.90f),
                new Vector2(.59f,.955f), "월드맵", new Color(.075f,.18f,.24f,.94f));
            open.gameObject.AddComponent<VirtualWorldMapButton>();
            var root = Rect("WorldMapRoot", canvasGo.transform, Vector2.zero, Vector2.one);
            root.gameObject.AddComponent<Image>().color = new Color(.012f,.009f,.03f,.988f);
            var header = Text("Header", root, new Vector2(.05f,.925f), new Vector2(.70f,.982f),
                44, FontStyle.Bold, TextAnchor.MiddleLeft, "기억의 여정 · 월드맵");
            var progress = Text("Progress", root, new Vector2(.66f,.925f), new Vector2(.84f,.982f),
                23, FontStyle.Bold, TextAnchor.MiddleRight, "방문 0 / 40");
            progress.color = new Color(.66f,.82f,1f,1f);
            var close = Button("Close", root, new Vector2(.865f,.93f), new Vector2(.96f,.98f),
                "×", new Color(.16f,.10f,.25f,1f));

            var listPanel = Rect("RouteList", root, new Vector2(.045f,.43f), new Vector2(.955f,.91f));
            listPanel.gameObject.AddComponent<Image>().color = new Color(.027f,.021f,.055f,1f);
            var scroll = listPanel.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Rect("Viewport", listPanel, Vector2.zero, Vector2.one);
            viewport.gameObject.AddComponent<Image>().color = new Color(1f,1f,1f,.01f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var content = Rect("Content", viewport, new Vector2(0f,1f), Vector2.one);
            content.pivot = new Vector2(.5f,1f);
            content.sizeDelta = new Vector2(0f,120f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10,10,10,10); layout.spacing = 7f;
            layout.childControlHeight = true; layout.childForceExpandHeight = false;
            layout.childControlWidth = true; layout.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            var template = CreateNode(content); template.gameObject.SetActive(false);

            var detail = Rect("Detail", root, new Vector2(.045f,.055f), new Vector2(.955f,.405f));
            detail.gameObject.AddComponent<Image>().color = new Color(.038f,.028f,.074f,1f);
            var accent = Rect("Accent", detail, Vector2.zero, new Vector2(.014f,1f)).gameObject.AddComponent<Image>();
            accent.color = new Color(.46f,.31f,.78f,1f);
            var act = Text("Act", detail, new Vector2(.045f,.82f), new Vector2(.70f,.96f),
                20, FontStyle.Bold, TextAnchor.MiddleLeft, "제1막");
            act.color = new Color(.70f,.58f,.95f,1f);
            var name = Text("Name", detail, new Vector2(.045f,.65f), new Vector2(.70f,.84f),
                36, FontStyle.Bold, TextAnchor.MiddleLeft, "지역을 선택하세요");
            var environment = Text("Environment", detail, new Vector2(.045f,.54f), new Vector2(.70f,.67f),
                21, FontStyle.Normal, TextAnchor.MiddleLeft, "");
            environment.color = new Color(.72f,.69f,.80f,1f);
            var level = Text("Level", detail, new Vector2(.70f,.70f), new Vector2(.95f,.91f),
                21, FontStyle.Bold, TextAnchor.MiddleRight, "");
            var summary = Text("Summary", detail, new Vector2(.045f,.36f), new Vector2(.95f,.55f),
                22, FontStyle.Normal, TextAnchor.UpperLeft, "");
            summary.horizontalOverflow = HorizontalWrapMode.Wrap;
            var shadows = Text("Shadows", detail, new Vector2(.045f,.23f), new Vector2(.95f,.37f),
                19, FontStyle.Normal, TextAnchor.MiddleLeft, "");
            shadows.color = new Color(.73f,.80f,.92f,1f);
            var boss = Text("Boss", detail, new Vector2(.045f,.13f), new Vector2(.64f,.25f),
                20, FontStyle.Bold, TextAnchor.MiddleLeft, "");
            boss.color = new Color(1f,.57f,.68f,1f);
            var feedback = Text("Feedback", detail, new Vector2(.045f,.025f), new Vector2(.66f,.14f),
                17, FontStyle.Normal, TextAnchor.MiddleLeft, "");
            feedback.color = new Color(.72f,.67f,.80f,1f);
            var travel = Button("Travel", detail, new Vector2(.70f,.05f), new Vector2(.95f,.25f),
                "빠른 이동", new Color(.15f,.32f,.39f,1f));

            var controller = canvasGo.GetComponent<WorldMapController>();
            var so = new SerializedObject(controller);
            Set(so,"root",root.gameObject); Set(so,"contentRoot",content); Set(so,"nodeTemplate",template);
            Set(so,"progressText",progress); Set(so,"actText",act); Set(so,"nameText",name);
            Set(so,"environmentText",environment); Set(so,"levelText",level); Set(so,"summaryText",summary);
            Set(so,"shadowsText",shadows); Set(so,"bossText",boss); Set(so,"feedbackText",feedback);
            Set(so,"detailAccent",accent); Set(so,"travelButton",travel);
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(close.onClick, controller.Close);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(travel.onClick, controller.TravelToSelected);
            root.gameObject.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(canvasGo, PrefabPath);
            Object.DestroyImmediate(canvasGo);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            Debug.Log("[Shadow Theater] 40개 지역 월드맵 UI 생성 완료");
        }

        private static RegionMapNodeView CreateNode(Transform parent)
        {
            var root = Rect("RegionNodeTemplate", parent, Vector2.zero, Vector2.one);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 104f;
            var image = root.gameObject.AddComponent<Image>(); image.color = new Color(.057f,.043f,.10f,1f);
            var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var accent = Rect("Accent", root, Vector2.zero, new Vector2(.015f,1f)).gameObject.AddComponent<Image>();
            var order = Text("Order", root, new Vector2(.04f,.12f), new Vector2(.14f,.88f),
                23, FontStyle.Bold, TextAnchor.MiddleCenter, "01");
            order.color = new Color(.62f,.56f,.72f,1f);
            var name = Text("Name", root, new Vector2(.16f,.46f), new Vector2(.64f,.91f),
                27, FontStyle.Bold, TextAnchor.MiddleLeft, "지역");
            var level = Text("Level", root, new Vector2(.16f,.08f), new Vector2(.55f,.49f),
                19, FontStyle.Normal, TextAnchor.MiddleLeft, "Lv.1–5");
            level.color = new Color(.68f,.65f,.75f,1f);
            var state = Text("State", root, new Vector2(.64f,.10f), new Vector2(.95f,.90f),
                21, FontStyle.Bold, TextAnchor.MiddleRight, "잠김");
            var view = root.gameObject.AddComponent<RegionMapNodeView>();
            var so = new SerializedObject(view);
            Set(so,"button",button); Set(so,"accent",accent); Set(so,"orderText",order);
            Set(so,"nameText",name); Set(so,"levelText",level); Set(so,"stateText",state);
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent,false);
            var rect = (RectTransform)go.transform; rect.anchorMin=min; rect.anchorMax=max;
            rect.offsetMin=Vector2.zero; rect.offsetMax=Vector2.zero; return rect;
        }

        private static Text Text(string name, Transform parent, Vector2 min, Vector2 max, int size,
            FontStyle style, TextAnchor anchor, string value)
        {
            var text = Rect(name,parent,min,max).gameObject.AddComponent<Text>();
            text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize=size;
            text.fontStyle=style; text.alignment=anchor; text.color=new Color(.96f,.93f,1f,1f);
            text.raycastTarget=false; text.text=value; return text;
        }

        private static Button Button(string name, Transform parent, Vector2 min, Vector2 max,
            string label, Color color)
        {
            var root=Rect(name,parent,min,max); var image=root.gameObject.AddComponent<Image>(); image.color=color;
            var button=root.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            Text("Label",root,Vector2.zero,Vector2.one,24,FontStyle.Bold,TextAnchor.MiddleCenter,label);
            return button;
        }

        private static void Set(SerializedObject so,string property,Object value) =>
            so.FindProperty(property).objectReferenceValue=value;

        private static void EnsureFolder(string parent,string name)
        {
            string path=parent+"/"+name;
            if(!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent,name);
        }
    }
}
#endif
