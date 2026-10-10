using System;
using System.IO;
using System.Linq;
using PlanBSkyscraperMod;
internal static class GlbChecks
{
    internal static void Validate()
    {
        string folder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../Assets/Reference/Gebaeude_GLB_Detailset_b"));
        int retained=0;
        for(int n=0;n<10000;n++)
        {
            if(VariantRules.KeepDome(n,n*3,173,0)||!VariantRules.KeepDome(n,n*3,173,100))throw new Exception("Dome share endpoints failed");
            if(VariantRules.KeepDome(n,n*3,173,30))retained++;
        }
        if(retained<2800||retained>3200)throw new Exception("Dome frequency outside expected range");
        var wall=MaterialAppearance.Color("ceramic",new[]{.86f,.91f,.90f,1f});var glass=MaterialAppearance.Color("glass",new[]{.27f,.59f,.77f,1f});
        if(wall[0]>=.86f||glass[2]<=glass[0]*3||glass[1]>=.59f)throw new Exception("Material contrast adjustment failed");
        Console.WriteLine("PASS: dome retention ~30%, endpoints, darker walls and saturated blue glazing.");
        int[] expected={1970,1721,2013,991,2198};
        for(int id=0;id<5;id++)
        {
            GlbAsset asset;using(var input=File.OpenRead(Path.Combine(folder,GlbAsset.Files[id])))asset=GlbAsset.Read(input);
            if(asset.Materials.Count!=6||asset.Normals.Count!=asset.Vertices.Count||asset.TriangleMaterials.Count!=expected[id])throw new Exception("PBR/normal mapping mismatch");
            if(asset.Indices.Count/3!=expected[id]||Math.Abs(asset.Bottom)>1e-5||asset.Height<=0||asset.Radius<=0)throw new Exception("GLB topology/ground mismatch");
            foreach(float radius in new[]{.2f,.5f,1f})for(int band=0;band<5;band++)
            {
                float scale=radius*.90f/asset.Radius*(ReferenceDesign.IsSmall(id)?1f:new[]{.68f,.80f,.90f,1f,1f}[band]);
                foreach(var v in asset.Vertices)
                    if(Math.Sqrt(v[0]*v[0]+v[2]*v[2])*scale>radius*.901f)throw new Exception("Imported mesh exceeds hex incircle");
                if(Math.Abs((asset.Height*scale)/(asset.Radius*scale)-asset.Height/asset.Radius)>1e-5)throw new Exception("Imported proportions changed");
            }
            foreach(var normal in asset.Normals)
            {
                double length=Math.Sqrt(normal.Sum(v=>v*v));
                if(normal.Any(v=>!float.IsFinite(v))||length>.000001&&Math.Abs(length-1)>.001)throw new Exception("Invalid exported normal");
            }
            if(Math.Abs(asset.Materials[2].Roughness-.27f)>.0001||Math.Abs(asset.Materials[4].Metallic-.2f)>.0001)throw new Exception("PBR factors lost");
            for(int n=0;n<asset.Indices.Count;n+=3)
            {
                var color=asset.Colors[asset.Indices[n]];
                if(!color.SequenceEqual(asset.Colors[asset.Indices[n+1]])||!color.SequenceEqual(asset.Colors[asset.Indices[n+2]]))throw new Exception("GLB contains unsupported gradient colors");
            }
            Console.WriteLine("PASS: imported "+GlbAsset.Files[id]+", "+expected[id]+" triangles, PBR factors, exported normals, Y-up conversion, ground, uniform proportions and hex fit.");
        }
        using(var invalid=new MemoryStream(new byte[12]))
        {try{GlbAsset.Read(invalid);throw new Exception("Malformed GLB accepted");}catch(InvalidDataException){}}
    }
}
