using System;

namespace ShadowTheater.Data
{
    public enum TradeError { None, InvalidQuantity, InvalidPrice, InvalidState, KeyOwned, KeyQuantity, KeyCannotSell,
        InventoryFull, NotEnoughGold, NotEnoughItems, GoldFull, Unsellable }
    public struct ItemTradeQuote
    { public int quantity, total, goldAfter, ownedAfter; }
    /// <summary>One exact transaction, with wide arithmetic and no partial fills.</summary>
    public static class ItemTradePolicy
    {
        public static bool TryQuote(bool buy,int price,bool key,int owned,int gold,int quantity,out ItemTradeQuote quote,out TradeError error)
        {
            quote=default;error=TradeError.None;
            if(quantity<=0)error=TradeError.InvalidQuantity;
            else if(price<0)error=TradeError.InvalidPrice;
            else if(owned<0||gold<0)error=TradeError.InvalidState;
            else if(!buy&&key)error=TradeError.KeyCannotSell;
            else if(buy&&key&&owned>0)error=TradeError.KeyOwned;
            else if(buy&&key&&quantity!=1)error=TradeError.KeyQuantity;
            else if(!buy&&price==0)error=TradeError.Unsellable;
            if(error!=TradeError.None)return false;
            long total=(long)price*quantity;
            if(buy)
            {
                if((long)owned+quantity>int.MaxValue)error=TradeError.InventoryFull;
                else if(total>gold)error=TradeError.NotEnoughGold;
            }
            else
            {
                if(quantity>owned)error=TradeError.NotEnoughItems;
                else if((long)gold+total>int.MaxValue)error=TradeError.GoldFull;
            }
            if(error!=TradeError.None)return false;
            quote=new ItemTradeQuote{quantity=quantity,total=(int)total,goldAfter=(int)(buy?gold-total:gold+total),
                ownedAfter=buy?owned+quantity:owned-quantity};return true;
        }
        public static int Maximum(bool buy,int price,bool key,int owned,int gold)
        {
            if(price<0||owned<0||gold<0)return 0;
            if(buy)
            {
                if(key&&owned>0)return 0;
                long available=key?1:(long)int.MaxValue-owned;
                return (int)Math.Min(available,price==0?available:gold/price);
            }
            if(key||price<=0)return 0;
            return (int)Math.Min(owned,((long)int.MaxValue-gold)/price);
        }
    }
}
