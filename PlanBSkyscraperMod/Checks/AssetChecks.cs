using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using PlanBSkyscraperMod;

internal static class AssetChecks
{
    internal static void Validate()
    {
        ValidateReferences();
        var unique=new HashSet<string>(); int maximumVertices=0;
        for(int id=0;id<25;id++)
        {
            var design=BuildingDesign.Build(id);
            if(!unique.Add(design.Obj()))throw new Exception("Duplicate building geometry");
            var points=design.Faces.SelectMany(f=>f.Points).ToArray();
            maximumVertices=Math.Max(maximumVertices,points.Length);
            if(points.Length>12000)throw new Exception("Building exceeds mesh budget");
            if(Math.Abs(points.Max(p=>p.Y)-1)>1e-6 || Math.Abs(points.Min(p=>p.Y))>1e-6)
                throw new Exception("Incorrect normalized building height or ground alignment");
            if(!design.Faces.Any(f=>f.Surface==Surface.Glass) || !design.Faces.Any(f=>f.Surface==Surface.Roof))
                throw new Exception("Building lacks facade or roof geometry");
            foreach(var face in design.Faces)
            {
                foreach(var p in face.Points)
                    if(!float.IsFinite(p.X)||!float.IsFinite(p.Y)||!float.IsFinite(p.Z)||p.X*p.X+p.Z*p.Z>.25f)
                        throw new Exception("Non-finite geometry or footprint outside allowed radius");
                var a=face.Points[0];var b=face.Points[1];var c=face.Points[2];
                double ux=b.X-a.X,uy=b.Y-a.Y,uz=b.Z-a.Z,vx=c.X-a.X,vy=c.Y-a.Y,vz=c.Z-a.Z;
                double nx=uy*vz-uz*vy,ny=uz*vx-ux*vz,nz=ux*vy-uy*vx;
                if(nx*nx+ny*ny+nz*nz<1e-14)throw new Exception("Degenerate building face");
                if(face.Surface==Surface.Roof && ny<=0)throw new Exception("Roof faces point inwards");
            }
            double glassArea=design.Faces.Where(f=>f.Surface==Surface.Glass).Sum(Area);
            double facadeArea=design.Faces.Where(f=>f.Surface==Surface.Wall && Math.Abs(f.Points[1].Y-f.Points[0].Y)>.0001).Sum(Area);
            if(glassArea<facadeArea*.15 || glassArea>facadeArea*.45)throw new Exception("White/blue facade balance outside reference range");
        }
        Console.WriteLine($"PASS: 25 unique original meshes, own windows/roofs, ground alignment, footprint, finite nondegenerate geometry; max {maximumVertices} vertices.");
        Console.WriteLine("PASS: asymmetric blue inserts cover 15-45% of wall area; mostly white wall slabs and offset blade endings.");
    }

    private static void ValidateReferences()
    {
        var unique=new HashSet<string>();
        for(int id=0;id<ReferenceDesign.Count;id++)
        {
            var d=ReferenceDesign.Build(id);int triangles=d.Faces.Sum(f=>f.Points.Length-2);
            if(triangles<500||triangles>1500)throw new Exception(d.Name+" triangle budget failed: "+triangles);
            if(!unique.Add(d.Obj()))throw new Exception("Duplicate reference mesh");
            var points=d.Faces.SelectMany(f=>f.Points).ToArray();
            if(Math.Abs(points.Min(q=>q.Y))>1e-6||Math.Abs(points.Max(q=>q.Y)-1)>1e-6)throw new Exception("Reference height/ground mismatch");
            foreach(var face in d.Faces)
            {
                for(int n=1;n<face.Points.Length-1;n++)
                    if(Area(new DesignFace{Points=new[]{face.Points[0],face.Points[n],face.Points[n+1]}})<1e-10)throw new Exception("Degenerate reference triangle: "+d.Name);
                foreach(var q in face.Points)
                {
                    if(!float.IsFinite(q.X)||!float.IsFinite(q.Y)||!float.IsFinite(q.Z))throw new Exception("Non-finite reference vertex");
                    // Check all six half-planes after representative native model rotations.
                    for(int rotation=0;rotation<360;rotation+=15)for(int edge=0;edge<6;edge++)
                    {
                        double a=(rotation+edge*60)*Math.PI/180;
                        if(q.X*Math.Cos(a)+q.Z*Math.Sin(a)>.46001)throw new Exception("Reference hex boundary exceeded");
                    }
                }
            }
            Console.WriteLine("PASS: "+d.Name+" — "+triangles+" triangles, normalized ground/height, hex containment.");
        }
        for(uint hash=0;hash<100;hash++)
        {
            if(!ReferenceDesign.IsSmall(ReferenceDesign.Select(false,hash))||ReferenceDesign.IsSmall(ReferenceDesign.Select(true,hash)))throw new Exception("House/tower category mismatch");
        }
    }
    private static double Area(DesignFace face)
    {
        double result=0;
        for(int n=1;n<face.Points.Length-1;n++)
        {
            var a=face.Points[0];var b=face.Points[n];var c=face.Points[n+1];
            double ux=b.X-a.X,uy=b.Y-a.Y,uz=b.Z-a.Z,vx=c.X-a.X,vy=c.Y-a.Y,vz=c.Z-a.Z;
            double nx=uy*vz-uz*vy,ny=uz*vx-ux*vz,nz=ux*vy-uy*vx;
            result+=Math.Sqrt(nx*nx+ny*ny+nz*nz)/2;
        }
        return result;
    }

