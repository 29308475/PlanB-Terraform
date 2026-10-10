using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PlanBSkyscraperMod
{
    internal sealed class NativePalette
    {
        internal sealed class Sample
        {
            internal Material Material;
            internal Color Color;
            internal Vector4[] Uv;
            internal float Brightness, Vertical;
            internal float BlueScore;
        }
        internal Sample[] Samples;

        internal static NativePalette Read(IEnumerable<GameObject> sources, Action<string> info)
        {
            var samples=new List<Sample>();
            var textures=new Dictionary<Texture,Texture2D>();
            try
            {
                foreach(var source in sources)
                foreach(var filter in source.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer=filter.GetComponent<MeshRenderer>();
                    if(renderer==null || filter.sharedMesh==null)continue;
                    var mesh=ReadableMesh.Copy(filter.sharedMesh);
                    try
                    {
                        var vertices=mesh.vertices;var colors=mesh.colors;
                        var uv=new List<Vector4>[8];
                        for(int c=0;c<8;c++){uv[c]=new List<Vector4>();mesh.GetUVs(c,uv[c]);}
                        var matrix=VariantFactory.ToRoot(source,filter.transform);
                        for(int sub=0;sub<mesh.subMeshCount && sub<renderer.sharedMaterials.Length;sub++)
                        {
                            var material=renderer.sharedMaterials[sub];if(material==null)continue;
                            string property=material.HasProperty("_BaseMap")?"_BaseMap":material.HasProperty("_MainTex")?"_MainTex":null;
                            if(property==null)
                            {
                                // Shader Graph shaders often use a generated property name.
                                var shader=material.shader;
                                for(int p=0;p<shader.GetPropertyCount();p++)
                                {
                                    if(shader.GetPropertyType(p)!=UnityEngine.Rendering.ShaderPropertyType.Texture)continue;
                                    string candidate=shader.GetPropertyName(p);
                                    string lower=candidate.ToLowerInvariant();
                                    if(lower.Contains("normal")||lower.Contains("mask")||lower.Contains("metal"))continue;
                                    if(material.GetTexture(candidate) is Texture2D){property=candidate;break;}
                                }
                            }
                            Texture texture=property!=null?material.GetTexture(property):null;
                            Texture2D readable=null;
                            if(texture!=null && !textures.TryGetValue(texture,out readable))
                            {readable=CopyTexture(texture);textures[texture]=readable;}
                            int[] indices=mesh.GetIndices(sub);
                            for(int t=0;t+2<indices.Length;t+=3)
                            {
                                int a=indices[t],b=indices[t+1],c=indices[t+2];
                                Vector3 va=matrix.MultiplyPoint3x4(vertices[a]),vb=matrix.MultiplyPoint3x4(vertices[b]),vc=matrix.MultiplyPoint3x4(vertices[c]);
                                Vector3 normal=Vector3.Cross(vb-va,vc-va).normalized;
                                var coord=new Vector4[8];
                                for(int channel=0;channel<8;channel++)
                                    if(uv[channel].Count==vertices.Length)coord[channel]=(uv[channel][a]+uv[channel][b]+uv[channel][c])/3;
                                Color color=colors.Length==vertices.Length?(colors[a]+colors[b]+colors[c])/3:Color.white;
                                Color appearance=color;
                                if(readable!=null)
                                {
                                    Vector2 scale=material.GetTextureScale(property),offset=material.GetTextureOffset(property);
                                    appearance*=readable.GetPixelBilinear(coord[0].x*scale.x+offset.x,coord[0].y*scale.y+offset.y);
                                }
                                samples.Add(new Sample{Material=material,Color=color,Uv=coord,
                                    Brightness=.2126f*appearance.r+.7152f*appearance.g+.0722f*appearance.b,
                                    BlueScore=appearance.b-(appearance.r+appearance.g)*.5f,Vertical=Mathf.Abs(normal.y)});
                            }
                        }
                    }
                    finally{Object.Destroy(mesh);}
                }
                var vertical=samples.Where(s=>s.Vertical<.45f && s.Brightness>.04f).OrderBy(s=>s.Brightness).ToArray();
                if(vertical.Length==0)vertical=samples.OrderBy(s=>s.Brightness).ToArray();
                if(vertical.Length==0)throw new InvalidOperationException("No usable native building materials");
                var wall=vertical[(int)((vertical.Length-1)*.80f)];
                var blue=vertical.OrderByDescending(s=>s.BlueScore).First();
                var glass=blue.BlueScore>.04f?blue:vertical[(int)((vertical.Length-1)*.10f)];
                var trim=wall;
                var reveal=vertical[0];
                var roofs=samples.Where(s=>s.Vertical>.8f).OrderBy(s=>s.Brightness).ToArray();
                var roof=roofs.Length>0?roofs[roofs.Length/2]:trim;
                if(blue.BlueScore<=.04f && glass.Brightness>wall.Brightness*.8f)
                    glass=new Sample{Material=glass.Material,Uv=glass.Uv,Color=new Color(glass.Color.r*.5f,glass.Color.g*.5f,glass.Color.b*.5f,glass.Color.a),Brightness=glass.Brightness*.5f};
                info("Native palette: " + string.Join(", ",samples.Select(s=>s.Material.shader.name).Distinct().ToArray()) + "; facade and roof atlas samples reused on own geometry.");
                return new NativePalette{Samples=new[]{wall,glass,trim,roof,reveal,
                    SolidAccent(wall,new Color(.84f,.88f,.51f),"Lime"),
                    SolidAccent(wall,new Color(.98f,.88f,.36f),"Gold")}};
            }
            finally{foreach(var texture in textures.Values)if(texture!=null)Object.Destroy(texture);}
        }
        internal static NativePalette DetailSet(Action<string> info)
        {
            GlbAsset source;
            using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("PlanBSkyscraperMod.Glb."+GlbAsset.Files[0]))
            {if(stream==null)throw new InvalidOperationException("Detail palette resource missing");source=GlbAsset.Read(stream);}
            // Identical named PBR sources for legacy geometry and imported GLBs.
            var names=new[]{"ceramic","glass","edge","metal","recess","ceramic","yellow"};
            var samples=new Sample[names.Length];
            for(int n=0;n<samples.Length;n++)
            {
                var spec=source.Materials.First(m=>m.Name==names[n]);
                samples[n]=new Sample{Material=GlbFactory.CreateMaterial(spec),Color=Color.white,Uv=new Vector4[8],Brightness=1};
            }
            info("All 25 procedural designs use the Detailset_b PBR palette.");return new NativePalette{Samples=samples};
        }
        internal static NativePalette Flat(Action<string> info)
        {
            var shader=Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if(shader==null)throw new InvalidOperationException("No flat unlit shader available; custom geometry cannot use the requested palette.");
            var colors=new[]{new Color(.94f,.96f,.98f),new Color(.14f,.64f,.91f),new Color(.99f,.99f,1),new Color(.77f,.82f,.87f),new Color(.31f,.35f,.41f),new Color(.77f,.87f,.54f),new Color(1,.87f,.29f)};
            var samples=new Sample[colors.Length];
            for(int n=0;n<samples.Length;n++)
            {
                var material=new Material(shader){name="PBS_Flat_"+((Surface)n)};
                if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",colors[n]);
                if(material.HasProperty("_Color"))material.SetColor("_Color",colors[n]);
                if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",Texture2D.whiteTexture);
                if(material.HasProperty("_MainTex"))material.SetTexture("_MainTex",Texture2D.whiteTexture);
                samples[n]=new Sample{Material=material,Color=Color.white,Uv=new Vector4[8],Brightness=1};
            }
            info("Five reference designs use flat unlit pastel materials: "+shader.name);
            return new NativePalette{Samples=samples};
        }
        private static Sample SolidAccent(Sample fallback, Color color, string name)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
            if(shader==null)
                return new Sample{Material=fallback.Material,Color=fallback.Color*color,Uv=fallback.Uv,Brightness=fallback.Brightness};
            var material=new Material(shader){name="PBS_Reference_"+name};
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",Texture2D.whiteTexture);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.15f);
            return new Sample{Material=material,Color=Color.white,Uv=new Vector4[8],Brightness=1};
        }

        private static Texture2D CopyTexture(Texture source)
        {
            var previous=RenderTexture.active;
            var target=RenderTexture.GetTemporary(256,256,0,RenderTextureFormat.ARGB32);
            Texture2D result=null;
            try
            {
                Graphics.Blit(source,target);RenderTexture.active=target;
                result=new Texture2D(256,256,TextureFormat.RGBA32,false);
                result.ReadPixels(new Rect(0,0,256,256),0,0);result.Apply(false,false);
                return result;
            }
            catch {if(result!=null)Object.Destroy(result);return null;}
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
        }
    }
}
