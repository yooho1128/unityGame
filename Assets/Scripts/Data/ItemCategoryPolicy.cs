using System;

namespace ShadowTheater.Data
{
    public enum ItemCategory { All, Healing, Cleanse, Heat, Record, Growth, Key }
    public sealed class ItemListQuery
    {
        public ItemCategory category;
        public string search="";
        public bool Matches(ItemData item)=>ItemCategoryPolicy.Matches(item,category,search);
    }
    public static class ItemCategoryPolicy
    {
        public static bool Matches(ItemData item,ItemCategory category,string search="")
        {
            if(item==null)return false;
            if(!string.IsNullOrWhiteSpace(search)&&(item.displayName??"").IndexOf(search.Trim(),StringComparison.OrdinalIgnoreCase)<0)return false;
            if(category==ItemCategory.All)return true;
            if(category==ItemCategory.Key)return item.isKeyItem||item.effectType==ItemEffectType.TraversalTool;
            switch(category)
            {
                case ItemCategory.Healing:return item.effectType==ItemEffectType.HealFlat||item.effectType==ItemEffectType.HealRatio||item.effectType==ItemEffectType.RestoreAndCleanse;
                case ItemCategory.Cleanse:return item.effectType==ItemEffectType.CureStatus||item.effectType==ItemEffectType.RestoreAndCleanse;
                case ItemCategory.Heat:return item.effectType==ItemEffectType.GainFP;
                case ItemCategory.Record:return item.effectType==ItemEffectType.CaptureBoost;
                case ItemCategory.Growth:return item.effectType==ItemEffectType.GainExperience;
                default:return false;
            }
        }
        public static string[] Labels=>new[]{"전체 도구","HP 회복","상태 정화","FP 회복","각본 기록","경험치","핵심 도구"};
    }
}
