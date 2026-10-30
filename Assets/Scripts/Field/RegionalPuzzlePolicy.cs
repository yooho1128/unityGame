using System;
using System.Collections.Generic;
using System.Linq;

namespace ShadowTheater.Field
{
    [Serializable] public sealed class RegionalPuzzleDefinition
    {
        public string id,regionId,title,kind,clue,completionText;
        public LayoutCell board;public LayoutCell[] nodes,exits;
        public string[] nodeNames;public int[] sequence,toggleMasks,rotations;public int goalMask;
        public string CompletionFlag=>"regional_puzzle_"+id+"_complete";
    }
    [Serializable] public sealed class RegionalPuzzleCatalogData {public RegionalPuzzleDefinition[] entries;}
    public sealed class RegionalPuzzleState
    {
        public readonly RegionalPuzzleDefinition definition;
        private readonly List<int> prefix=new List<int>();
        private readonly int[] rotations;
        public int Mask {get;private set;}
        public int Progress=>prefix.Count;
        public bool Solved {get;private set;}
        public RegionalPuzzleState(RegionalPuzzleDefinition definition,bool solved=false)
        {
            if(!Valid(definition))throw new ArgumentException("Invalid regional puzzle definition");
            this.definition=definition;rotations=new int[definition.nodes.Length];Solved=solved;
            if(solved)
            {Mask=definition.goalMask;if(definition.kind=="Mirrors")Array.Copy(definition.rotations,rotations,rotations.Length);}
        }
        public static bool Valid(RegionalPuzzleDefinition d)
        {
            if(d==null||string.IsNullOrWhiteSpace(d.id)||string.IsNullOrWhiteSpace(d.regionId)||string.IsNullOrWhiteSpace(d.clue)||
                d.nodes==null||d.nodes.Length!=3||d.nodeNames==null||d.nodeNames.Length!=d.nodes.Length||d.exits?.Length!=2)return false;
            if(d.kind=="Sequence")return d.sequence?.Length>0&&d.sequence.All(i=>i>=0&&i<d.nodes.Length);
            if(d.kind=="Mirrors")return d.rotations?.Length==d.nodes.Length&&d.rotations.All(r=>r>=0&&r<4)&&d.rotations.Any(r=>r!=0);
            return d.kind=="Toggle"&&d.goalMask>0&&d.goalMask<8&&d.toggleMasks?.Length==d.nodes.Length&&d.toggleMasks.All(m=>m>0&&m<8);
        }
        public bool Activate(int node)
        {
            if(Solved||node<0||node>=definition.nodes.Length)return false;
            if(definition.kind=="Sequence")
            {
                prefix.Add(node);
                while(prefix.Count>0&&!prefix.SequenceEqual(definition.sequence.Take(prefix.Count)))prefix.RemoveAt(0);
                Solved=prefix.Count==definition.sequence.Length;
            }
            else if(definition.kind=="Toggle")
            {Mask^=definition.toggleMasks[node];Solved=Mask==definition.goalMask;}
            else
            {rotations[node]=(rotations[node]+1)%4;Solved=rotations.SequenceEqual(definition.rotations);}
            return true;
        }
        public string Status(int node)
        {
            if(node<0||node>=definition.nodes.Length)return "";
            if(Solved)return "✓ 복원";
            if(definition.kind=="Sequence")return prefix.Contains(node)?"✓ 확인":"미확인";
            if(definition.kind=="Toggle")return (Mask&(1<<node))!=0?"켜짐":"꺼짐";
            return new[]{"↑ 북","→ 동","↓ 남","← 서"}[rotations[node]];
        }
        public string Summary=>Solved?"복원 완료 · 지름길 개방":definition.kind=="Sequence"?$"순서 {Progress}/{definition.sequence.Length}":
            string.Join(" · ",Enumerable.Range(0,definition.nodes.Length).Select(i=>(i+1)+" "+Status(i)));
    }
}
