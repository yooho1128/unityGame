#if UNITY_EDITOR
using ShadowTheater.Field;
using ShadowTheater.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.EditorTools
{
    public static class PartyStoragePrefabGenerator
    {
        [MenuItem("Tools/Shadow Theater/Generate Party and Storage UI")]
        public static void Generate()
        {
            Ensure("Assets","Prefabs"); Ensure("Assets/Prefabs","UI");
            var root=new GameObject("PartyStorageCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(PartyStorageController));
            var c=root.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=54;
            var sc=root.GetComponent<CanvasScaler>();sc.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;sc.referenceResolution=new Vector2(1080,1920);sc.matchWidthOrHeight=.5f;
            var open=Btn("OpenParty",root.transform,new Vector2(.22f,.90f),new Vector2(.39f,.955f),"파티");open.gameObject.AddComponent<VirtualPartyButton>();
            var panel=Rect("Root",root.transform,Vector2.zero,Vector2.one);panel.gameObject.AddComponent<Image>().color=new Color(.015f,.01f,.035f,.99f);
            Text("Header",panel,new Vector2(.05f,.92f),new Vector2(.75f,.985f),44,"파티 · 각본 서고",TextAnchor.MiddleLeft);
            var close=Btn("Close",panel,new Vector2(.86f,.93f),new Vector2(.96f,.98f),"×");
            var selected=Text("Selected",panel,new Vector2(.05f,.84f),new Vector2(.65f,.91f),28,"그림자를 선택하세요",TextAnchor.MiddleLeft);
            var feedback=Text("Feedback",panel,new Vector2(.05f,.79f),new Vector2(.95f,.84f),20,"",TextAnchor.MiddleLeft);
            var partyLabel=Text("PartyCount",panel,new Vector2(.05f,.73f),new Vector2(.5f,.79f),27,"파티",TextAnchor.MiddleLeft);
            Scroll("Party",panel,new Vector2(.05f,.46f),new Vector2(.95f,.73f),out var partyContent,out var partyTemplate);
            var storageLabel=Text("StorageCount",panel,new Vector2(.05f,.39f),new Vector2(.5f,.45f),27,"각본 서고",TextAnchor.MiddleLeft);
            Scroll("Storage",panel,new Vector2(.05f,.12f),new Vector2(.95f,.39f),out var storageContent,out var storageTemplate);
            var toParty=Btn("ToParty",panel,new Vector2(.05f,.04f),new Vector2(.27f,.10f),"파티로");
            var toStorage=Btn("ToStorage",panel,new Vector2(.29f,.04f),new Vector2(.52f,.10f),"서고로");
            var up=Btn("Up",panel,new Vector2(.56f,.04f),new Vector2(.74f,.10f),"위로");
            var down=Btn("Down",panel,new Vector2(.76f,.04f),new Vector2(.95f,.10f),"아래로");
            var ctrl=root.GetComponent<PartyStorageController>();var so=new SerializedObject(ctrl);
            Set(so,"root",panel.gameObject);Set(so,"partyContent",partyContent);Set(so,"storageContent",storageContent);Set(so,"partyTemplate",partyTemplate);Set(so,"storageTemplate",storageTemplate);
            Set(so,"partyCountText",partyLabel);Set(so,"storageCountText",storageLabel);Set(so,"selectedText",selected);Set(so,"feedbackText",feedback);
            Set(so,"toPartyButton",toParty);Set(so,"toStorageButton",toStorage);Set(so,"upButton",up);Set(so,"downButton",down);so.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(close.onClick,ctrl.Close);UnityEditor.Events.UnityEventTools.AddPersistentListener(toParty.onClick,ctrl.MoveSelectedToParty);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(toStorage.onClick,ctrl.MoveSelectedToStorage);UnityEditor.Events.UnityEventTools.AddPersistentListener(up.onClick,ctrl.MoveSelectedUp);UnityEditor.Events.UnityEventTools.AddPersistentListener(down.onClick,ctrl.MoveSelectedDown);
            panel.gameObject.SetActive(false);string path="Assets/Prefabs/UI/PartyStorageCanvas.prefab";PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);AssetDatabase.SaveAssets();AssetDatabase.Refresh();EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }
        private static ScrollRect Scroll(string n,Transform p,Vector2 min,Vector2 max,out RectTransform content,out PartyStorageEntryView template){var r=Rect(n,p,min,max);r.gameObject.AddComponent<Image>().color=new Color(.035f,.025f,.07f);var v=Rect("Viewport",r,Vector2.zero,Vector2.one);v.gameObject.AddComponent<Image>().color=new Color(1,1,1,.01f);v.gameObject.AddComponent<Mask>().showMaskGraphic=false;content=Rect("Content",v,new Vector2(0,1),Vector2.one);content.pivot=new Vector2(.5f,1);var l=content.gameObject.AddComponent<VerticalLayoutGroup>();l.spacing=6;l.childControlHeight=true;l.childForceExpandHeight=false;content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;template=Entry(content);template.gameObject.SetActive(false);var s=r.gameObject.AddComponent<ScrollRect>();s.viewport=v;s.content=content;s.horizontal=false;return s;}
        private static PartyStorageEntryView Entry(Transform p)
        {
            var r=Rect("Template",p,Vector2.zero,Vector2.one);r.gameObject.AddComponent<LayoutElement>().preferredHeight=112;
            var im=r.gameObject.AddComponent<Image>();im.color=new Color(.07f,.05f,.12f);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=im;
            var portrait=Rect("Portrait",r,new Vector2(.02f,.06f),new Vector2(.15f,.94f)).gameObject.AddComponent<Image>();
            var name=Text("Name",r,new Vector2(.18f,.52f),new Vector2(.60f,.94f),27,"",TextAnchor.MiddleLeft);
            var info=Text("Info",r,new Vector2(.18f,.08f),new Vector2(.62f,.54f),20,"",TextAnchor.MiddleLeft);
            var stage=Text("Stage",r,new Vector2(.64f,.58f),new Vector2(.97f,.94f),20,"",TextAnchor.MiddleRight);
            var hpBar=Rect("HP",r,new Vector2(.64f,.39f),new Vector2(.97f,.53f));hpBar.gameObject.AddComponent<Image>().color=new Color(.12f,.1f,.18f);
            var hpFill=Rect("Fill",hpBar,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>();hpFill.type=Image.Type.Filled;hpFill.fillMethod=Image.FillMethod.Horizontal;hpFill.color=new Color(.35f,.9f,.65f);
            var expText=Text("ExpText",r,new Vector2(.64f,.17f),new Vector2(.97f,.36f),16,"",TextAnchor.MiddleRight);
            var expBar=Rect("EXP",r,new Vector2(.64f,.06f),new Vector2(.97f,.15f));expBar.gameObject.AddComponent<Image>().color=new Color(.10f,.08f,.16f);
            var expFill=Rect("Fill",expBar,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>();expFill.type=Image.Type.Filled;expFill.fillMethod=Image.FillMethod.Horizontal;expFill.color=new Color(.58f,.38f,1f);
            var view=r.gameObject.AddComponent<PartyStorageEntryView>();var so=new SerializedObject(view);
            Set(so,"button",b);Set(so,"portrait",portrait);Set(so,"hpFill",hpFill);Set(so,"expFill",expFill);
            Set(so,"nameText",name);Set(so,"infoText",info);Set(so,"stageText",stage);Set(so,"expText",expText);so.ApplyModifiedPropertiesWithoutUndo();return view;
        }
        private static Button Btn(string n,Transform p,Vector2 min,Vector2 max,string label){var r=Rect(n,p,min,max);var i=r.gameObject.AddComponent<Image>();i.color=new Color(.12f,.075f,.22f);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=i;Text("Label",r,Vector2.zero,Vector2.one,24,label,TextAnchor.MiddleCenter);return b;}
        private static Text Text(string n,Transform p,Vector2 min,Vector2 max,int size,string value,TextAnchor a){var r=Rect(n,p,min,max);var t=r.gameObject.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.text=value;t.alignment=a;t.color=new Color(.96f,.93f,1);t.raycastTarget=false;return t;}
        private static RectTransform Rect(string n,Transform p,Vector2 min,Vector2 max){var g=new GameObject(n,typeof(RectTransform));g.transform.SetParent(p,false);var r=(RectTransform)g.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;return r;}
        private static void Set(SerializedObject s,string n,Object v)=>s.FindProperty(n).objectReferenceValue=v;
        private static void Ensure(string p,string n){if(!AssetDatabase.IsValidFolder(p+"/"+n))AssetDatabase.CreateFolder(p,n);}
    }
}
#endif
