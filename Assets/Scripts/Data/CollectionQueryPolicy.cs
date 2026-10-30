using System;
using System.Collections.Generic;
using System.Linq;

namespace ShadowTheater.Data
{
    public enum CollectionSort { Original, Name, LevelDescending, LevelAscending }
    public sealed class ShadowSearchEntry
    {
        public string key, name, baseName, title;
        public int index, element, level;
        public bool seen, recorded;
    }
    /// <summary>View-only filtering. Unknown names/types cannot be revealed by a search.</summary>
    public sealed class CollectionQuery
    {
        public string search = "";
        public int element = -1;
        public CollectionSort sort;
        public bool Matches(ShadowSearchEntry entry)
        {
            if(entry==null)return false;
            if(element>=0&&(!entry.recorded||entry.element!=element))return false;
            string[] tokens=(search??"").Split(new[]{' ','\t','\r','\n'},StringSplitOptions.RemoveEmptyEntries);
            if(tokens.Length>0&&!entry.seen)return false;
            foreach(string token in tokens)
                if(!Contains(entry.name,token)&&!Contains(entry.baseName,token)&&!(entry.recorded&&Contains(entry.title,token)))return false;
            return true;
        }
        private static bool Contains(string text,string token)=>(text??"").IndexOf(token,StringComparison.OrdinalIgnoreCase)>=0;
        public List<ShadowSearchEntry> Apply(IEnumerable<ShadowSearchEntry> source)
        {
            var entries=(source??Enumerable.Empty<ShadowSearchEntry>()).Where(Matches).ToList();
            entries.Sort(Compare);return entries;
        }
        private int Compare(ShadowSearchEntry a,ShadowSearchEntry b)
        {
            int result=0;
            if(sort==CollectionSort.Name)
            {
                result=b.seen.CompareTo(a.seen);
                if(result==0&&a.seen)result=StringComparer.OrdinalIgnoreCase.Compare(a.name,b.name);
            }
            else if(sort==CollectionSort.LevelAscending||sort==CollectionSort.LevelDescending)
            {
                result=(b.level>0).CompareTo(a.level>0);
                if(result==0)result=sort==CollectionSort.LevelAscending?a.level.CompareTo(b.level):b.level.CompareTo(a.level);
            }
            return result!=0?result:a.index.CompareTo(b.index);
        }
        public void Reset(){search="";element=-1;sort=CollectionSort.Original;}
    }
}
