using System;
using System.Globalization;
using ShadowTheater.Data;
using ShadowTheater.Save;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>Exact quantity quote and final confirmation; no inventory mutation while editing.</summary>
    public sealed class ShopTradeDialog
    {
        private readonly GameObject root;
        private readonly Text title,summary,feedback;
        private readonly InputField quantity;
        private readonly Button confirm,maximum;
        private readonly Func<ItemData,int,bool,bool> transact;
        private ItemData item;
        private bool buying;
        public bool IsOpen=>root.activeSelf;
        public ShopTradeDialog(Transform parent,Font font,Func<ItemData,int,bool,bool> transact)
        {
            this.transact=transact;
            var shade=RuntimeUiControls.Rect(parent,"TradeConfirmation",Vector2.zero,Vector2.one);root=shade.gameObject;
            shade.gameObject.AddComponent<Image>().color=new Color(0,0,0,.8f);
            var card=RuntimeUiControls.Rect(shade,"Card",new Vector2(.08f,.28f),new Vector2(.92f,.75f));
            TheatreUiStyle.Panel(card.gameObject.AddComponent<Image>(),TheatreUiStyle.Midnight);
            title=RuntimeUiControls.Label(card,font,"",30);Place(title,new Vector2(.06f,.81f),new Vector2(.94f,.96f));
            summary=RuntimeUiControls.Label(card,font,"",22);Place(summary,new Vector2(.06f,.60f),new Vector2(.94f,.80f));
            quantity=RuntimeUiControls.Input(card,font,"수량",new Vector2(.15f,.46f),new Vector2(.85f,.58f));
            quantity.contentType=InputField.ContentType.IntegerNumber;quantity.characterLimit=10;
            quantity.textComponent.alignment=TextAnchor.MiddleCenter;
            int[] presets={1,5,10};
            for(int i=0;i<presets.Length;i++)
            {
                int count=presets[i];RuntimeUiControls.Button(card,font,"x"+count,new Vector2(.06f+i*.22f,.31f),new Vector2(.26f+i*.22f,.43f),()=>quantity.text=count.ToString(CultureInfo.InvariantCulture));
            }
            maximum=RuntimeUiControls.Button(card,font,"최대",new Vector2(.72f,.31f),new Vector2(.94f,.43f),()=>
            {int count=SaveManager.MaximumTradeQuantity(item,buying);if(count>0)quantity.text=count.ToString(CultureInfo.InvariantCulture);else Refresh();});
            feedback=RuntimeUiControls.Label(card,font,"",19);Place(feedback,new Vector2(.06f,.19f),new Vector2(.94f,.30f));
            RuntimeUiControls.Button(card,font,"취소",new Vector2(.06f,.05f),new Vector2(.47f,.17f),Close);
            confirm=RuntimeUiControls.Button(card,font,"거래 확정",new Vector2(.53f,.05f),new Vector2(.94f,.17f),Confirm);
            quantity.onValueChanged.AddListener(_=>Refresh());root.SetActive(false);
        }
        public void Show(ItemData selected,bool buy)
        {item=selected;buying=buy;root.transform.SetAsLastSibling();root.SetActive(true);quantity.text="1";Refresh();}
        public void Close(){root.SetActive(false);item=null;}
        private int Count=>int.TryParse(quantity.text,NumberStyles.Integer,CultureInfo.InvariantCulture,out int count)?count:0;
        public void Refresh()
        {
            if(!IsOpen||item==null)return;
            int count=Count;long price=buying?item.buyPrice:item.sellPrice;
            title.text=L10n.Text(item.displayName)+(buying?" · 구매":" · 판매");
            summary.text=$"보유 {SaveManager.GetItemCount(item.itemId):N0}개 · 금화 {SaveManager.Current?.gold??0:N0}\n단가 {price:N0} · 총액 {(count>0?price*count:0):N0} 금화";
            string message;bool valid=buying?SaveManager.CanBuyItem(item,count,out message):SaveManager.CanSellItem(item,count,out message);
            confirm.interactable=valid;feedback.text=valid?$"{count:N0}개를 한 번에 거래합니다.":message;
            maximum.interactable=SaveManager.MaximumTradeQuantity(item,buying)>0;
            feedback.color=valid?TheatreUiStyle.Mint:new Color(1f,.65f,.67f);
        }
        private void Confirm()
        {
            if(!IsOpen||item==null)return;
            if(transact(item,Count,buying))Close();else Refresh();
        }
        private static void Place(Text text,Vector2 min,Vector2 max)=>RuntimeUiControls.Place(text.rectTransform,min,max);
    }
}
