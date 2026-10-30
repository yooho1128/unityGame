using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.Story;
using UnityEngine;

class Program
{
    static int checks;
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static Dictionary<string,ItemData> Items()
    {
        var result=new Dictionary<string,ItemData>();
        foreach(string file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory,"Data"),"*Catalog.json"))
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(file));
            if(!doc.RootElement.TryGetProperty("items",out var entries))continue;
            foreach(var entry in entries.EnumerateArray())
            {
                int Number(string key)=>entry.TryGetProperty(key,out var v)?v.GetInt32():0;
                bool Bool(string key)=>entry.TryGetProperty(key,out var v)&&v.GetBoolean();
                string Text(string key)=>entry.TryGetProperty(key,out var v)?v.GetString():"";
                var item=new ItemData{itemId=Text("itemId"),displayName=Text("displayName"),buyPrice=Number("buyPrice"),sellPrice=Number("sellPrice"),
                    shopUnlockAct=Number("shopUnlockAct"),isKeyItem=Bool("isKeyItem"),isEventOnly=Bool("isEventOnly"),effectType=Enum.Parse<ItemEffectType>(Text("effectType"))};
                result[item.itemId]=item;
            }
        }
        return result;
    }
    static void Main()
    {
        var items=Items();Check(items.Count==21,"real item catalogue coverage");
        Check(JourneyHubPolicy.All.Count==2,"two late-act outposts");
        var layouts=JsonUtility.FromJson<RegionLayoutCatalog>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data/RegionLayoutCatalog.json")));
        foreach(var hub in JourneyHubPolicy.All)
        {
            var region=RegionRepository.Get(hub.regionId);
            Check(region!=null&&region.act>=7&&!region.isSettlement&&region.AllowsRandomEncounters,"outpost preserves field encounters");
            SaveManager.Current=new SaveData{mapId="PrologueTheater"};SaveManager.Flags.Clear();
            Check(!JourneyHubPolicy.CanFastTravel(region),"first visit cannot be skipped");
            SaveManager.Current.visitedRegionIds.Add(region.regionId);
            if(!string.IsNullOrEmpty(region.requiredFlag))
            {
                Check(!JourneyHubPolicy.CanFastTravel(region),"visited outpost cannot bypass story gate");
                SaveManager.SetFlag(region.requiredFlag);
            }
            Check(JourneyHubPolicy.CanFastTravel(region),"old visited save can return to outpost");
            var arrival=JourneyHubPolicy.Arrival(region);Check(arrival.x==hub.checkpointX&&arrival.y==hub.checkpointY,"shared respawn and travel cell");
            SaveManager.Current.mapId=region.sceneName;Check(!JourneyHubPolicy.CanFastTravel(region),"already at destination");
            var plan=RegionLayoutPolicy.Build(layouts.entries.First(l=>l.regionId==region.regionId));
            Check(!plan.Blocked(hub.x,hub.y)&&!plan.Blocked(arrival.x,arrival.y),"supply stand and respawn floor reserved");
            var exact=JourneyHubPolicy.StationCell(hub,Array.Empty<LayoutCell>());
            Check(exact.HasValue&&exact.Value.x==hub.x&&exact.Value.y==hub.y,"preferred stand location");
            var shifted=JourneyHubPolicy.StationCell(hub,new[]{new LayoutCell(hub.x,hub.y)});
            Check(shifted.HasValue&&(shifted.Value.x!=hub.x||shifted.Value.y!=hub.y)&&
                (shifted.Value.x!=hub.checkpointX||shifted.Value.y!=hub.checkpointY),"legacy discovery prop does not overlap supply stand or respawn");
            foreach(string id in hub.itemIds)Check(items.ContainsKey(id)&&JourneyHubPolicy.AllowsItem(region,items[id]),"existing priced supply "+id);
            Check(!JourneyHubPolicy.AllowsItem(region,items["frostwing_compass"]),"event key not sold");
            Check(!JourneyHubPolicy.AllowsItem(region,new ItemData{itemId="unknown"}),"unlisted stock not sold");
        }
        Check(RegionRepository.All.Count(r=>r.isSettlement)==7,"seven towns preserved");
        Check(RegionRepository.All.Count(JourneyHubPolicy.HasServices)==9,"nine total service destinations");
        Check(!JourneyHubPolicy.CanFastTravel(null),"null map selection safe");
        var full=Enumerable.Range(0,RegionLayoutPlan.Count).Select(RegionLayoutPlan.Cell).ToArray();
        Check(JourneyHubPolicy.StationCell(JourneyHubPolicy.All[0],full)==null,"fully occupied field does not create overlapping stand");

        var first=new ShadowSearchEntry{key="1",index=0,name="기사",baseName="기사",title="달의 기사",element=1,level=20,seen=true,recorded=true};
        var duplicate=new ShadowSearchEntry{key="2",index=1,name="기억을 되찾은 기사",baseName="기사",title="달의 기사",element=1,level=35,seen=true,recorded=true};
        var neutral=new ShadowSearchEntry{key="3",index=2,name="Mage",baseName="Mage",title="도서관의 파수꾼",element=0,level=10,seen=true,recorded=true};
        var seen=new ShadowSearchEntry{key="seen",index=3,name="목격자",baseName="목격자",title="비공개 칭호",element=2,seen=true};
        var unknown=new ShadowSearchEntry{key="unknown",index=4,name="숨은 이름",baseName="숨은 이름",title="비공개 칭호",element=1};
        var entries=new[]{first,duplicate,neutral,seen,unknown};var original=entries.Select(e=>e.key).ToArray();
        var query=new CollectionQuery();Check(query.Apply(entries).Count==5,"default list includes unknown slots");
        query.search="기사";Check(query.Apply(entries).Count==2,"base and restored names, duplicate individuals retained");
        query.search="  기억   기사 ";Check(query.Apply(entries).Single()==duplicate,"multiple tokens and whitespace");
        query.search="mage";Check(query.Apply(entries).Single()==neutral,"case-insensitive search");
        query.search="파수꾼";Check(query.Apply(entries).Single()==neutral,"recorded title search");
        query.search="숨은";Check(query.Apply(entries).Count==0,"unknown names not discoverable by search");
        query.search="비공개";Check(query.Apply(entries).Count==0,"unrecorded titles not leaked");
        query.search="";query.element=0;Check(query.Apply(entries).Single()==neutral,"neutral is an element rather than all");
        query.element=2;Check(query.Apply(entries).Count==0,"seen-only type stays hidden");
        query.Reset();query.sort=CollectionSort.LevelAscending;Check(query.Apply(entries).Select(e=>e.key).SequenceEqual(new[]{"3","1","2","seen","unknown"}),"owned levels ascending with no-owned at end");
        query.sort=CollectionSort.LevelDescending;Check(query.Apply(entries)[0]==duplicate,"owned levels descending");
        query.sort=CollectionSort.Name;Check(query.Apply(entries).Last()==unknown,"unknown names do not affect alphabetical order");
        Check(entries.Select(e=>e.key).SequenceEqual(original),"sorting never changes source party/storage order");
        query.Reset();Check(query.sort==CollectionSort.Original&&query.element==-1&&query.search=="","clear all filters");
        Check(query.Apply(null).Count==0,"no source safe");

        foreach(var item in items.Values)
        {
            Check(ItemCategoryPolicy.Matches(item,ItemCategory.All),"all real items visible");
            Check(Enum.GetValues(typeof(ItemCategory)).Cast<ItemCategory>().Any(c=>c!=ItemCategory.All&&ItemCategoryPolicy.Matches(item,c)),"real item classified "+item.itemId);
            Check(ItemCategoryPolicy.Matches(item,ItemCategory.All,item.displayName),"real item searchable "+item.itemId);
        }
        Check(ItemCategoryPolicy.Matches(items["restoration_tea"],ItemCategory.Healing)&&ItemCategoryPolicy.Matches(items["restoration_tea"],ItemCategory.Cleanse),"combined healing/cleanse in both categories");
        Check(!ItemCategoryPolicy.Matches(null,ItemCategory.All),"null item safe");
        Check(ItemCategoryPolicy.Matches(items["complete_script"],ItemCategory.Growth),"training item remains classified with growth rules external");

        Check(ItemTradePolicy.TryQuote(true,60,false,3,1000,5,out var buy,out _)&&buy.goldAfter==700&&buy.ownedAfter==8&&buy.total==300,"five existing potions exact price");
        Check(!ItemTradePolicy.TryQuote(true,60,false,3,299,5,out _,out var error)&&error==TradeError.NotEnoughGold,"no partial purchase");
        Check(!ItemTradePolicy.TryQuote(false,20,false,3,1000,4,out _,out error)&&error==TradeError.NotEnoughItems,"no partial sale");
        Check(ItemTradePolicy.Maximum(true,60,false,0,1000)==16,"maximum affordable quantity");
        Check(!ItemTradePolicy.TryQuote(true,240,true,0,1000,2,out _,out error)&&error==TradeError.KeyQuantity,"one unique key");
        Check(!ItemTradePolicy.TryQuote(true,240,true,1,1000,1,out _,out error)&&error==TradeError.KeyOwned,"no duplicate key");
        Check(!ItemTradePolicy.TryQuote(false,1,true,1,0,1,out _,out error)&&error==TradeError.KeyCannotSell,"cannot sell key");
        Check(!ItemTradePolicy.TryQuote(false,20,false,1,int.MaxValue-19,1,out _,out error)&&error==TradeError.GoldFull,"gold overflow preserves item");
        Check(!ItemTradePolicy.TryQuote(true,1,false,int.MaxValue,100,1,out _,out error)&&error==TradeError.InventoryFull,"stack overflow preserves gold");
        int[] prices={0,1,60,300,int.MaxValue},counts={0,1,5,int.MaxValue-1,int.MaxValue},quantities={-1,0,1,2,5,int.MaxValue};
        foreach(bool purchasing in new[]{false,true})foreach(bool key in new[]{false,true})
        foreach(int price in prices)foreach(int owned in counts)foreach(int gold in counts)foreach(int quantity in quantities)
        {
            bool valid=quantity>0&&!(key&&(!purchasing||owned>0||quantity!=1))&&(purchasing||price>0);
            BigInteger cost=(BigInteger)price*quantity;
            if(valid)valid=purchasing?cost<=gold&&(BigInteger)owned+quantity<=int.MaxValue:quantity<=owned&&(BigInteger)gold+cost<=int.MaxValue;
            bool accepted=ItemTradePolicy.TryQuote(purchasing,price,key,owned,gold,quantity,out var quote,out _);
            Check(accepted==valid,"wide-number trade oracle");
            if(accepted)Check(quote.total==cost&&quote.goldAfter==(purchasing?gold-cost:gold+cost)&&quote.ownedAfter==(purchasing?(BigInteger)owned+quantity:owned-quantity),"exact conservation of items and gold");
            int max=ItemTradePolicy.Maximum(purchasing,price,key,owned,gold);
            if(max>0)Check(ItemTradePolicy.TryQuote(purchasing,price,key,owned,gold,max,out _,out _),"maximum is executable");
            if(max<int.MaxValue)Check(!ItemTradePolicy.TryQuote(purchasing,price,key,owned,gold,max+1,out _,out _),"maximum plus one rejected");
        }
        Console.WriteLine("PASS: "+checks+" production hub/search/item/trade checks: 2 hubs, 21 items, hidden search protection, stable source order, exact bulk trades and integer boundaries (.NET Unity bridges simulated)");
    }
}
