#if UNITY_EDITOR
using ShadowTheater.Field;
using ShadowTheater.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.EditorTools
{
    public static class SettlementShopPrefabGenerator
    {
        private const string PrefabPath = "Assets/Prefabs/UI/SettlementShopCanvas.prefab";

        [MenuItem("Tools/Shadow Theater/Generate Settlement Shop UI")]
        public static void Generate()
        {
            Ensure("Assets", "Prefabs"); Ensure("Assets/Prefabs", "UI");
            var canvasGo = new GameObject("SettlementShopCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(SettlementShopController));
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 56;
            var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080,1920); scaler.matchWidthOrHeight = .5f;

            var open = Button("OpenShop", canvasGo.transform, new Vector2(.605f,.90f), new Vector2(.78f,.955f),
                "상점", new Color(.20f,.13f,.08f,.94f));
            open.gameObject.AddComponent<VirtualShopButton>();
            var root = Rect("Root", canvasGo.transform, Vector2.zero, Vector2.one);
            root.gameObject.AddComponent<Image>().color = new Color(.012f,.008f,.027f,.99f);
            var card = Rect("Card", root, new Vector2(.045f,.06f), new Vector2(.955f,.95f));
            card.gameObject.AddComponent<Image>().color = new Color(.035f,.023f,.062f,1f);
            var region = Text("Region", card, new Vector2(.055f,.90f), new Vector2(.68f,.975f), 40, "기억 상점", TextAnchor.MiddleLeft);
            var gold = Text("Gold", card, new Vector2(.60f,.90f), new Vector2(.84f,.975f), 24, "보유 금화  0", TextAnchor.MiddleRight);
            gold.color = new Color(1f,.83f,.42f);
            var close = Button("Close", card, new Vector2(.865f,.915f), new Vector2(.955f,.972f), "×", new Color(.15f,.09f,.23f));
            var guide = Text("Guide", card, new Vector2(.055f,.845f), new Vector2(.945f,.90f), 20,
                "여정에 필요한 도구를 사고팝니다. 회복 시약은 파티 화면에서도 사용할 수 있습니다.", TextAnchor.MiddleLeft);
            guide.color = new Color(.75f,.70f,.83f);

            var listPanel = Rect("List", card, new Vector2(.055f,.17f), new Vector2(.945f,.835f));
            listPanel.gameObject.AddComponent<Image>().color = new Color(.022f,.016f,.045f);
            var viewport = Rect("Viewport", listPanel, Vector2.zero, Vector2.one);
            viewport.gameObject.AddComponent<Image>().color = new Color(1,1,1,.01f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var content = Rect("Content", viewport, new Vector2(0,1), Vector2.one); content.pivot = new Vector2(.5f,1);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(10,10,10,10);
            layout.spacing = 8; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = listPanel.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            var template = Entry(content); template.gameObject.SetActive(false);
            var feedback = Text("Feedback", card, new Vector2(.055f,.07f), new Vector2(.69f,.15f), 21, "", TextAnchor.MiddleLeft);
            var rest = Button("Rest", card, new Vector2(.71f,.075f), new Vector2(.945f,.15f), "막간 휴식 30", new Color(.18f,.20f,.36f));

            var controller = canvasGo.GetComponent<SettlementShopController>();
            var so = new SerializedObject(controller);
            Set(so,"root",root.gameObject); Set(so,"openButton",open); Set(so,"contentRoot",content);
            Set(so,"itemTemplate",template); Set(so,"regionText",region); Set(so,"goldText",gold); Set(so,"feedbackText",feedback);
            Set(so,"restButton",rest); so.FindProperty("restCost").intValue=30;
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(close.onClick, controller.Close);
            UnityEventTools.AddPersistentListener(rest.onClick, controller.RestParty);
            root.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(canvasGo, PrefabPath);
            Object.DestroyImmediate(canvasGo);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log($"[Shadow Theater] 정착지 상점 UI 생성 완료: {PrefabPath}");
        }

        private static ShopItemEntryView Entry(Transform parent)
        {
            var root = Rect("ItemTemplate", parent, Vector2.zero, Vector2.one);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 150;
            root.gameObject.AddComponent<Image>().color = new Color(.065f,.045f,.105f);
            var name = Text("Name", root, new Vector2(.025f,.55f), new Vector2(.46f,.94f), 28, "도구", TextAnchor.MiddleLeft);
            var description = Text("Description", root, new Vector2(.025f,.08f), new Vector2(.54f,.58f), 18, "설명", TextAnchor.UpperLeft);
            var owned = Text("Owned", root, new Vector2(.48f,.57f), new Vector2(.64f,.91f), 20, "보유 0", TextAnchor.MiddleRight);
            var buy = Button("Buy", root, new Vector2(.67f,.53f), new Vector2(.965f,.91f), "구매", new Color(.15f,.30f,.25f));
            var sell = Button("Sell", root, new Vector2(.67f,.09f), new Vector2(.965f,.47f), "판매", new Color(.25f,.13f,.24f));
            var buyText = buy.transform.Find("Label").GetComponent<Text>();
            var sellText = sell.transform.Find("Label").GetComponent<Text>();
            var view = root.gameObject.AddComponent<ShopItemEntryView>(); var so = new SerializedObject(view);
            Set(so,"nameText",name); Set(so,"descriptionText",description); Set(so,"ownedText",owned);
            Set(so,"buyPriceText",buyText); Set(so,"sellPriceText",sellText); Set(so,"buyButton",buy); Set(so,"sellButton",sell);
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static Button Button(string name, Transform parent, Vector2 min, Vector2 max, string label, Color color)
        {
            var rect=Rect(name,parent,min,max); var image=rect.gameObject.AddComponent<Image>(); image.color=color;
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            Text("Label",rect,Vector2.zero,Vector2.one,22,label,TextAnchor.MiddleCenter); return button;
        }
        private static Text Text(string name, Transform parent, Vector2 min, Vector2 max, int size, string value, TextAnchor align)
        {
            var rect=Rect(name,parent,min,max); var text=rect.gameObject.AddComponent<Text>();
            text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize=size; text.text=value;
            text.alignment=align; text.color=new Color(.96f,.93f,1f); text.raycastTarget=false; return text;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false); var rect=(RectTransform)go.transform;
            rect.anchorMin=min; rect.anchorMax=max; rect.offsetMin=Vector2.zero; rect.offsetMax=Vector2.zero; return rect;
        }
        private static void Set(SerializedObject so,string name,Object value)=>so.FindProperty(name).objectReferenceValue=value;
        private static void Ensure(string parent,string name){if(!AssetDatabase.IsValidFolder(parent+"/"+name))AssetDatabase.CreateFolder(parent,name);}
    }
}
#endif
