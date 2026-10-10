using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace PlanBSkyscraperMod
{
    internal enum Surface { Wall, Glass, Trim, Roof, Accent, Lime, Gold }
    internal struct Point
    {
        internal float X, Y, Z;
        internal Point(float x, float y, float z) { X = x; Y = y; Z = z; }
        internal static Point Lerp(Point a, Point b, float t)
            => new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
    }
    internal sealed class DesignFace
    {
        internal Point[] Points;
        internal Surface Surface;
    }
    internal sealed class BuildingDesign
    {
        internal static readonly string[] Names = {
            "Atrium", "Terrassenhaus", "Doppelfluegel", "Kaskade", "Glasoktogon",
            "Hofwinkel", "Staffelturm", "Zwillingspaar", "Brueckenhaus", "Sechseckturm",
            "Kreuzhaus", "Versatzhaus", "Faecherhaus", "Dreifachturm", "Glasnadel",
            "Gartenwinkel", "Lamellenhaus", "Stadtbogen", "Himmelsbruecke", "Achteckterrasse",
            "Hofensemble", "Treppenhaus", "Campusfluegel", "Doppeldach", "Terrassenturm"
        };
        internal readonly List<DesignFace> Faces = new List<DesignFace>();
        internal readonly int Id;
        private readonly string customName;
        internal string Name => customName ?? Names[Id];
        internal BuildingDesign(int id,string name=null) { Id = id; customName=name; }

        internal static BuildingDesign Build(int id)
        {
            if (id < 0 || id >= Names.Length) throw new ArgumentOutOfRangeException(nameof(id));
            var d = new BuildingDesign(id);
            // Twenty-five authored arrangements, not one prefab scaled twenty-five ways.
            switch (id)
            {
                case 0: d.SlopedTower(-.19f,0,.24f,.64f,0,.83f,.075f,true,false); d.SlopedTower(.19f,0,.24f,.64f,0,1,.095f,true,true); d.Tower(0,.25f,.28f,.14f,0,.45f); break;
                case 1: d.Tower(0,0,.68f,.56f,0,.46f); d.Tower(-.08f,0,.50f,.44f,.46f,.75f); d.Tower(-.13f,0,.32f,.32f,.75f,1); break;
                case 2: d.SlopedTower(-.16f,0,.26f,.64f,0,1,.09f,true,false); d.SlopedTower(.16f,.12f,.26f,.38f,0,.72f,.055f,true,true); d.Tower(.10f,-.22f,.40f,.15f,0,.32f); break;
                case 3: d.Tower(-.22f,0,.20f,.52f,0,.52f); d.Tower(0,0,.22f,.52f,0,.76f); d.Tower(.22f,0,.20f,.52f,0,1); break;
                case 4: d.Tower(0,0,.49f,.49f,0,.88f,8); d.Tower(0,0,.36f,.36f,.88f,.97f,8); d.Crown(0,0,.36f,.36f,.97f,1.08f,8); break;
                case 5: d.Tower(-.20f,0,.22f,.66f,0,1); d.Tower(.10f,.22f,.42f,.22f,0,.69f); break;
                case 6: d.Tower(0,0,.62f,.48f,0,.36f); d.Tower(0,-.03f,.46f,.36f,.36f,.69f); d.Tower(0,-.06f,.30f,.26f,.69f,1); break;
                case 7: d.SlopedTower(-.18f,0,.26f,.40f,0,1,.075f,true,false,8); d.SlopedTower(.18f,0,.26f,.40f,0,.88f,.065f,true,true,8); d.Tower(0,.13f,.22f,.14f,0,.21f); break;
                case 8: d.Tower(-.23f,0,.22f,.47f,0,1); d.Tower(.23f,0,.22f,.47f,0,.91f); d.Tower(0,0,.34f,.25f,.58f,.71f); break;
                case 9: d.Tower(0,0,.58f,.58f,0,.90f,6); d.Tower(0,0,.40f,.40f,.90f,1.04f,6); break;
                case 10: d.Tower(0,0,.23f,.72f,0,.92f); d.Tower(0,0,.67f,.23f,0,1); break;
                case 11: d.Tower(-.06f,.06f,.52f,.46f,0,.47f); d.Tower(.06f,-.05f,.46f,.38f,.47f,.80f); d.SlopedTower(-.04f,-.07f,.30f,.29f,.80f,1,.045f,false,false); break;
                case 12: d.Tower(-.21f,-.08f,.20f,.43f,0,.63f); d.Tower(0,0,.20f,.55f,0,1); d.Tower(.21f,.08f,.20f,.43f,0,.78f); break;
                case 13: d.Tower(-.19f,-.12f,.25f,.29f,0,.77f); d.Tower(.19f,-.12f,.25f,.29f,0,1); d.Tower(0,.19f,.25f,.29f,0,.88f); break;
                case 14: d.Tower(0,0,.32f,.44f,0,.91f); d.Tower(0,0,.25f,.34f,.91f,1); d.Crown(0,0,.25f,.34f,1,1.15f,4); break;
                case 15: d.Tower(-.20f,0,.22f,.65f,0,.74f); d.Tower(.08f,.22f,.40f,.21f,0,1); d.Tower(.19f,-.16f,.20f,.31f,0,.38f); break;
                case 16: d.Tower(0,0,.54f,.42f,0,1); for(int n=0;n<4;n++) d.PlainBox(-.21f+n*.14f,0,.026f,.48f,0,1.015f,Surface.Trim); break;
                case 17: d.Tower(-.24f,0,.18f,.50f,0,.83f); d.Tower(.24f,0,.18f,.50f,0,.83f); d.Tower(0,0,.38f,.31f,.70f,1); break;
                case 18: d.SlopedTower(-.23f,0,.22f,.40f,0,1,.08f,true,true); d.SlopedTower(.23f,0,.22f,.40f,0,.92f,.065f,true,false); d.Tower(0,0,.34f,.23f,.79f,.89f); d.Tower(0,.19f,.22f,.21f,0,.29f); break;
                case 19: d.Tower(0,0,.57f,.57f,0,.78f,8); d.Tower(0,0,.41f,.41f,.78f,.96f,8); d.Crown(0,0,.41f,.41f,.96f,1.10f,8); break;
                case 20: d.Tower(-.23f,0,.18f,.65f,0,.69f); d.Tower(.23f,0,.18f,.65f,0,1); d.Tower(0,.24f,.36f,.17f,0,.40f); break;
                case 21: for(int n=0;n<4;n++) d.Tower(-.255f+n*.17f,0,.17f,.48f,0,.40f+n*.20f); break;
                case 22: d.Tower(0,0,.22f,.63f,0,1); d.Tower(-.20f,-.16f,.24f,.29f,0,.56f); d.Tower(.20f,.16f,.24f,.29f,0,.76f); break;
                case 23: d.SlopedTower(-.19f,0,.26f,.41f,0,.90f,.085f,true,false); d.SlopedTower(.19f,0,.26f,.41f,0,1,.10f,true,true); break;
                case 24: d.Tower(0,0,.63f,.53f,0,.32f); d.Tower(-.04f,0,.49f,.40f,.32f,.60f); d.Tower(-.08f,0,.35f,.29f,.60f,.83f); d.Tower(-.10f,0,.22f,.20f,.83f,1); d.Crown(-.10f,0,.22f,.20f,1,1.08f,4); break;
            }
            // Small roof service volumes, no inherited base platform.
            {
                var highest = d.Faces.Where(f=>f.Surface==Surface.Roof && f.Points.All(p=>Math.Abs(p.Y-f.Points[0].Y)<.00001f)).OrderByDescending(f=>f.Points[0].Y).FirstOrDefault();
                if(highest!=null)
                {
                float rx=highest.Points.Average(p=>p.X), rz=highest.Points.Average(p=>p.Z), ry=highest.Points[0].Y;
                d.PlainBox(rx,rz,.09f,.09f,ry,ry+.024f,Surface.Trim);
                d.PlainBox(rx+.028f,rz,.025f,.075f,ry+.024f,ry+.027f,Surface.Accent);
                d.PlainBox(rx-.028f,rz,.025f,.075f,ry+.024f,ry+.027f,Surface.Accent);
                for(int fin=0;fin<4;fin++)d.PlainBox(rx,rz-.030f+fin*.020f,.079f,.005f,ry+.027f,ry+.032f,Surface.Accent);
                }
            }
            float top=d.Faces.SelectMany(f=>f.Points).Max(p=>p.Y);
            foreach(var f in d.Faces) for(int n=0;n<f.Points.Length;n++) { f.Points[n].Y/=top; f.Points[n].X*=.95f; f.Points[n].Z*=.95f; }
            return d;
        }

        private Point[] Outline(float cx,float cz,float w,float depth,float y,int sides)
        {
            if(sides==4) return new[]{new Point(cx-w/2,y,cz-depth/2),new Point(cx+w/2,y,cz-depth/2),new Point(cx+w/2,y,cz+depth/2),new Point(cx-w/2,y,cz+depth/2)};
            var p=new Point[sides];
            for(int i=0;i<sides;i++){double a=2*Math.PI*i/sides; p[i]=new Point(cx+(float)Math.Cos(a)*w/2,y,cz+(float)Math.Sin(a)*depth/2);}
            return p;
        }
        internal void Face(Surface s,params Point[] points)=>Faces.Add(new DesignFace{Surface=s,Points=points});
        internal void PlainBox(float cx,float cz,float w,float depth,float bottom,float top,Surface s)
            => Prism(cx,cz,w,depth,bottom,top,4,s,false);
        internal void Tower(float cx,float cz,float w,float depth,float bottom,float top,int sides=4)
            => Prism(cx,cz,w,depth,bottom,top,sides,Surface.Wall,true);
        private void SlopedTower(float cx,float cz,float w,float depth,float bottom,float top,float drop,bool alongZ,bool reverse,int sides=4)
        {
            int first=Faces.Count;
            Tower(cx,cz,w,depth,bottom,top,sides);
            // Split upper geometry along the roof direction before bending. New points
            // make the curved profile real geometry, rather than just moving its corners.
            SubdivideUpperRoof(first,top-(top-bottom)*.25f,alongZ,alongZ?cz-depth/2:cx-w/2,alongZ?depth:w);
            // Deform the entire cap and its connected upper facade together. This keeps
            // glazing, reveals, frames and parapets joined to the broad shed roof.
            float shoulder=top-(top-bottom)*.25f;
            for(int face=first;face<Faces.Count;face++)for(int n=0;n<Faces[face].Points.Length;n++)
            {
                var p=Faces[face].Points[n];
                if(p.Y>shoulder)
                {
                    float t=alongZ?(p.Z-cz)/depth+.5f:(p.X-cx)/w+.5f;
                    t=Math.Max(0,Math.Min(1,t));if(reverse)t=1-t;
                    float blend=Math.Min(1,(p.Y-shoulder)/(top-shoulder));
                    float curve=t*t*(3-2*t);
                    p.Y-=drop*curve*blend;Faces[face].Points[n]=p;
                }
            }
        }
        private void SubdivideUpperRoof(int first,float shoulder,bool alongZ,float origin,float span)
        {
            const int sections=8;
            var original=Faces.Skip(first).ToArray();Faces.RemoveRange(first,Faces.Count-first);
            foreach(var face in original)
            {
                if(face.Points.Max(p=>p.Y)<=shoulder){Faces.Add(face);continue;}
                for(int section=0;section<sections;section++)
                {
                    float low=origin+span*section/sections,high=origin+span*(section+1)/sections;
                    if(section==0)low=Math.Min(low,face.Points.Min(p=>alongZ?p.Z:p.X));
                    if(section==sections-1)high=Math.Max(high,face.Points.Max(p=>alongZ?p.Z:p.X));
                    var clipped=ClipAxis(face.Points,alongZ,low,true);
                    clipped=ClipAxis(clipped,alongZ,high,false);
                    var clean=new List<Point>();
                    foreach(var p in clipped)
                        if(clean.Count==0||DistanceSquared(clean[clean.Count-1],p)>1e-12f)clean.Add(p);
                    if(clean.Count>1&&DistanceSquared(clean[0],clean[clean.Count-1])<1e-12f)clean.RemoveAt(clean.Count-1);
                    bool changed=true;
                    while(changed&&clean.Count>=3)
                    {
                        changed=false;
                        for(int n=0;n<clean.Count;n++)
                        {
                            var a=clean[(n+clean.Count-1)%clean.Count];var b=clean[n];var c=clean[(n+1)%clean.Count];
                            float ux=b.X-a.X,uy=b.Y-a.Y,uz=b.Z-a.Z,vx=c.X-b.X,vy=c.Y-b.Y,vz=c.Z-b.Z;
                            float nx=uy*vz-uz*vy,ny=uz*vx-ux*vz,nz=ux*vy-uy*vx;
                            if(nx*nx+ny*ny+nz*nz<1e-14f){clean.RemoveAt(n);changed=true;break;}
                        }
                    }
                    if(clean.Count>=3)Face(face.Surface,clean.ToArray());
                }
            }
        }
        private static float DistanceSquared(Point a,Point b)
            =>(a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y)+(a.Z-b.Z)*(a.Z-b.Z);
        private static Point[] ClipAxis(Point[] polygon,bool alongZ,float boundary,bool keepAbove)
        {
            var result=new List<Point>();if(polygon.Length==0)return result.ToArray();
            var a=polygon[polygon.Length-1];float da=(alongZ?a.Z:a.X)-boundary;
            foreach(var b in polygon)
            {
                float db=(alongZ?b.Z:b.X)-boundary;bool insideA=keepAbove?da>=0:da<=0,insideB=keepAbove?db>=0:db<=0;
                if(insideA!=insideB)result.Add(Point.Lerp(a,b,da/(da-db)));
                if(insideB)result.Add(b);a=b;da=db;
            }
            return result.ToArray();
        }
        private void Prism(float cx,float cz,float w,float depth,float bottom,float top,int sides,Surface wall,bool windows)
        {
            var lo=Outline(cx,cz,w,depth,bottom,sides); var hi=Outline(cx,cz,w,depth,top,sides);
            int floors=Math.Max(1,(int)Math.Round((top-bottom)*VariantRules.Heights[Id%5]/.055));
            for(int i=0;i<sides;i++)
            {
                int j=(i+1)%sides;
                Face(Surface.Roof,new Point(cx,top,cz),hi[j],hi[i]);
                Face(wall,new Point(cx,bottom,cz),lo[i],lo[j]);
                if(!windows){Face(wall,lo[i],hi[i],hi[j],lo[j]);continue;}
                float dx=lo[j].X-lo[i].X,dz=lo[j].Z-lo[i].Z;
                float len=(float)Math.Sqrt(dx*dx+dz*dz), nx=dz/len*.0015f,nz=-dx/len*.0015f;
                float left=.12f+(Id+i)%3*.07f;
                float right=left+((Id+i)%2==0?.34f:.22f);
                float low=bottom+(top-bottom)*(.035f+((Id+i)%3)*.035f);
                float high=bottom+(top-bottom)*(.78f+((Id+i)%3)*.065f);
                float recessDepth=Math.Min(.026f,len*.10f);
                var openings=new List<FacadeInset>{new FacadeInset(left,right,low,high,recessDepth,Surface.Glass)};
                if((Id+i)%2==0)
                    openings.Add(new FacadeInset(.77f,.83f,bottom+(top-bottom)*.24f,
                        bottom+(top-bottom)*.91f,recessDepth*.7f,Surface.Glass));
                // Real narrow slots in opaque wall areas, not painted surface lines.
                for(int band=1;band<3;band++)
                {
                    float level=bottom+(top-bottom)*(band==1?.31f:.69f);
                    openings.Add(new FacadeInset(right+.025f,.97f,level,level+.004f,recessDepth*.55f,Surface.Accent));
                }
                RecessedFacade(lo[i],lo[j],bottom,top,openings,wall);
                // Projecting structural frames have front, side and cap faces.
                Blade(lo[i],lo[j],left-.024f,left-.006f,low,high+.008f,nx*7,nz*7,false);
                Blade(lo[i],lo[j],right+.006f,right+.018f,low,high+.008f,nx*5,nz*5,false);
                if((Id+i)%3==0)
                    Blade(lo[i],lo[j],.035f,.12f,bottom,top+.045f,nx*9,nz*9);
                // Raised roof edge with inner face and cap; terraces remain flat.
                float parapet=top+.009f,insideX=-nx*2,insideZ=-nz*2;
                var outerA=new Point(lo[i].X,parapet,lo[i].Z);var outerB=new Point(lo[j].X,parapet,lo[j].Z);
                var innerA=new Point(outerA.X+insideX,parapet,outerA.Z+insideZ);
                var innerB=new Point(outerB.X+insideX,parapet,outerB.Z+insideZ);
                Face(Surface.Trim,hi[i],outerA,outerB,hi[j]);
                Face(Surface.Roof,outerA,innerA,innerB,outerB);
                Face(Surface.Trim,new Point(innerB.X,top,innerB.Z),innerB,innerA,new Point(innerA.X,top,innerA.Z));
                if(bottom==0 && i==0)
                {
                    // Glazed entry with a thin projecting canopy, no full-width plinth.
                    float ex=(lo[i].X+lo[j].X)/2,ez=(lo[i].Z+lo[j].Z)/2;
                    float entryTop=Math.Min(top,.07f/VariantRules.Heights[Id%5]);
                    PlainBox(ex+nx*4,ez+nz*4,.09f,.023f,entryTop,entryTop+.007f,Surface.Trim);
                }
            }
        }
        private void Panel(Point start,Point end,float left,float right,float bottom,float top,
            float offsetX,float offsetZ,Surface surface,int pieces)
        {
            var a=Point.Lerp(start,end,left);var b=Point.Lerp(start,end,right);
            a.X+=offsetX;a.Z+=offsetZ;b.X+=offsetX;b.Z+=offsetZ;
            for(int n=0;n<pieces;n++)
            {
                float y0=bottom+(top-bottom)*n/pieces,y1=bottom+(top-bottom)*(n+1)/pieces;
                Face(surface,new Point(a.X,y0,a.Z),new Point(a.X,y1,a.Z),new Point(b.X,y1,b.Z),new Point(b.X,y0,b.Z));
            }
        }

        private sealed class FacadeInset
        {
            internal float Left,Right,Bottom,Top,Depth;
            internal Surface Surface;
            internal FacadeInset(float left,float right,float bottom,float top,float depth,Surface surface)
            {Left=left;Right=right;Bottom=bottom;Top=top;Depth=depth;Surface=surface;}
        }

        private void RecessedFacade(Point start,Point end,float bottom,float top,List<FacadeInset> openings,Surface wall)
        {
            float dx=end.X-start.X,dz=end.Z-start.Z,len=(float)Math.Sqrt(dx*dx+dz*dz);
            float normalX=dz/len,normalZ=-dx/len;
            var xs=new SortedSet<float>{0,1};var ys=new SortedSet<float>{bottom,top};
            foreach(var hole in openings){xs.Add(hole.Left);xs.Add(hole.Right);ys.Add(hole.Bottom);ys.Add(hole.Top);}
            var x=MergeCoordinates(xs);var y=MergeCoordinates(ys);
            // Tessellate the wall around holes. No wall remains in front of the glass.
            for(int row=0;row<y.Length-1;row++)for(int col=0;col<x.Length-1;col++)
            {
                float mx=(x[col]+x[col+1])/2,my=(y[row]+y[row+1])/2;
                var hole=openings.FirstOrDefault(h=>mx>h.Left && mx<h.Right && my>h.Bottom && my<h.Top);
                float depth=hole?.Depth??0;
                Panel(start,end,x[col],x[col+1],y[row],y[row+1],-normalX*depth,-normalZ*depth,hole?.Surface??wall,1);
            }
            foreach(var hole in openings)
            {
                var a=Point.Lerp(start,end,hole.Left);var b=Point.Lerp(start,end,hole.Right);
                var front=new[]{new Point(a.X,hole.Bottom,a.Z),new Point(a.X,hole.Top,a.Z),new Point(b.X,hole.Top,b.Z),new Point(b.X,hole.Bottom,b.Z)};
                var back=front.Select(p=>new Point(p.X-normalX*hole.Depth,p.Y,p.Z-normalZ*hole.Depth)).ToArray();
                for(int edge=0;edge<4;edge++)
                    Face(Surface.Accent,front[edge],front[(edge+1)%4],back[(edge+1)%4],back[edge]);
                if(hole.Surface==Surface.Glass)
                {
                    // Metal crossbars sit inside the reveal, clear of the wall face.
                    int bars=Math.Max(1,Math.Min(5,(int)((hole.Top-hole.Bottom)/.20f)));
                    var insetStart=new Point(start.X-normalX*(hole.Depth-.003f),0,start.Z-normalZ*(hole.Depth-.003f));
                    var insetEnd=new Point(end.X-normalX*(hole.Depth-.003f),0,end.Z-normalZ*(hole.Depth-.003f));
                    for(int n=1;n<=bars;n++)
                    {
                        float level=hole.Bottom+(hole.Top-hole.Bottom)*n/(bars+1);
                        Blade(insetStart,insetEnd,hole.Left,hole.Right,level,level+.0035f,normalX*.003f,normalZ*.003f,false);
                    }
                }
            }
        }

        private static float[] MergeCoordinates(IEnumerable<float> sorted)
        {
            var result=new List<float>();
            foreach(float value in sorted)
                if(result.Count==0 || value-result[result.Count-1]>.00001f)result.Add(value);
            return result.ToArray();
        }

        private void Blade(Point start,Point end,float left,float right,float bottom,float top,float ox,float oz,bool slanted=true)
        {
            var a=Point.Lerp(start,end,left);var b=Point.Lerp(start,end,right);
            var c=new Point(b.X+ox,0,b.Z+oz);var d=new Point(a.X+ox,0,a.Z+oz);
            var lo=new[]{new Point(a.X,bottom,a.Z),new Point(b.X,bottom,b.Z),new Point(c.X,bottom,c.Z),new Point(d.X,bottom,d.Z)};
            float endTop=slanted?top-.028f:top;
            var hi=new[]{new Point(a.X,top,a.Z),new Point(b.X,endTop,b.Z),new Point(c.X,endTop,c.Z),new Point(d.X,top,d.Z)};
            // Offset white blade with an oblique slab end, not a pyramid or roof spire.
            for(int n=0;n<4;n++)Face(Surface.Trim,lo[n],lo[(n+1)%4],hi[(n+1)%4],hi[n]);
            Face(Surface.Roof,hi[0],hi[1],hi[2],hi[3]);
        }

        private void Crown(float cx,float cz,float w,float depth,float bottom,float top,int sides)
        {
            // A low flat roof pavilion replaces every former pointed crown.
            Prism(cx,cz,w*.76f,depth*.76f,bottom,bottom+.025f,sides,Surface.Trim,true);
        }

        internal string Obj()
        {
            var s=new StringBuilder("# Original PlanBSkyscraperMod geometry; normalized height 1\nmtllib Palette.mtl\n");
            int index=1;
            foreach(var face in Faces)
            {
                s.Append("usemtl ").Append(face.Surface).Append('\n');
                foreach(var p in face.Points)s.AppendFormat(CultureInfo.InvariantCulture,"v {0:R} {1:R} {2:R}\n",p.X,p.Y,p.Z);
                for(int i=1;i<face.Points.Length-1;i++)s.AppendFormat("f {0} {1} {2}\n",index,index+i,index+i+1);
                index+=face.Points.Length;
            }
            return s.ToString();
        }
    }
}


