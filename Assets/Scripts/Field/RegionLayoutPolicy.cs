using System;
using System.Collections.Generic;

namespace ShadowTheater.Field
{
    [Serializable] public struct LayoutCell
    {
        public int x, y;
        public LayoutCell(int x, int y) { this.x = x; this.y = y; }
    }
    [Serializable] public sealed class RegionLayoutDefinition
    { public string regionId, profile; public LayoutCell[] anchors; }
    [Serializable] public sealed class RegionLayoutCatalog
    { public RegionLayoutDefinition[] entries; }
    public enum FieldTileKind { Grass, Stone, Path, Wood, Water, Tree, Brick, Cliff, Bush }

    /// <summary>Deterministic geography, independent of Unity. All open cells form one component.</summary>
    public sealed class RegionLayoutPlan
    {
        public const int Width = 23, Height = 19, Count = Width * Height;
        public readonly FieldTileKind[] ground = new FieldTileKind[Count];
        public readonly FieldTileKind?[] obstacles = new FieldTileKind?[Count];
        public static int Index(int x, int y) => (y + 9) * Width + x + 11;
        public static LayoutCell Cell(int index) => new LayoutCell(index % Width - 11, index / Width - 9);
        public static bool Interior(int x, int y) => x > -11 && x < 11 && y > -9 && y < 9;
        public bool Blocked(int x, int y) => !Interior(x, y) || obstacles[Index(x, y)].HasValue;
    }
    public static class RegionLayoutPolicy
    {
        private static readonly int[] Dx = { 0, -1, 1, 0 }, Dy = { -1, 0, 0, 1 };
        public static RegionLayoutPlan Build(RegionLayoutDefinition definition, IEnumerable<LayoutCell> extra = null)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var plan = new RegionLayoutPlan();
            int seed = 0;
            foreach (char c in definition.regionId ?? "") seed = unchecked(seed * 31 + c);
            bool mirror = (seed & 1) != 0;
            for (int y = -9; y <= 9; y++)
            for (int x = -11; x <= 11; x++)
            {
                int i = RegionLayoutPlan.Index(x,y), u = mirror ? -x : x;
                FieldTileKind floor = FieldTileKind.Stone;
                FieldTileKind? obstacle = null;
                switch (definition.profile)
                {
                    case "Village":
                        floor = Math.Abs(x) <= 2 || Math.Abs(y) <= 1 ? FieldTileKind.Path : FieldTileKind.Stone;
                        if (Math.Abs(x)>=4 && Math.Abs(x)<=7 && Math.Abs(y)>=3 && Math.Abs(y)<=6) obstacle=FieldTileKind.Brick;
                        break;
                    case "Forest":
                        int bend = y < -2 ? -3 : y > 2 ? 3 : 0;
                        floor = Math.Abs(u-bend)<=1 || Math.Abs(y+u/4)<=1 ? FieldTileKind.Path : FieldTileKind.Grass;
                        if (floor!=FieldTileKind.Path && ((u+12)*(u+12)+(y+10)*3+(seed&7))%11<4) obstacle=FieldTileKind.Tree;
                        break;
                    case "Catacomb":
                        floor = Math.Abs(x)<=1 || Math.Abs(y-2)<=1 ? FieldTileKind.Path : FieldTileKind.Stone;
                        if ((Math.Abs(x)==3 || Math.Abs(x)==7) && y>=-6 && y<=6 && y!=2 && y!=3 && y!=-3) obstacle=FieldTileKind.Brick;
                        if (Math.Abs(x)>=4 && Math.Abs(x)<=6 && (y==-5 || y==5)) obstacle=FieldTileKind.Cliff;
                        break;
                    case "Archive":
                        floor = Math.Abs(y)<=1 || Math.Abs(x)>=8 ? FieldTileKind.Path : FieldTileKind.Wood;
                        if ((u==-6 || u==-2 || u==3 || u==6) && Math.Abs(y)>=3 && Math.Abs(y)<=6) obstacle=FieldTileKind.Brick;
                        break;
                    case "Coast":
                        floor = y<=-3 ? FieldTileKind.Wood : FieldTileKind.Path;
                        if (y>=3 && u>=2+y/3 || y>=6 && u<=-5) obstacle=FieldTileKind.Water;
                        if (y<=-5 && u<=-5 && (u+y)%3==0) obstacle=FieldTileKind.Cliff;
                        break;
                    case "Marsh":
                        floor = Math.Abs(u-y/2)<=1 || y==-2 || y==-1 ? FieldTileKind.Wood : FieldTileKind.Grass;
                        if (floor!=FieldTileKind.Wood && (u*u+y*y+(seed&7))%13<6) obstacle=FieldTileKind.Water;
                        break;
                    case "Workshop":
                        floor = y%4==0 || Math.Abs(x)<=1 ? FieldTileKind.Path : FieldTileKind.Wood;
                        if (Math.Abs(u)>=3 && Math.Abs(u)<=7 && (y==-5 || y==3) && (u<0 || u>4)) obstacle=FieldTileKind.Brick;
                        if ((u==-6 || u==5) && (y==-4 || y==4)) obstacle=FieldTileKind.Cliff;
                        break;
                    case "Stage":
                        floor = Math.Abs(x)<=3 ? FieldTileKind.Wood : FieldTileKind.Stone;
                        if (Math.Abs(x)>=5 && Math.Abs(x)<=7 && Math.Abs(y)>=2 && Math.Abs(y)<=6) obstacle=FieldTileKind.Brick;
                        break;
                    default:
                        floor=FieldTileKind.Path;
                        if (Math.Abs(u-y/3)>=3 && ((u+11)*7+(y+9)*3+(seed&15))%17<5) obstacle=FieldTileKind.Cliff;
                        break;
                }
                plan.ground[i]=floor;
                plan.obstacles[i]=RegionLayoutPlan.Interior(x,y)?obstacle:FieldTileKind.Cliff;
            }
            // Stable centre and all authored interaction/landing cells, with approach space.
            Clear(plan,0,0);
            foreach (var anchor in definition.anchors ?? Array.Empty<LayoutCell>()) Reserve(plan,anchor);
            if (extra!=null) foreach (var anchor in extra) Reserve(plan,anchor);
            Connect(plan);
            return plan;
        }
        private static void Reserve(RegionLayoutPlan plan, LayoutCell cell)
        {
            // A 3x3 approach area lets the player walk around a solid NPC at the centre.
            for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++) Clear(plan,cell.x+x,cell.y+y);
        }
        private static void Clear(RegionLayoutPlan plan,int x,int y)
        { if(RegionLayoutPlan.Interior(x,y))plan.obstacles[RegionLayoutPlan.Index(x,y)]=null; }
        public static HashSet<int> Reachable(RegionLayoutPlan plan)
        {
            var reached=new HashSet<int>();var queue=new Queue<int>();int start=RegionLayoutPlan.Index(0,0);
            reached.Add(start);queue.Enqueue(start);
            while(queue.Count>0)
            {
                var cell=RegionLayoutPlan.Cell(queue.Dequeue());
                for(int d=0;d<4;d++)
                {
                    int x=cell.x+Dx[d],y=cell.y+Dy[d];
                    if(!plan.Blocked(x,y)&&reached.Add(RegionLayoutPlan.Index(x,y)))queue.Enqueue(RegionLayoutPlan.Index(x,y));
                }
            }
            return reached;
        }
        private static void Connect(RegionLayoutPlan plan)
        {
            var reached=Reachable(plan);
            for(int i=0;i<RegionLayoutPlan.Count;i++)
            {
                var cell=RegionLayoutPlan.Cell(i);
                if(plan.Blocked(cell.x,cell.y)||reached.Contains(i))continue;
                // Dijkstra prefers existing corridors, only cutting walls when necessary.
                var distance=new int[RegionLayoutPlan.Count];var previous=new int[RegionLayoutPlan.Count];
                var done=new bool[RegionLayoutPlan.Count];
                for(int j=0;j<distance.Length;j++){distance[j]=int.MaxValue;previous[j]=-1;}
                distance[i]=0;int destination=-1;
                while(true)
                {
                    int best=-1;
                    for(int j=0;j<distance.Length;j++)if(!done[j]&&distance[j]!=int.MaxValue&&(best<0||distance[j]<distance[best]))best=j;
                    if(best<0)throw new InvalidOperationException("Cannot connect interior geography");
                    if(reached.Contains(best)){destination=best;break;}
                    done[best]=true;var at=RegionLayoutPlan.Cell(best);
                    for(int d=0;d<4;d++)
                    {
                        int x=at.x+Dx[d],y=at.y+Dy[d];if(!RegionLayoutPlan.Interior(x,y))continue;
                        int n=RegionLayoutPlan.Index(x,y),cost=distance[best]+(plan.obstacles[n].HasValue?8:1);
                        if(cost<distance[n]){distance[n]=cost;previous[n]=best;}
                    }
                }
                for(int at=destination;at>=0;at=previous[at])
                {plan.obstacles[at]=null;if(at==i)break;}
                reached=Reachable(plan);
            }
        }
    }
}
