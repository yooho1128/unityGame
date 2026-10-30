using System;
using System.Collections.Generic;
using ShadowTheater.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    public sealed class CollectionToolbar
    {
        public CollectionToolbar(Transform parent,Font font,CollectionQuery query,Vector2 min,Vector2 max,Action changed)
        {
            var root=RuntimeUiControls.Rect(parent,"CollectionSearch",min,max);
            var input=RuntimeUiControls.Input(root,font,"이름·칭호 검색",Vector2.zero,new Vector2(.43f,1));
            var elements=new List<string>{"모든 속성"};
            foreach(ShadowElement element in Enum.GetValues(typeof(ShadowElement)))elements.Add(ElementChart.Name(element));
            var types=RuntimeUiControls.Dropdown(root,font,elements,new Vector2(.45f,0),new Vector2(.67f,1));
            var sort=RuntimeUiControls.Dropdown(root,font,new List<string>{"기본 순서","이름순","레벨 높은순","레벨 낮은순"},new Vector2(.69f,0),new Vector2(.90f,1));
            input.onValueChanged.AddListener(text=>{query.search=text;changed();});
            types.onValueChanged.AddListener(index=>{query.element=index-1;changed();});
            sort.onValueChanged.AddListener(index=>{query.sort=(CollectionSort)index;changed();});
            var reset=RuntimeUiControls.Button(root,font,"초기화",new Vector2(.92f,0),Vector2.one,()=>
            {query.Reset();input.text="";types.value=0;sort.value=0;changed();});
            reset.GetComponentInChildren<Text>().fontSize=16;
        }
    }
}
