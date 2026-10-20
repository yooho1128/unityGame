#if UNITY_EDITOR
using ShadowTheater.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.EditorTools
{
    public static class RegionArrivalPrefabGenerator
    {
        private const string PrefabPath = "Assets/Prefabs/UI/RegionArrivalCanvas.prefab";

        [MenuItem("Tools/Shadow Theater/Generate Region Arrival UI")]
        public static void Generate()
        {
            Ensure("Assets", "Prefabs"); Ensure("Assets/Prefabs", "UI");
            var canvasGo = new GameObject("RegionArrivalCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(RegionArrivalBanner));
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 45;
            var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = .5f;

            var card = Rect("ArrivalCard", canvasGo.transform, new Vector2(.08f,.64f), new Vector2(.92f,.82f));
            var group = card.gameObject.AddComponent<CanvasGroup>(); group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;
            var bg = card.gameObject.AddComponent<Image>(); bg.color = new Color(.018f,.014f,.045f,.9f); bg.raycastTarget = false;
            var outline = card.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.42f,.30f,.68f,.65f); outline.effectDistance = new Vector2(2,-2);
            var glow = Rect("AtmosphereGlow", card, new Vector2(.02f,.08f), new Vector2(.98f,.92f)).gameObject.AddComponent<Image>();
            glow.color = new Color(.35f,.2f,.7f,.16f); glow.raycastTarget = false;
            var motifLeft = Rect("MotifLeft", card, new Vector2(.04f,.12f), new Vector2(.12f,.88f)).gameObject.AddComponent<Image>();
            motifLeft.color = new Color(.55f,.34f,.9f,.3f); motifLeft.raycastTarget = false;
            var motifRight = Rect("MotifRight", card, new Vector2(.88f,.16f), new Vector2(.95f,.84f)).gameObject.AddComponent<Image>();
            motifRight.color = new Color(.78f,.68f,1f,.22f); motifRight.raycastTarget = false;
            var accent = Rect("Accent", card, new Vector2(0,.04f), new Vector2(.012f,.96f)).gameObject.AddComponent<Image>(); accent.raycastTarget = false;
            var act = Text("Act", card, new Vector2(.055f,.67f), new Vector2(.95f,.91f), 24, FontStyle.Bold, TextAnchor.MiddleCenter);
            act.color = new Color(.72f,.64f,.88f,1f);
            var region = Text("Region", card, new Vector2(.045f,.30f), new Vector2(.955f,.70f), 48, FontStyle.Bold, TextAnchor.MiddleCenter);
            var environment = Text("Environment", card, new Vector2(.055f,.16f), new Vector2(.945f,.35f), 24, FontStyle.Normal, TextAnchor.MiddleCenter);
            environment.color = new Color(.72f,.70f,.80f,1f);
            var discovery = Text("Discovery", card, new Vector2(.055f,.015f), new Vector2(.945f,.18f), 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            discovery.color = new Color(1f,.78f,.32f,1f); discovery.gameObject.SetActive(false);

            var banner = canvasGo.GetComponent<RegionArrivalBanner>(); var so = new SerializedObject(banner);
            Set(so,"root",group); Set(so,"card",card); Set(so,"accent",accent);
            Set(so,"atmosphereGlow",glow); Set(so,"motifLeft",motifLeft); Set(so,"motifRight",motifRight); Set(so,"actText",act);
            Set(so,"regionText",region); Set(so,"environmentText",environment); Set(so,"discoveryText",discovery); so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(canvasGo,PrefabPath); Object.DestroyImmediate(canvasGo);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[Shadow Theater] 지역 진입 타이틀 UI 생성 완료");
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        { var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r; }
        private static Text Text(string name, Transform parent, Vector2 min, Vector2 max, int size, FontStyle style, TextAnchor align)
        { var t=Rect(name,parent,min,max).gameObject.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.fontStyle=style;t.alignment=align;t.color=Color.white;t.raycastTarget=false;return t; }
        private static void Set(SerializedObject so,string name,Object value)=>so.FindProperty(name).objectReferenceValue=value;
        private static void Ensure(string parent,string child){if(!AssetDatabase.IsValidFolder(parent+"/"+child))AssetDatabase.CreateFolder(parent,child);}
    }
}
#endif
