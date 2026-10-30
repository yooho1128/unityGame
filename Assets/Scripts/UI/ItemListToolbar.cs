using System;
using System.Collections.Generic;
using ShadowTheater.Data;
using UnityEngine;

namespace ShadowTheater.UI
{
    public sealed class ItemListToolbar
    {
        public ItemListToolbar(Transform parent,Font font,ItemListQuery query,Vector2 min,Vector2 max,Action changed)
        {
            var root=RuntimeUiControls.Rect(parent,"ItemFilters",min,max);
            var input=RuntimeUiControls.Input(root,font,"도구 이름 검색",Vector2.zero,new Vector2(.56f,1));
            var category=RuntimeUiControls.Dropdown(root,font,new List<string>(ItemCategoryPolicy.Labels),new Vector2(.58f,0),Vector2.one);
            input.onValueChanged.AddListener(text=>{query.search=text;changed();});
            category.onValueChanged.AddListener(index=>{query.category=(ItemCategory)index;changed();});
        }
    }
}
