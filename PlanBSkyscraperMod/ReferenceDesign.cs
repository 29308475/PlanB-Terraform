using System;
using System.Collections.Generic;
using System.Linq;

namespace PlanBSkyscraperMod
{
    internal static class ReferenceDesign
    {
        internal const int Count=5;
        internal static readonly string[] Names={"CurvedSkyscraper","TripleServerHall","TriangularHQ","SteppedTransmitter","GeodesicDome"};
        internal static readonly float[] PreviewHeights={.90f,.40f,.65f,.90f,.26f};
        internal static bool IsSmall(int id)=>id==1||id==4;
        internal static int Select(bool tower,uint hash)=>tower?new[]{0,2,3}[hash%3]:new[]{1,4}[hash%2];
        internal static BuildingDesign Build(int id)
        {
            if(id<0||id>=Count)throw new ArgumentOutOfRangeException(nameof(id));
            var d=new BuildingDesign(id,Names[id]);
            switch(id){case 0:CurvedSkyscraper(d);break;case 1:TripleServerHall(d);break;case 2:TriangularHQ(d);break;case 3:SteppedTransmitter(d);break;case 4:GeodesicDome(d);break;}
            var points=d.Faces.SelectMany(f=>f.Points).ToArray();float bottom=points.Min(v=>v.Y),top=points.Max(v=>v.Y);
            float radial=points.Max(v=>(float)Math.Sqrt(v.X*v.X+v.Z*v.Z)),scale=Math.Min(1,.46f/radial);
            foreach(var f in d.Faces)for(int n=0;n<f.Points.Length;n++){var v=f.Points[n];v.X*=scale;v.Z*=scale;v.Y=(v.Y-bottom)/(top-bottom);f.Points[n]=v;}
            return d;
        }
        private static void CurvedSkyscraper(BuildingDesign d)
        {
            d.PlainBox(0,0,.78f,.48f,0,.035f,Surface.Trim);
            d.PlainBox(0,0,.67f,.39f,.035f,.135f,Surface.Wall);
            d.PlainBox(0,-.198f,.61f,.008f,.055f,.108f,Surface.Glass);
            d.PlainBox(0,.198f,.61f,.008f,.055f,.108f,Surface.Glass);
            // Segmented solid wall follows a smooth asymmetric roof, with blue roof ribbon.
            const int slices=12;
            for(int n=0;n<slices;n++)
            {
                float x0=-.30f+n*.05f,x1=x0+.05f,t0=n/(float)slices,t1=(n+1)/(float)slices;
                float y0=.94f-.39f*(3*t0*t0-2*t0*t0*t0),y1=.94f-.39f*(3*t1*t1-2*t1*t1*t1);
                Extrude(d,new[]{P(x0,.135f),P(x1,.135f),P(x1,y1),P(x0,y0)},-.135f,.135f,Surface.Wall);
                d.Face(Surface.Glass,new Point(x0,y0+.001f,-.080f),new Point(x0,y0+.001f,.080f),new Point(x1,y1+.001f,.080f),new Point(x1,y1+.001f,-.080f));
                if(n%2==0)for(int side=-1;side<=1;side+=2)
                    d.PlainBox((x0+x1)/2,side*.138f,.022f,.005f,.17f,Math.Min(y0,y1)-.055f,Surface.Glass);
            }
            for(int side=-1;side<=1;side+=2)for(int n=0;n<5;n++)d.PlainBox(-.31f,side*.141f,.019f,.006f,.19f+n*.13f,.195f+n*.13f,Surface.Trim);
            for(int side=-1;side<=1;side+=2)Strut(d,P(side*.07f,.04f),P(side*.05f,.15f),.024f,-.25f,-.20f,Surface.Trim);
            d.PlainBox(0,-.233f,.09f,.012f,.04f,.11f,Surface.Glass);
            d.PlainBox(-.25f,0,.10f,.13f,.94f,.985f,Surface.Wall);
            d.PlainBox(-.25f,0,.016f,.016f,.985f,1.06f,Surface.Trim);
            d.PlainBox(-.25f,0,.009f,.009f,1.06f,1.09f,Surface.Glass);
        }
        private static void TripleServerHall(BuildingDesign d)
        {
            for(int n=0;n<3;n++)
            {
                float x=(n-1)*.27f,h=.63f+(n==1?.035f:0);
                d.PlainBox(x,0,.205f,.28f,0,h,Surface.Wall);
                for(int side=-1;side<=1;side+=2)
                {
                    d.PlainBox(x,side*.142f,.072f,.005f,.095f,h-.035f,Surface.Glass);
                    for(int k=1;k<5;k++)d.PlainBox(x,side*.147f,.077f,.007f,.10f+k*.085f,.108f+k*.085f,Surface.Accent);
                    for(int j=-1;j<=1;j+=2)d.PlainBox(x+j*.056f,side*.155f,.018f,.022f,.07f,h-.018f,Surface.Trim);
                    d.PlainBox(x,side*.160f,.10f,.020f,.068f,.092f,Surface.Trim);
                    d.PlainBox(x,side*.145f,.07f,.006f,.006f,.065f,Surface.Accent);
                }
                d.PlainBox(x,0,.17f,.25f,h,h+.025f,Surface.Trim);
                d.PlainBox(x,0,.072f,.085f,h+.025f,h+.042f,Surface.Accent);
                d.PlainBox(x,0,.020f,.020f,h+.042f,h+.088f,Surface.Accent);
                d.PlainBox(x,0,.037f,.037f,h+.088f,h+.15f,Surface.Gold);
                for(int j=-1;j<=1;j+=2)d.PlainBox(x+j*.091f,-.06f,.04f,.20f,0,.07f,Surface.Trim);
            }
            for(int side=-1;side<=1;side+=2)
            {
                d.PlainBox(side*.135f,0,.12f,.19f,.36f,.48f,Surface.Wall);
                for(int n=0;n<2;n++)d.PlainBox(side*.135f,-.098f,.073f,.007f,.39f+n*.034f,.407f+n*.034f,Surface.Accent);
            }
        }
        private static void TriangularHQ(BuildingDesign d)
        {
            d.PlainBox(0,0,.82f,.44f,0,.028f,Surface.Trim);
            // Solid triangular glazed volume; open white A-frame stands proud of both sides.
            Extrude(d,new[]{P(-.34f,.028f),P(.34f,.028f),P(0,.72f)},-.12f,.12f,Surface.Glass);
            for(int side=-1;side<=1;side+=2)
            {
                for(int face=-1;face<=1;face+=2)
                    Strut(d,P(side*.39f,.028f),P(side*.04f,.70f),.025f,face*.15f-.025f,face*.15f+.025f,Surface.Trim);
                for(int n=0;n<6;n++)
                {
                    float y=.08f+n*.09f,w=.30f*(1-y/.72f);
                    d.PlainBox(0,side*.125f,w*2,.006f,y,y+.005f,Surface.Accent);
                }
                d.PlainBox(side*.34f,0,.16f,.38f,.028f,.12f,Surface.Wall);
                d.PlainBox(side*.34f,-.195f,.12f,.008f,.06f,.092f,Surface.Glass);
                // Separated angled crown blades with blue recessed insert.
                Extrude(d,new[]{P(side*.073f-.030f,.65f),P(side*.073f+.030f,.65f),P(side*.15f+.030f,.77f),P(side*.11f+.017f,1f),P(side*.11f-.012f,.96f),P(side*.15f-.030f,.77f)},-.063f,.063f,Surface.Trim);
                Strut(d,P(side*.10f,.76f),P(side*.11f,.94f),.009f,-.067f,-.064f,Surface.Glass);
            }
            d.PlainBox(0,0,.055f,.17f,.06f,.84f,Surface.Wall);
            d.PlainBox(0,-.091f,.018f,.008f,.16f,.83f,Surface.Gold);
            d.PlainBox(0,-.195f,.105f,.09f,.028f,.125f,Surface.Glass);
            for(int side=-1;side<=1;side+=2)Strut(d,P(side*.078f,.028f),P(0,.18f),.015f,-.25f,-.205f,Surface.Trim);
            d.PlainBox(0,-.218f,.13f,.11f,.11f,.132f,Surface.Trim);
            for(int side=-1;side<=1;side+=2)
                d.PlainBox(side*.037f,-.246f,.007f,.006f,.038f,.095f,Surface.Gold);
        }
        private static void SteppedTransmitter(BuildingDesign d)
        {
            // Four hollow support legs surround the open base instead of a filled pyramid.
            for(int i=0;i<4;i++)
            {
                float a=(float)(i*Math.PI/2+Math.PI/4),cx=(float)Math.Cos(a),cz=(float)Math.Sin(a);
                for(int n=0;n<3;n++)
                {
                    float t=n/3f,u=(n+1)/3f;
                    Beam(d,new Point(cx*(.39f-.21f*t),t*.31f,cz*(.39f-.21f*t)),new Point(cx*(.39f-.21f*u),u*.31f,cz*(.39f-.21f*u)),.032f,Surface.Trim);
                }
                d.PlainBox(cx*.39f,cz*.39f,.09f,.09f,0,.035f,Surface.Accent);
            }
            Frustum(d,.31f,.43f,.22f,.11f,Surface.Wall,8);
            Frustum(d,.43f,.46f,.15f,.15f,Surface.Accent,8);
            Frustum(d,.46f,.60f,.105f,.079f,Surface.Wall,8);
            Frustum(d,.60f,.62f,.115f,.115f,Surface.Accent,8);
            Frustum(d,.62f,.77f,.085f,.080f,Surface.Wall,8);
            Frustum(d,.77f,.79f,.093f,.093f,Surface.Accent,8);
            Frustum(d,.79f,.91f,.061f,.050f,Surface.Wall,8);
            Frustum(d,.91f,.93f,.055f,.038f,Surface.Accent,8);
            Frustum(d,.93f,.985f,.016f,.016f,Surface.Accent,6);
            Frustum(d,.985f,1.08f,.027f,.0015f,Surface.Trim,6);
            for(int level=0;level<4;level++)for(int side=0;side<4;side++)
            {
                float y=new[]{.37f,.53f,.69f,.84f}[level],r=new[]{.171f,.098f,.085f,.057f}[level];
                if(side%2==0)d.PlainBox(0,side==0?-r:r,.066f,.006f,y,y+.012f,Surface.Glass);
                else d.PlainBox(side==1?-r:r,0,.006f,.066f,y,y+.012f,Surface.Glass);
            }
        }
        private static void GeodesicDome(BuildingDesign d)
        {
            // Four staggered latitude rings, triangular panels, square white edge beams.
            const int rings=3,sectors=10;float r=.43f;
            var v=new Point[rings,sectors];var edges=new HashSet<string>();
            for(int j=0;j<rings;j++)for(int n=0;n<sectors;n++)
            {double lat=j*Math.PI/(2*rings),a=(n+(j%2)*.5)*2*Math.PI/sectors;v[j,n]=new Point(r*(float)(Math.Cos(lat)*Math.Cos(a)),.018f+r*(float)Math.Sin(lat),r*(float)(Math.Cos(lat)*Math.Sin(a)));}
            Action<Point,Point,Point> tri=(a,b,c)=>
            {
                var u=P(b.X-a.X,b.Y-a.Y);u.Z=b.Z-a.Z;var w=P(c.X-a.X,c.Y-a.Y);w.Z=c.Z-a.Z;
                float dot=(u.Y*w.Z-u.Z*w.Y)*a.X+(u.Z*w.X-u.X*w.Z)*(a.Y-.018f)+(u.X*w.Y-u.Y*w.X)*a.Z;
                if(dot<0){var tmp=b;b=c;c=tmp;}d.Face(Surface.Glass,a,b,c);
                var q=new[]{a,b,c};for(int n=0;n<3;n++){var x=q[n];var y=q[(n+1)%3];string k=Key(x),l=Key(y),edge=string.CompareOrdinal(k,l)<0?k+"/"+l:l+"/"+k;if(edges.Add(edge))Beam(d,x,y,.010f,Surface.Trim);}
            };
            for(int j=0;j<rings-1;j++)for(int n=0;n<sectors;n++)
            {int k=(n+1)%sectors,upper=j%2==0?n:k;tri(v[j,n],v[j,k],v[j+1,upper]);tri(v[j,k],v[j+1,(upper+1)%sectors],v[j+1,upper]);}
            var peak=new Point(0,.018f+r,0);for(int n=0;n<sectors;n++)tri(v[rings-1,n],v[rings-1,(n+1)%sectors],peak);
            Frustum(d,0,.018f,.43f,.43f,Surface.Trim,sectors);
        }
        private static Point P(float x,float y)=>new Point(x,y,0);
        private static string Key(Point p)=>Math.Round(p.X,5)+","+Math.Round(p.Y,5)+","+Math.Round(p.Z,5);
        private static void Strut(BuildingDesign d,Point a,Point b,float width,float front,float back,Surface s)
        {float dx=b.X-a.X,dy=b.Y-a.Y,len=(float)Math.Sqrt(dx*dx+dy*dy),x=dy/len*width,y=-dx/len*width;Extrude(d,new[]{P(a.X-x,a.Y-y),P(a.X+x,a.Y+y),P(b.X+x,b.Y+y),P(b.X-x,b.Y-y)},front,back,s);}
        private static void Extrude(BuildingDesign d,Point[] p,float front,float back,Surface s)
        {
            var a=p.Select(v=>new Point(v.X,v.Y,front)).ToArray();var b=p.Select(v=>new Point(v.X,v.Y,back)).ToArray();
            // Ear clipping handles concave crown silhouettes without filling their notches.
            var indices=Enumerable.Range(0,p.Length).ToList();
            while(indices.Count>3)
            {
                bool found=false;
                for(int n=0;n<indices.Count;n++)
                {
                    int i=indices[(n+indices.Count-1)%indices.Count],j=indices[n],k=indices[(n+1)%indices.Count];
                    if(Cross(p[i],p[j],p[k])<=1e-8f)continue;
                    if(indices.Any(q=>q!=i&&q!=j&&q!=k&&Cross(p[i],p[j],p[q])>=0&&Cross(p[j],p[k],p[q])>=0&&Cross(p[k],p[i],p[q])>=0))continue;
                    d.Face(s,a[k],a[j],a[i]);d.Face(s,b[i],b[j],b[k]);indices.RemoveAt(n);found=true;break;
                }
                if(!found)throw new InvalidOperationException("Non-simple profile in "+d.Name);
            }
            d.Face(s,a[indices[2]],a[indices[1]],a[indices[0]]);d.Face(s,b[indices[0]],b[indices[1]],b[indices[2]]);
            for(int n=0;n<a.Length;n++){int k=(n+1)%a.Length;d.Face(s,a[n],a[k],b[k],b[n]);}
        }
        private static float Cross(Point a,Point b,Point c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
        private static void Frustum(BuildingDesign d,float bottom,float top,float low,float high,Surface s,int sides)
        {
            var a=new Point[sides];var b=new Point[sides];
            for(int n=0;n<sides;n++){double angle=n*2*Math.PI/sides;a[n]=new Point(low*(float)Math.Cos(angle),bottom,low*(float)Math.Sin(angle));b[n]=new Point(high*(float)Math.Cos(angle),top,high*(float)Math.Sin(angle));}
            for(int n=0;n<sides;n++){int k=(n+1)%sides;d.Face(s,a[n],b[n],b[k],a[k]);}
            d.Face(s,b.Reverse().ToArray());d.Face(s,a);
        }
        private static void Beam(BuildingDesign d,Point a,Point b,float radius,Surface s)
        {
            float dx=b.X-a.X,dy=b.Y-a.Y,dz=b.Z-a.Z,len=(float)Math.Sqrt(dx*dx+dy*dy+dz*dz);dx/=len;dy/=len;dz/=len;
            float ux=-dz,uz=dx;if(Math.Abs(dy)>.95f){ux=1;uz=0;}float ul=(float)Math.Sqrt(ux*ux+uz*uz);ux/=ul;uz/=ul;
            float vx=dy*uz,vy=dz*ux-dx*uz,vz=-dy*ux;
            var lo=new Point[4];var hi=new Point[4];
            for(int n=0;n<4;n++){float c=(float)Math.Cos(n*Math.PI/2+Math.PI/4)*radius,w=(float)Math.Sin(n*Math.PI/2+Math.PI/4)*radius;lo[n]=new Point(a.X+ux*c+vx*w,a.Y+vy*w,a.Z+uz*c+vz*w);hi[n]=new Point(b.X+ux*c+vx*w,b.Y+vy*w,b.Z+uz*c+vz*w);}
            for(int n=0;n<4;n++){int k=(n+1)%4;d.Face(s,lo[n],lo[k],hi[k],hi[n]);}d.Face(s,lo.Reverse().ToArray());d.Face(s,hi);
        }
    }
}
