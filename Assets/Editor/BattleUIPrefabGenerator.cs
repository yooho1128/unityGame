#if UNITY_EDITOR
using ShadowTheater.Battle;
using ShadowTheater.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.EditorTools
{
    public static class BattleUIPrefabGenerator
    {
        private const string Path = "Assets/Prefabs/UI/BattleCanvas.prefab";

        [MenuItem("Tools/Shadow Theater/Generate Battle UI")]
        public static void Generate()
        {
            EnsureFolder("Assets", "Prefabs"); EnsureFolder("Assets/Prefabs", "UI");
            var root = new GameObject("BattleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(BattleManager), typeof(BattleUIController));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0.5f;

            var bg = Rect("Backdrop", root.transform, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            bg.color = new Color(0.025f, 0.018f, 0.06f, 1f);
            var enemy = UnitPanel("Enemy", root.transform, new Vector2(0.08f, 0.57f), new Vector2(0.92f, 0.91f), true);
            var player = UnitPanel("Player", root.transform, new Vector2(0.08f, 0.30f), new Vector2(0.92f, 0.58f), false);
            var turn = Text("Turn", root.transform, new Vector2(0.04f, 0.94f), new Vector2(0.25f, 0.99f), 24, TextAnchor.MiddleLeft);
            var fp = Text("FP", root.transform, new Vector2(0.28f, 0.94f), new Vector2(0.55f, 0.99f), 24, TextAnchor.MiddleCenter);
            var auto = Button("Auto", root.transform, new Vector2(0.62f, 0.94f), new Vector2(0.80f, 0.99f), "AUTO OFF");
            var speed = Button("Speed", root.transform, new Vector2(0.82f, 0.94f), new Vector2(0.96f, 0.99f), "×1");
            var message = Text("Message", root.transform, new Vector2(0.06f, 0.225f), new Vector2(0.94f, 0.30f), 29, TextAnchor.MiddleCenter);

            var actions = Rect("Actions", root.transform, new Vector2(0.04f, 0.025f), new Vector2(0.96f, 0.21f));
            var attack = ActionButton("Attack", actions, 0, "공격"); var skills = ActionButton("Skills", actions, 1, "스킬");
            var swap = ActionButton("Switch", actions, 2, "교체"); var record = ActionButton("Record", actions, 3, "각본 기록");
            var items = ActionButton("Items", actions, 4, "도구"); var escape = ActionButton("Escape", actions, 5, "도주");

            var options = Rect("Options", root.transform, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.22f));
            var optionsImage = options.gameObject.AddComponent<Image>(); optionsImage.color = new Color(0.04f, 0.025f, 0.08f, 0.98f);
            var back = Button("Back", options, new Vector2(0.78f, 0.72f), new Vector2(0.97f, 0.96f), "뒤로");
            var viewport = Rect("Viewport", options, new Vector2(0.03f, 0.05f), new Vector2(0.74f, 0.95f));
            viewport.gameObject.AddComponent<Image>().color = new Color(1,1,1,0.01f); viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var content = Rect("Content", viewport, new Vector2(0,1), Vector2.one); content.pivot = new Vector2(0.5f,1);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 6; layout.childForceExpandHeight = false; layout.childControlHeight = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            var template = OptionTemplate(content); template.gameObject.SetActive(false); options.gameObject.SetActive(false);

            var ui = root.GetComponent<BattleUIController>(); var so = new SerializedObject(ui);
            Set(so,"manager",root.GetComponent<BattleManager>()); Set(so,"playerPanel",player); Set(so,"enemyPanel",enemy);
            Set(so,"actionRoot",actions.gameObject); Set(so,"optionRoot",options.gameObject); Set(so,"optionContent",content);
            Set(so,"optionTemplate",template); Set(so,"messageText",message); Set(so,"fpText",fp); Set(so,"turnText",turn);
            Set(so,"autoText",auto.GetComponentInChildren<Text>()); Set(so,"speedText",speed.GetComponentInChildren<Text>());
            Set(so,"recordButton",record); Set(so,"escapeButton",escape); so.ApplyModifiedPropertiesWithoutUndo();
            var bm = new SerializedObject(root.GetComponent<BattleManager>()); Set(bm,"presenterComponent",ui); bm.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(attack.onClick, ui.Attack);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(skills.onClick, ui.OpenSkills);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(swap.onClick, ui.OpenSwitches);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(record.onClick, ui.Record);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(items.onClick, ui.OpenItems);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(escape.onClick, ui.Escape);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(back.onClick, ui.BackToActions);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(auto.onClick, ui.ToggleAuto);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(speed.onClick, ui.CycleSpeed);
            PrefabUtility.SaveAsPrefabAsset(root, Path); Object.DestroyImmediate(root); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
        }

        private static BattleUnitPanel UnitPanel(string name, Transform parent, Vector2 min, Vector2 max, bool flip)
        {
            var r=Rect(name,parent,min,max); var accent=Rect("Accent",r,Vector2.zero,new Vector2(0.015f,1)).gameObject.AddComponent<Image>();
            var portrait=Rect("Portrait",r,flip?new Vector2(0.62f,0):Vector2.zero,flip?Vector2.one:new Vector2(0.38f,1)).gameObject.AddComponent<Image>(); portrait.preserveAspect=true;
            var info=Rect("Info",r,flip?Vector2.zero:new Vector2(0.40f,0),flip?new Vector2(0.60f,1):Vector2.one);
            var n=Text("Name",info,new Vector2(0.03f,0.68f),new Vector2(0.72f,0.98f),32,TextAnchor.MiddleLeft);
            var lv=Text("Level",info,new Vector2(0.72f,0.68f),new Vector2(0.97f,0.98f),23,TextAnchor.MiddleRight);
            var bar=Rect("HpBar",info,new Vector2(0.03f,0.43f),new Vector2(0.97f,0.58f)); bar.gameObject.AddComponent<Image>().color=new Color(.12f,.1f,.18f);
            var fill=Rect("Fill",bar,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>(); fill.type=Image.Type.Filled; fill.fillMethod=Image.FillMethod.Horizontal;
            var hp=Text("Hp",info,new Vector2(0.03f,0.20f),new Vector2(0.65f,0.42f),22,TextAnchor.MiddleLeft);
            var st=Text("Status",info,new Vector2(0.65f,0.20f),new Vector2(0.97f,0.42f),22,TextAnchor.MiddleRight);
            var p=r.gameObject.AddComponent<BattleUnitPanel>(); var so=new SerializedObject(p);
            Set(so,"portrait",portrait); Set(so,"accent",accent); Set(so,"hpFill",fill); Set(so,"nameText",n); Set(so,"levelText",lv); Set(so,"hpText",hp); Set(so,"statusText",st); so.ApplyModifiedPropertiesWithoutUndo(); return p;
        }
        private static BattleOptionButton OptionTemplate(Transform p){var r=Rect("OptionTemplate",p,Vector2.zero,Vector2.one);r.gameObject.AddComponent<LayoutElement>().preferredHeight=82;var i=r.gameObject.AddComponent<Image>();i.color=new Color(.11f,.07f,.19f);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=i;var t=Text("Title",r,new Vector2(.03f,.42f),new Vector2(.97f,.95f),25,TextAnchor.MiddleLeft);var s=Text("Subtitle",r,new Vector2(.03f,.04f),new Vector2(.97f,.46f),18,TextAnchor.MiddleLeft);var v=r.gameObject.AddComponent<BattleOptionButton>();var so=new SerializedObject(v);Set(so,"button",b);Set(so,"titleText",t);Set(so,"subtitleText",s);so.ApplyModifiedPropertiesWithoutUndo();return v;}
        private static Button ActionButton(string n,Transform p,int i,string label){int col=i%3,row=i/3;return Button(n,p,new Vector2(col/3f,0.5f-row*.5f),new Vector2((col+1)/3f,1f-row*.5f),label);}
        private static Button Button(string n,Transform p,Vector2 min,Vector2 max,string label,Color? c=null){var r=Rect(n,p,min,max);var im=r.gameObject.AddComponent<Image>();im.color=c??new Color(.12f,.075f,.22f);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=im;Text("Label",r,Vector2.zero,Vector2.one,24,TextAnchor.MiddleCenter).text=label;return b;}
        private static RectTransform Rect(string n,Transform p,Vector2 min,Vector2 max){var g=new GameObject(n,typeof(RectTransform));g.transform.SetParent(p,false);var r=(RectTransform)g.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;return r;}
        private static Text Text(string n,Transform p,Vector2 min,Vector2 max,int size,TextAnchor a){var r=Rect(n,p,min,max);var t=r.gameObject.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.alignment=a;t.color=new Color(.96f,.93f,1);t.raycastTarget=false;return t;}
        private static void Set(SerializedObject so,string n,Object v)=>so.FindProperty(n).objectReferenceValue=v;
        private static void EnsureFolder(string p,string n){if(!AssetDatabase.IsValidFolder(p+"/"+n))AssetDatabase.CreateFolder(p,n);}
    }
}
#endif
