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
            // 전투는 가로 화면을 기준으로 설계한다. 세로 기준 해상도를 쓰면 16:9와 4:3에서
            // 패널/버튼이 비정상적으로 커져 서로 겹친다.
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;

            var bg = Rect("Backdrop", root.transform, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            bg.color = new Color(0.025f, 0.018f, 0.06f, 1f);
            bg.raycastTarget = false;
            CreateStageBackdrop(root.transform);
            var safeArea = Rect("SafeArea", root.transform, Vector2.zero, Vector2.one);
            safeArea.gameObject.AddComponent<MobileSafeArea>();
            var fxDirector = root.AddComponent<BattleFxDirector>();
            var sfxPlayer = root.AddComponent<BattleSfxPlayer>();
            var topBar = Rect("TopBar", safeArea, new Vector2(0.035f, 0.925f), new Vector2(0.965f, 0.985f));
            topBar.gameObject.AddComponent<Image>().color = new Color(0.045f, 0.03f, 0.09f, 0.94f);
            var turn = Text("Turn", topBar, new Vector2(0.025f, 0f), new Vector2(0.24f, 1f), 26, TextAnchor.MiddleLeft);
            var fp = Text("FP", topBar, new Vector2(0.32f, 0f), new Vector2(0.56f, 1f), 26, TextAnchor.MiddleCenter);
            var auto = Button("Auto", topBar, new Vector2(0.64f, 0.12f), new Vector2(0.82f, 0.88f), "AUTO OFF");
            var speed = Button("Speed", topBar, new Vector2(0.84f, 0.12f), new Vector2(0.975f, 0.88f), "×1");

            // 포켓몬식 대각선 구도: 두 유닛 패널과 파티 슬롯의 영역이 절대 겹치지 않는다.
            var enemy = UnitPanel("Enemy", safeArea, new Vector2(0.51f, 0.565f), new Vector2(0.95f, 0.90f), true);
            var player = UnitPanel("Player", safeArea, new Vector2(0.05f, 0.335f), new Vector2(0.49f, 0.67f), false);
            var enemyParty = PartyStrip("EnemyParty", safeArea, new Vector2(.55f,.515f), new Vector2(.95f,.56f));
            var playerParty = PartyStrip("PlayerParty", safeArea, new Vector2(.05f,.285f), new Vector2(.45f,.33f));

            var messageCard = Rect("MessageCard", safeArea, new Vector2(0.315f, 0.215f), new Vector2(0.685f, 0.285f));
            messageCard.gameObject.AddComponent<Image>().color = new Color(0.055f, 0.035f, 0.105f, 0.96f);
            var message = Text("Message", messageCard, new Vector2(0.025f, 0.05f), new Vector2(0.975f, 0.95f), 28, TextAnchor.MiddleCenter);

            var actions = Rect("Actions", safeArea, new Vector2(0.04f, 0.035f), new Vector2(0.96f, 0.19f));
            actions.gameObject.AddComponent<Image>().color = new Color(0.038f, 0.024f, 0.075f, 0.96f);
            var attack = ActionButton("Attack", actions, 0, "공격"); var skills = ActionButton("Skills", actions, 1, "스킬");
            var swap = ActionButton("Switch", actions, 2, "교체"); var record = ActionButton("Record", actions, 3, "각본 기록");
            var items = ActionButton("Items", actions, 4, "도구"); var escape = ActionButton("Escape", actions, 5, "도주");

            var options = Rect("Options", safeArea, new Vector2(0.12f, 0.025f), new Vector2(0.88f, 0.28f));
            var optionsImage = options.gameObject.AddComponent<Image>(); optionsImage.color = new Color(0.04f, 0.025f, 0.08f, 0.98f);
            var optionPrompt = Text("Prompt", options, new Vector2(0.035f, 0.73f), new Vector2(0.77f, 0.96f), 25, TextAnchor.MiddleLeft);
            var back = Button("Back", options, new Vector2(0.80f, 0.73f), new Vector2(0.97f, 0.95f), "뒤로");
            var viewport = Rect("Viewport", options, new Vector2(0.025f, 0.06f), new Vector2(0.975f, 0.70f));
            viewport.gameObject.AddComponent<Image>().color = new Color(1,1,1,0.01f); viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var content = Rect("Content", viewport, new Vector2(0,1), Vector2.one); content.pivot = new Vector2(0.5f,1);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 6; layout.childForceExpandHeight = false; layout.childControlHeight = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            var template = OptionTemplate(content); template.gameObject.SetActive(false); options.gameObject.SetActive(false);

            var result = Rect("Result", safeArea, Vector2.zero, Vector2.one);
            result.gameObject.AddComponent<Image>().color = new Color(0.008f, 0.005f, 0.02f, 0.84f);
            var resultGroup = result.gameObject.AddComponent<CanvasGroup>();
            var resultCard = Rect("Card", result, new Vector2(0.22f, 0.17f), new Vector2(0.78f, 0.83f));
            resultCard.gameObject.AddComponent<Image>().color = new Color(0.045f, 0.028f, 0.085f, 0.99f);
            var resultAccent = Rect("Accent", resultCard, new Vector2(0f, 0.955f), Vector2.one).gameObject.AddComponent<Image>();
            var resultTitle = Text("Title", resultCard, new Vector2(0.06f, 0.75f), new Vector2(0.94f, 0.94f), 46, TextAnchor.MiddleCenter);
            var resultSummary = Text("Summary", resultCard, new Vector2(0.06f, 0.57f), new Vector2(0.94f, 0.74f), 28, TextAnchor.MiddleCenter);
            resultSummary.color = new Color(1f, .82f, .42f);
            var resultDetails = Text("Details", resultCard, new Vector2(0.08f, 0.25f), new Vector2(0.92f, 0.57f), 23, TextAnchor.UpperCenter);
            resultDetails.horizontalOverflow = HorizontalWrapMode.Wrap;
            resultDetails.verticalOverflow = VerticalWrapMode.Truncate;
            var resultContinue = Button("Continue", resultCard, new Vector2(0.30f, 0.07f), new Vector2(0.70f, 0.21f), "계속");
            result.gameObject.SetActive(false);

            var ui = root.GetComponent<BattleUIController>(); var so = new SerializedObject(ui);
            Set(so,"manager",root.GetComponent<BattleManager>()); Set(so,"playerPanel",player); Set(so,"enemyPanel",enemy);
            Set(so,"playerPartyStrip",playerParty); Set(so,"enemyPartyStrip",enemyParty);
            Set(so,"fxDirector",fxDirector);
            Set(so,"actionRoot",actions.gameObject); Set(so,"optionRoot",options.gameObject); Set(so,"optionContent",content);
            Set(so,"optionTemplate",template); Set(so,"messageRoot",messageCard.gameObject); Set(so,"messageText",message);
            Set(so,"optionPromptText",optionPrompt); Set(so,"fpText",fp); Set(so,"turnText",turn);
            Set(so,"autoText",auto.GetComponentInChildren<Text>()); Set(so,"speedText",speed.GetComponentInChildren<Text>());
            Set(so,"recordText",record.GetComponentInChildren<Text>()); Set(so,"recordButton",record); Set(so,"escapeButton",escape); so.ApplyModifiedPropertiesWithoutUndo();
            so.Update(); Set(so,"resultRoot",result.gameObject); Set(so,"resultGroup",resultGroup); Set(so,"resultCard",resultCard);
            Set(so,"resultAccent",resultAccent); Set(so,"resultTitleText",resultTitle); Set(so,"resultSummaryText",resultSummary);
            Set(so,"resultDetailsText",resultDetails); Set(so,"resultContinueText",resultContinue.GetComponentInChildren<Text>()); so.ApplyModifiedPropertiesWithoutUndo();
            // 전투 연출은 UI보다 위에 그리되 좌표 계산은 안전영역을 기준으로 한다.
            var fxRoot = Rect("BattleFx", root.transform, Vector2.zero, Vector2.one);
            fxRoot.SetAsLastSibling();
            var fx = new SerializedObject(fxDirector); Set(fx,"fxRoot",fxRoot); Set(fx,"stageRoot",safeArea); Set(fx,"sfxPlayer",sfxPlayer); fx.ApplyModifiedPropertiesWithoutUndo();
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
            UnityEditor.Events.UnityEventTools.AddPersistentListener(resultContinue.onClick, ui.ConfirmResult);
            PrefabUtility.SaveAsPrefabAsset(root, Path); Object.DestroyImmediate(root); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
        }

        private static BattleUnitPanel UnitPanel(string name, Transform parent, Vector2 min, Vector2 max, bool flip)
        {
            var r=Rect(name,parent,min,max);
            r.gameObject.AddComponent<Image>().color = new Color(.035f,.024f,.07f,.82f);
            var accent=Rect("Accent",r,Vector2.zero,new Vector2(0.012f,1)).gameObject.AddComponent<Image>();
            var portrait=Rect("Portrait",r,flip?new Vector2(0.60f,0.04f):new Vector2(0.02f,0.04f),flip?new Vector2(0.98f,0.96f):new Vector2(0.40f,0.96f)).gameObject.AddComponent<Image>(); portrait.preserveAspect=true; portrait.raycastTarget=false;
            var info=Rect("Info",r,flip?new Vector2(0.03f,0.04f):new Vector2(0.42f,0.04f),flip?new Vector2(0.58f,0.96f):new Vector2(0.97f,0.96f));
            var n=Text("Name",info,new Vector2(0.03f,0.74f),new Vector2(0.70f,0.98f),30,TextAnchor.MiddleLeft);
            var elem=Text("Element",info,new Vector2(0.03f,0.60f),new Vector2(0.70f,0.75f),18,TextAnchor.MiddleLeft);
            var lv=Text("Level",info,new Vector2(0.72f,0.68f),new Vector2(0.97f,0.98f),24,TextAnchor.MiddleRight);
            var bar=Rect("HpBar",info,new Vector2(0.03f,0.42f),new Vector2(0.97f,0.57f)); bar.gameObject.AddComponent<Image>().color=new Color(.12f,.1f,.18f);
            var fill=Rect("Fill",bar,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>(); fill.type=Image.Type.Filled; fill.fillMethod=Image.FillMethod.Horizontal;
            var hp=Text("Hp",info,new Vector2(0.03f,0.18f),new Vector2(0.43f,0.40f),22,TextAnchor.MiddleLeft);
            var st=Text("Status",info,new Vector2(0.43f,0.16f),new Vector2(0.97f,0.41f),18,TextAnchor.MiddleRight);
            var p=r.gameObject.AddComponent<BattleUnitPanel>(); var so=new SerializedObject(p);
            Set(so,"portrait",portrait); Set(so,"accent",accent); Set(so,"hpFill",fill); Set(so,"nameText",n); Set(so,"levelText",lv); Set(so,"elementText",elem); Set(so,"hpText",hp); Set(so,"statusText",st); so.ApplyModifiedPropertiesWithoutUndo(); return p;
        }
        private static BattleOptionButton OptionTemplate(Transform p){var r=Rect("OptionTemplate",p,Vector2.zero,Vector2.one);r.gameObject.AddComponent<LayoutElement>().preferredHeight=88;var i=r.gameObject.AddComponent<Image>();i.color=new Color(.11f,.07f,.19f);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=i;var t=Text("Title",r,new Vector2(.03f,.54f),new Vector2(.97f,.96f),25,TextAnchor.MiddleLeft);var s=Text("Subtitle",r,new Vector2(.03f,.04f),new Vector2(.97f,.58f),16,TextAnchor.UpperLeft);var v=r.gameObject.AddComponent<BattleOptionButton>();var so=new SerializedObject(v);Set(so,"button",b);Set(so,"titleText",t);Set(so,"subtitleText",s);so.ApplyModifiedPropertiesWithoutUndo();return v;}
        private static BattlePartyStrip PartyStrip(string n,Transform p,Vector2 min,Vector2 max){var r=Rect(n,p,min,max);var layout=r.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=6;layout.childAlignment=TextAnchor.MiddleCenter;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=true;var strip=r.gameObject.AddComponent<BattlePartyStrip>();var so=new SerializedObject(strip);var slots=so.FindProperty("slots");slots.arraySize=6;for(int i=0;i<6;i++){var slot=PartySlot($"Slot{i+1}",r);slots.GetArrayElementAtIndex(i).objectReferenceValue=slot;}so.ApplyModifiedPropertiesWithoutUndo();return strip;}
        private static BattlePartySlot PartySlot(string n,Transform p){var r=Rect(n,p,Vector2.zero,Vector2.one);r.gameObject.AddComponent<LayoutElement>().minWidth=46;var bg=r.gameObject.AddComponent<Image>();bg.color=new Color(.08f,.07f,.12f,.55f);var portrait=Rect("Portrait",r,new Vector2(.08f,.17f),new Vector2(.92f,.96f)).gameObject.AddComponent<Image>();portrait.preserveAspect=true;portrait.raycastTarget=false;var bar=Rect("Hp",r,new Vector2(.08f,.05f),new Vector2(.92f,.15f));bar.gameObject.AddComponent<Image>().color=new Color(.08f,.06f,.12f);var fill=Rect("Fill",bar,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>();fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;fill.raycastTarget=false;var faint=Rect("Fainted",r,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>();faint.raycastTarget=false;faint.enabled=false;var slot=r.gameObject.AddComponent<BattlePartySlot>();var so=new SerializedObject(slot);Set(so,"background",bg);Set(so,"portrait",portrait);Set(so,"hpFill",fill);Set(so,"faintOverlay",faint);so.ApplyModifiedPropertiesWithoutUndo();return slot;}
        private static Button ActionButton(string n,Transform p,int i,string label){const float gap=.006f;float cell=1f/6f;return Button(n,p,new Vector2(i*cell+gap,.08f),new Vector2((i+1)*cell-gap,.92f),label);}
        private static void CreateStageBackdrop(Transform parent)
        {
            Decor("Sky", parent, new Vector2(0, .34f), Vector2.one, new Color(.018f,.026f,.07f));
            Decor("HorizonGlow", parent, new Vector2(0, .34f), new Vector2(1,.55f), new Color(.08f,.10f,.19f,.70f));
            Decor("StageFloor", parent, Vector2.zero, new Vector2(1,.34f), new Color(.035f,.022f,.055f));
            for (int i=0;i<7;i++)
            {
                float x=.08f+i*.14f;
                Decor("FloorLine"+i,parent,new Vector2(x,0),new Vector2(x+.003f,.34f),new Color(.25f,.16f,.38f,.28f));
            }
            for (int i=0;i<5;i++)
            {
                float x=.12f+i*.19f;
                Decor("RuinedPillar"+i,parent,new Vector2(x,.43f),new Vector2(x+.018f,.73f+i%2*.06f),new Color(.07f,.075f,.13f,.72f));
                Decor("PillarCap"+i,parent,new Vector2(x-.008f,.72f+i%2*.06f),new Vector2(x+.026f,.735f+i%2*.06f),new Color(.12f,.11f,.19f,.75f));
            }
            Decor("LeftCurtain",parent,Vector2.zero,new Vector2(.025f,1),new Color(.16f,.035f,.09f,.92f));
            Decor("RightCurtain",parent,new Vector2(.975f,0),Vector2.one,new Color(.16f,.035f,.09f,.92f));
            Decor("TopCurtain",parent,new Vector2(0,.975f),Vector2.one,new Color(.12f,.025f,.075f,.94f));
        }
        private static void Decor(string n,Transform p,Vector2 min,Vector2 max,Color color){var image=Rect(n,p,min,max).gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;}
        private static Button Button(string n,Transform p,Vector2 min,Vector2 max,string label,Color? c=null){var r=Rect(n,p,min,max);var im=r.gameObject.AddComponent<Image>();im.color=c??new Color(.12f,.075f,.22f);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=im;Text("Label",r,Vector2.zero,Vector2.one,24,TextAnchor.MiddleCenter).text=label;return b;}
        private static RectTransform Rect(string n,Transform p,Vector2 min,Vector2 max){var g=new GameObject(n,typeof(RectTransform));g.transform.SetParent(p,false);var r=(RectTransform)g.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;return r;}
        private static Text Text(string n,Transform p,Vector2 min,Vector2 max,int size,TextAnchor a){var r=Rect(n,p,min,max);var t=r.gameObject.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.alignment=a;t.color=new Color(.96f,.93f,1);t.raycastTarget=false;return t;}
        private static void Set(SerializedObject so,string n,Object v)=>so.FindProperty(n).objectReferenceValue=v;
        private static void EnsureFolder(string p,string n){if(!AssetDatabase.IsValidFolder(p+"/"+n))AssetDatabase.CreateFolder(p,n);}
    }
}
#endif
