using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>Native uGUI controls shared by runtime upgrades of older generated prefabs.</summary>
    public static class RuntimeUiControls
    {
        public static bool IsEditingText=>EventSystem.current!=null&&
            EventSystem.current.currentSelectedGameObject!=null&&
            EventSystem.current.currentSelectedGameObject.GetComponent<InputField>()?.isFocused==true;
        public static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;Place(rect,min,max);return rect;
        }
        public static void Place(RectTransform rect,Vector2 min,Vector2 max)
        {rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        public static Text Label(Transform parent,Font font,string text,int size=20)
        {
            var rect=Rect(parent,"Label",Vector2.zero,Vector2.one);var label=rect.gameObject.AddComponent<Text>();
            label.font=font;label.fontSize=size;label.text=text;label.color=TheatreUiStyle.Ivory;
            label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;return label;
        }
        public static Button Button(Transform parent,Font font,string label,Vector2 min,Vector2 max,UnityAction action)
        {
            var rect=Rect(parent,label,min,max);var image=rect.gameObject.AddComponent<Image>();
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
            Label(rect,font,label);TheatreUiStyle.Button(button);button.onClick.AddListener(action);return button;
        }
        public static InputField Input(Transform parent,Font font,string hint,Vector2 min,Vector2 max)
        {
            var rect=Rect(parent,"Search",min,max);rect.gameObject.AddComponent<Image>().color=TheatreUiStyle.Surface;
            var input=rect.gameObject.AddComponent<InputField>();input.targetGraphic=rect.GetComponent<Image>();
            input.textComponent=Label(rect,font,"");Place(input.textComponent.rectTransform,new Vector2(.04f,.05f),new Vector2(.96f,.95f));
            input.textComponent.alignment=TextAnchor.MiddleLeft;input.textComponent.supportRichText=false;
            var placeholder=Label(rect,font,hint,18);placeholder.color=TheatreUiStyle.Muted;placeholder.alignment=TextAnchor.MiddleLeft;
            Place(placeholder.rectTransform,new Vector2(.04f,.05f),new Vector2(.96f,.95f));input.placeholder=placeholder;
            input.characterLimit=64;return input;
        }
        public static Dropdown Dropdown(Transform parent,Font font,List<string> options,Vector2 min,Vector2 max)
        {
            var rect=Rect(parent,"Filter",min,max);rect.gameObject.AddComponent<Image>().color=TheatreUiStyle.Raised;
            var dropdown=rect.gameObject.AddComponent<Dropdown>();dropdown.targetGraphic=rect.GetComponent<Image>();
            dropdown.captionText=Label(rect,font,"",18);Place(dropdown.captionText.rectTransform,new Vector2(.04f,0),new Vector2(.88f,1));
            var arrow=Label(rect,font,"▾",18);Place(arrow.rectTransform,new Vector2(.88f,0),Vector2.one);
            var template=Rect(rect,"Template",new Vector2(0,0),new Vector2(1,0));template.pivot=new Vector2(.5f,1);template.sizeDelta=new Vector2(0,320);
            template.gameObject.AddComponent<Image>().color=TheatreUiStyle.Surface;
            var viewport=Rect(template,"Viewport",Vector2.zero,Vector2.one);viewport.gameObject.AddComponent<Image>().color=TheatreUiStyle.Surface;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content=Rect(viewport,"Content",new Vector2(0,1),Vector2.one);content.pivot=new Vector2(.5f,1);
            content.sizeDelta=new Vector2(0,46);
            var item=Rect(content,"Item",new Vector2(0,.5f),new Vector2(1,.5f));item.sizeDelta=new Vector2(0,46);
            var background=item.gameObject.AddComponent<Image>();background.color=TheatreUiStyle.Raised;
            var toggle=item.gameObject.AddComponent<Toggle>();toggle.targetGraphic=background;
            var check=Rect(item,"Check",new Vector2(.015f,.25f),new Vector2(.045f,.75f)).gameObject.AddComponent<Image>();check.color=TheatreUiStyle.Brass;toggle.graphic=check;
            var itemText=Label(item,font,"",20);Place(itemText.rectTransform,new Vector2(.06f,0),new Vector2(.98f,1));
            var scroll=template.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
            dropdown.template=template;dropdown.itemText=itemText;dropdown.AddOptions(options);template.gameObject.SetActive(false);
            dropdown.RefreshShownValue();return dropdown;
        }
        public static RectTransform ScrollRoot(Transform content)=>content?.GetComponentInParent<ScrollRect>()?.transform as RectTransform;
    }
}
