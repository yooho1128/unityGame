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
            Ensure("Assets","Prefabs"); Ensure("Assets/Prefabs","UI");
            var canvasGo=new GameObject("FieldInventoryCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(FieldInventoryController));
            var canvas=canvasGo.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=57;
            var scaler=canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1080,1920); scaler.matchWidthOrHeight=.5f;
            var root=Rect("Root",canvasGo.transform,Vector2.zero,Vector2.one); root.gameObject.AddComponent<Image>().color=new Color(.012f,.008f,.028f,.99f);
            Text("Header",root,new Vector2(.05f,.925f),new Vector2(.55f,.985f),44,"도구 가방",TextAnchor.MiddleLeft);
            var gold=Text("Gold",root,new Vector2(.55f,.93f),new Vector2(.83f,.98f),22,"보유 금화",TextAnchor.MiddleRight); gold.color=new Color(1f,.83f,.42f);
            var close=Button("Close",root,new Vector2(.86f,.93f),new Vector2(.96f,.98f),"×",new Color(.16f,.10f,.25f));
            Text("ItemsLabel",root,new Vector2(.05f,.855f),new Vector2(.47f,.915f),25,"보유 도구",TextAnchor.MiddleLeft);
            Text("TargetsLabel",root,new Vector2(.53f,.855f),new Vector2(.95f,.915f),25,"파티",TextAnchor.MiddleLeft);
            var items=Scroll("Items",root,new Vector2(.05f,.38f),new Vector2(.47f,.85f),out var itemContent);
            var targets=Scroll("Targets",root,new Vector2(.53f,.38f),new Vector2(.95f,.85f),out var targetContent);
            var itemTemplate=ItemEntry(itemContent); itemTemplate.gameObject.SetActive(false);
            var targetTemplate=TargetEntry(targetContent); targetTemplate.gameObject.SetActive(false);
            var selectedItem=Text("SelectedItem",root,new Vector2(.05f,.30f),new Vector2(.47f,.37f),27,"도구를 선택하세요",TextAnchor.MiddleLeft);
            var selectedTarget=Text("SelectedTarget",root,new Vector2(.53f,.30f),new Vector2(.95f,.37f),24,"사용할 그림자를 선택하세요",TextAnchor.MiddleLeft);
            var description=Text("Description",root,new Vector2(.05f,.19f),new Vector2(.95f,.29f),20,"",TextAnchor.UpperLeft);
            var feedback=Text("Feedback",root,new Vector2(.05f,.10f),new Vector2(.69f,.18f),21,"",TextAnchor.MiddleLeft);
            var use=Button("Use",root,new Vector2(.72f,.105f),new Vector2(.95f,.18f),"사용",new Color(.14f,.30f,.27f));
            var ctrl=canvasGo.GetComponent<FieldInventoryController>(); var so=new SerializedObject(ctrl);
            Set(so,"root",root.gameObject);Set(so,"itemContent",itemContent);Set(so,"targetContent",targetContent);Set(so,"itemTemplate",itemTemplate);Set(so,"targetTemplate",targetTemplate);
            Set(so,"goldText",gold);Set(so,"selectedItemText",selectedItem);Set(so,"selectedTargetText",selectedTarget);Set(so,"descriptionText",description);Set(so,"feedbackText",feedback);Set(so,"useButton",use);so.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(close.onClick,ctrl.Close);UnityEventTools.AddPersistentListener(use.onClick,ctrl.UseSelected);
            root.gameObject.SetActive(false);PrefabUtility.SaveAsPrefabAsset(canvasGo,Path);Object.DestroyImmediate(canvasGo);AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log($"[Shadow Theater] 필드 도구 가방 UI 생성 완료: {Path}");
        }
        private static ScrollRect Scroll(string n,Transform p,Vector2 min,Vector2 max,out RectTransform content){var r=Rect(n,p,min,max);r.gameObject.AddComponent<Image>().color=new Color(.028f,.02f,.055f);var v=Rect("Viewport",r,Vector2.zero,Vector2.one);v.gameObject.AddComponent<Image>().color=new Color(1,1,1,.01f);v.gameObject.AddComponent<Mask>().showMaskGraphic=false;content=Rect("Content",v,new Vector2(0,1),Vector2.one);content.pivot=new Vector2(.5f,1);var l=content.gameObject.AddComponent<VerticalLayoutGroup>();l.padding=new RectOffset(8,8,8,8);l.spacing=7;l.childControlHeight=true;l.childForceExpandHeight=false;content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;var s=r.gameObject.AddComponent<ScrollRect>();s.viewport=v;s.content=content;s.horizontal=false;return s;}
        private static InventoryItemEntryView ItemEntry(Transform p){var r=Rect("ItemTemplate",p,Vector2.zero,Vector2.one);r.gameObject.AddComponent<LayoutElement>().preferredHeight=108;var i=r.gameObject.AddComponent<Image>();i.color=new Color(.07f,.05f,.12f);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=i;var name=Text("Name",r,new Vector2(.04f,.44f),new Vector2(.72f,.94f),24,"도구",TextAnchor.MiddleLeft);var count=Text("Count",r,new Vector2(.73f,.48f),new Vector2(.95f,.94f),23,"x0",TextAnchor.MiddleRight);var use=Text("UseType",r,new Vector2(.04f,.07f),new Vector2(.95f,.45f),17,"",TextAnchor.MiddleLeft);use.color=new Color(.68f,.62f,.78f);var view=r.gameObject.AddComponent<InventoryItemEntryView>();var so=new SerializedObject(view);Set(so,"button",b);Set(so,"nameText",name);Set(so,"countText",count);Set(so,"useText",use);so.ApplyModifiedPropertiesWithoutUndo();return view;}
        private static InventoryTargetEntryView TargetEntry(Transform p){var r=Rect("TargetTemplate",p,Vector2.zero,Vector2.one);r.gameObject.AddComponent<LayoutElement>().preferredHeight=118;var i=r.gameObject.AddComponent<Image>();i.color=new Color(.07f,.05f,.12f);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=i;var portrait=Rect("Portrait",r,new Vector2(.02f,.08f),new Vector2(.20f,.92f)).gameObject.AddComponent<Image>();var name=Text("Name",r,new Vector2(.23f,.52f),new Vector2(.96f,.94f),23,"그림자",TextAnchor.MiddleLeft);var hp=Text("HP",r,new Vector2(.23f,.12f),new Vector2(.96f,.49f),17,"HP",TextAnchor.MiddleLeft);var bar=Rect("HPBar",r,new Vector2(.23f,.04f),new Vector2(.96f,.12f));bar.gameObject.AddComponent<Image>().color=new Color(.12f,.1f,.18f);var fill=Rect("Fill",bar,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>();fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;fill.color=new Color(.35f,.9f,.65f);var view=r.gameObject.AddComponent<InventoryTargetEntryView>();var so=new SerializedObject(view);Set(so,"button",b);Set(so,"portrait",portrait);Set(so,"hpFill",fill);Set(so,"nameText",name);Set(so,"hpText",hp);so.ApplyModifiedPropertiesWithoutUndo();return view;}
        private static Button Button(string n,Transform p,Vector2 min,Vector2 max,string label,Color c){var r=Rect(n,p,min,max);var i=r.gameObject.AddComponent<Image>();i.color=c;var b=r.gameObject.AddComponent<Button>();b.targetGraphic=i;Text("Label",r,Vector2.zero,Vector2.one,23,label,TextAnchor.MiddleCenter);return b;}
        private static Text Text(string n,Transform p,Vector2 min,Vector2 max,int size,string value,TextAnchor a){var r=Rect(n,p,min,max);var t=r.gameObject.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.text=value;t.alignment=a;t.color=new Color(.96f,.93f,1);t.raycastTarget=false;return t;}
        private static RectTransform Rect(string n,Transform p,Vector2 min,Vector2 max){var g=new GameObject(n,typeof(RectTransform));g.transform.SetParent(p,false);var r=(RectTransform)g.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;return r;}
        private static void Set(SerializedObject s,string n,Object v)=>s.FindProperty(n).objectReferenceValue=v;private static void Ensure(string p,string n){if(!AssetDatabase.IsValidFolder(p+"/"+n))AssetDatabase.CreateFolder(p,n);}
    }
}
#endif