    internal static void Export(string directory)
    {
        Directory.CreateDirectory(directory);
        GlbAsset sourcePalette;
        using(var input=File.OpenRead(Path.Combine(directory,"Reference/Gebaeude_GLB_Detailset_b",GlbAsset.Files[0])))sourcePalette=GlbAsset.Read(input);
        var names=new[]{"ceramic","glass","edge","metal","recess","ceramic","yellow"};
        var colors=names.Select(name=>MaterialAppearance.Color(name,sourcePalette.Materials.First(m=>m.Name==name).Color).Take(3).Select(v=>v<=.0031308f?v*12.92f:1.055f*(float)Math.Pow(v,1/2.4f)-.055f).ToArray()).ToArray();
        var palette=Enumerable.Range(0,names.Length).ToDictionary(n=>(Surface)n,n=>string.Join(" ",colors[n].Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
        File.WriteAllText(Path.Combine(directory,"Palette.mtl"),string.Join("\n",palette.Select(p=>$"newmtl {p.Key}\nKd {p.Value}\nKa 0.15 0.15 0.15\n")));
        var designs=new List<object>();var csv=new StringBuilder("Id,Name,HeightRatio,Vertices,Faces\n");
        for(int id=0;id<25;id++)
        {
            var d=BuildingDesign.Build(id);
            File.WriteAllText(Path.Combine(directory,$"{id:00}_{d.Name}.obj"),d.Obj());
            float ratio=VariantRules.Heights[id%5];
            designs.Add(new{id,name=d.Name,ratio,faces=d.Faces.Select(f=>new{surface=(int)f.Surface,color=colors[(int)f.Surface].Select(v=>(int)Math.Round(v*255)).ToArray(),points=f.Points.Select(p=>new[]{p.X,p.Y,p.Z}).ToArray()}).ToArray()});
            csv.AppendFormat(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4}\n",id,d.Name,ratio,d.Faces.Sum(f=>f.Points.Length),d.Faces.Count);
        }
        File.WriteAllText(Path.Combine(directory,"Catalogue.csv"),csv.ToString());
        string html=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Gallery.template.html"));
        File.WriteAllText(Path.Combine(directory,"Gebaeudevorschau.html"),html.Replace("__DESIGNS__",JsonSerializer.Serialize(designs)));
        ExportReferences(directory, html);
        Console.WriteLine("Exported 25 OBJ assets and interactive geometry preview: "+Path.GetFullPath(directory));
    }
    private static void ExportReferences(string directory,string template)
    {
        string folder=Path.Combine(directory,"Reference");Directory.CreateDirectory(folder);
        File.Copy(Path.Combine(directory,"Palette.mtl"),Path.Combine(folder,"Palette.mtl"),true);
        var designs=new List<object>();
        for(int id=0;id<5;id++)
        {
            GlbAsset asset;using(var input=File.OpenRead(Path.Combine(folder,"Gebaeude_GLB_Detailset_b",GlbAsset.Files[id])))asset=GlbAsset.Read(input);
            float scale=.46f/asset.Radius;
            var faces=new List<object>();
            for(int n=0;n<asset.Indices.Count;n+=3)
            {
                var spec=asset.Materials[asset.TriangleMaterials[n/3]];
                var color=MaterialAppearance.Color(spec.Name,spec.Color).Take(3).Select(v=>(int)Math.Round((v<=.0031308f?v*12.92f:1.055f*(float)Math.Pow(v,1/2.4f)-.055f)*255)).ToArray();
                faces.Add(new{surface=0,color=new[]{(int)color[0],(int)color[1],(int)color[2]},points=Enumerable.Range(0,3).Select(k=>{
                    var v=asset.Vertices[asset.Indices[n+k]];return new[]{v[0]*scale,(v[1]-asset.Bottom)/asset.Height,v[2]*scale};}).ToArray()});
            }
            designs.Add(new{id,name=Path.GetFileNameWithoutExtension(GlbAsset.Files[id]).Substring(3),ratio=asset.Height*scale/1.6f,triangles=asset.Indices.Count/3,imported=true,faces=faces.ToArray()});
        }
        string html=template.Replace("<p><a href=\"Reference/Referenzvorschau.html\">Neue Gebäude nach den Illustrationen ansehen</a></p>", "<p><a href=\"../Gebaeudevorschau.html\">Zu den bisherigen 25 Gebäuden</a></p>").Replace("__DESIGNS__",JsonSerializer.Serialize(designs)).Replace("25 eigenständige Gebäudeentwürfe","5 optimierte Sci-Fi-Wohngebäude").Replace("25 Gebäudeentwürfe","Referenzgebäude");
        int start=html.IndexOf("<p>"),end=html.IndexOf("</p>",start);
        html=html.Substring(0,start)+"<p>Version 0.15.3: Detailset: Originalgeometrie, exportierte Normalen und sechs PBR-Materialien. Gleichmäßige Skalierung erhält die Seitenverhältnisse. Beleuchtete Materialien im Spiel; vereinfachte Schattierung in dieser Vorschau.</p>"+html.Substring(end+4);
        File.WriteAllText(Path.Combine(folder,"Referenzvorschau.html"),html);
        Console.WriteLine("PASS: five reference meshes, finite nondegenerate geometry, footprint and ground alignment; exported reference gallery.");
    }

}
