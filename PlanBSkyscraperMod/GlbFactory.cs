using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Object=UnityEngine.Object;

namespace PlanBSkyscraperMod
{
    internal sealed class GlbFactory
    {
        private readonly Transform parent;private readonly Action<string> info,warning;
        private readonly Dictionary<string,CAssetModel> cache=new Dictionary<string,CAssetModel>();
        private readonly Dictionary<int,GlbAsset> source=new Dictionary<int,GlbAsset>();
        private readonly Dictionary<string,Material> palette=new Dictionary<string,Material>();
        internal GlbFactory(Transform parent,Action<string> info,Action<string> warning){this.parent=parent;this.info=info;this.warning=warning;}
        internal static Material CreateMaterial(GlbAsset.PbrMaterial spec)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null||!shader.isSupported)throw new InvalidOperationException("Supported native URP/Lit shader unavailable");
            var material=new Material(shader){name="PBS_Detail_"+spec.Name,enableInstancing=true};
            var color=MaterialAppearance.Color(spec.Name,spec.Color);
            var linear=new Color(color[0],color[1],color[2],color[3]);material.SetColor("_BaseColor",linear.gamma);
            material.SetTexture("_BaseMap",Texture2D.whiteTexture);material.SetFloat("_Metallic",spec.Metallic);material.SetFloat("_Smoothness",1-spec.Roughness);
            material.SetFloat("_Cull",spec.DoubleSided?0:2);return material;
        }
        internal CAssetModel Get(int id,int band,float inradius,int layer)
        {
            inradius=FootprintCache.Radius(inradius);
            string key=id+":"+band+":"+inradius.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+":"+layer;
            if(cache.TryGetValue(key,out var existing))return existing;
            GameObject go=null;Mesh mesh=null;
            try
            {
                if(!source.TryGetValue(id,out var asset))
                {
                    using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("PlanBSkyscraperMod.Glb."+GlbAsset.Files[id]))
                    {if(stream==null)throw new FileNotFoundException(GlbAsset.Files[id]);asset=GlbAsset.Read(stream);}source.Add(id,asset);
                }
                // A footprint-based UNIFORM scale retains the shape on every axis.
                float factor=ReferenceDesign.IsSmall(id)?1f:new[]{.68f,.80f,.90f,1f,1f}[band];
                float scale=inradius*.90f/asset.Radius*factor;
                var positions=new List<Vector3>();var normals=new List<Vector3>();var materials=new List<Material>();var triangles=new List<List<int>>();
                for(int n=0;n<asset.Vertices.Count;n++)
                {
                    var p=asset.Vertices[n];var v=new Vector3(p[0]*scale,(p[1]-asset.Bottom)*scale,p[2]*scale);
                    if(new Vector2(v.x,v.z).magnitude>inradius*.901f)throw new InvalidOperationException("GLB exceeds tile footprint");
                    positions.Add(v);var normal=asset.Normals[n];normals.Add(new Vector3(normal[0],normal[1],normal[2]));
                }
                for(int n=0;n<asset.Indices.Count;n+=3)
                {
                    var spec=asset.Materials[asset.TriangleMaterials[n/3]];
                    string materialKey=spec.Name+":"+string.Join(",",Array.ConvertAll(spec.Color,c=>c.ToString("R",System.Globalization.CultureInfo.InvariantCulture)))+":"+spec.Metallic+":"+spec.Roughness+":"+spec.DoubleSided;
                    if(!palette.TryGetValue(materialKey,out var material))
                    {
                        material=CreateMaterial(spec);palette.Add(materialKey,material);
                    }
                    int slot=materials.IndexOf(material);if(slot<0){slot=materials.Count;materials.Add(material);triangles.Add(new List<int>());}
                    for(int k=0;k<3;k++)triangles[slot].Add(asset.Indices[n+k]);
                }
                int repaired=0;
                var accumulated=new Vector3[normals.Count];
                for(int n=0;n<asset.Indices.Count;n+=3)
                {
                    int a=asset.Indices[n],b=asset.Indices[n+1],c=asset.Indices[n+2];
                    var face=Vector3.Cross(positions[b]-positions[a],positions[c]-positions[a]);
                    accumulated[a]+=face;accumulated[b]+=face;accumulated[c]+=face;
                }
                for(int n=0;n<normals.Count;n++)if(normals[n].sqrMagnitude<.000001f)
                {normals[n]=accumulated[n].sqrMagnitude>.0000000001f?accumulated[n].normalized:Vector3.up;repaired++;}
                if(repaired>0)info("GLB missing normals repaired: "+GlbAsset.Files[id]+", count="+repaired);
                mesh=new Mesh{name="PBS_GLB_"+id};if(positions.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(positions);mesh.SetNormals(normals);mesh.subMeshCount=materials.Count;for(int s=0;s<materials.Count;s++)mesh.SetTriangles(triangles[s],s,false);
                mesh.RecalculateBounds();
                if(Mathf.Abs(mesh.bounds.min.y)>.0001f)throw new InvalidOperationException("Invalid GLB ground alignment");
                go=new GameObject("C3_PBS_GLB_"+GlbAsset.Files[id]);go.SetActive(false);go.transform.SetParent(parent,false);go.layer=layer;
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=materials.ToArray();
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;renderer.receiveShadows=true;
                var model=new CAssetModel(go);cache.Add(key,model);
                info("GLB loaded: "+GlbAsset.Files[id]+", triangles="+asset.Indices.Count/3+", uniformScale="+scale.ToString("F4")+", height="+mesh.bounds.size.y.ToString("F4"));return model;
            }
            catch(Exception ex){if(go!=null)Object.Destroy(go);if(mesh!=null)Object.Destroy(mesh);cache[key]=null;warning("GLB loading failed; retaining native model: "+ex);return null;}
        }
    }
}
