using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace PlanBSkyscraperMod
{
    // Deliberately bounded reader for the supplied static Z-up GLBs (no animations/Draco).
    internal sealed class GlbAsset
    {
        internal static readonly string[] Files={"01_stepped_tower.glb","02_three_module_complex.glb","03_buttressed_reactor.glb","04_antenna_spire.glb","05_geodesic_dome.glb"};
        internal readonly List<float[]> Vertices=new List<float[]>();
        internal readonly List<byte[]> Colors=new List<byte[]>();
        internal readonly List<int> Indices=new List<int>();
        internal readonly List<float[]> Normals=new List<float[]>();
        internal readonly List<int> TriangleMaterials=new List<int>();
        internal sealed class PbrMaterial
        {internal string Name;internal float[] Color;internal float Metallic,Roughness;internal bool DoubleSided;}
        internal readonly List<PbrMaterial> Materials=new List<PbrMaterial>();
        internal static GlbAsset Read(Stream input)
        {
            using(var reader=new BinaryReader(input,Encoding.UTF8,true))
            {
                if(reader.ReadUInt32()!=0x46546C67||reader.ReadUInt32()!=2)throw new InvalidDataException("Expected GLB 2");
                uint length=reader.ReadUInt32();if(length!=input.Length||length>10000000)throw new InvalidDataException("Invalid GLB length");
                int jsonLength=checked((int)reader.ReadUInt32());if(reader.ReadUInt32()!=0x4E4F534A)throw new InvalidDataException("Missing JSON chunk");
                var json=JObject.Parse(Encoding.UTF8.GetString(reader.ReadBytes(jsonLength)));
                int binaryLength=checked((int)reader.ReadUInt32());if(reader.ReadUInt32()!=0x004E4942)throw new InvalidDataException("Missing binary chunk");
                byte[] data=reader.ReadBytes(binaryLength);if(data.Length!=binaryLength)throw new InvalidDataException("Truncated GLB");
                if(json["extensionsRequired"]!=null&&json["extensionsRequired"].Any())throw new InvalidDataException("Unsupported required extension");
                foreach(var node in json["nodes"])
                    if(node["matrix"]!=null||node["translation"]!=null||node["rotation"]!=null||node["scale"]!=null)throw new InvalidDataException("Expected baked identity nodes");
                var result=new GlbAsset();
                foreach(var material in json["materials"])
                {
                    var pbr=material["pbrMetallicRoughness"];
                    if(pbr["baseColorTexture"]!=null||material["normalTexture"]!=null||material["alphaMode"]!=null&&(string)material["alphaMode"]!="OPAQUE")throw new InvalidDataException("Unsupported textured/transparent material");
                    result.Materials.Add(new PbrMaterial{Name=(string)material["name"],Color=pbr["baseColorFactor"].Select(v=>(float)v).ToArray(),Metallic=(float)pbr["metallicFactor"],Roughness=(float)pbr["roughnessFactor"],DoubleSided=(bool?)material["doubleSided"]??false});
                }
                foreach(var mesh in json["meshes"])foreach(var primitive in mesh["primitives"])
                {
                    if((int?)primitive["mode"]!=null&&(int)primitive["mode"]!=4)throw new InvalidDataException("Expected triangles");
                    var positions=json["accessors"][(int)primitive["attributes"]["POSITION"]];
                    var normals=json["accessors"][(int)primitive["attributes"]["NORMAL"]];
                    int materialId=(int)primitive["material"];var materialColor=result.Materials[materialId].Color;
                    var indices=json["accessors"][(int)primitive["indices"]];
                    if((int)positions["componentType"]!=5126||(string)positions["type"]!="VEC3"||(int)normals["componentType"]!=5126||(string)normals["type"]!="VEC3")throw new InvalidDataException("Unsupported vertex format");
                    int count=(int)positions["count"],offset=result.Vertices.Count;if((int)normals["count"]!=count)throw new InvalidDataException("Color count mismatch");
                    for(int n=0;n<count;n++)
                    {
                        int a=Address(json,positions,n,12,data.Length),c=Address(json,normals,n,12,data.Length);
                        // glTF Y-up right-handed to Unity Y-up left-handed: reflect Z and reverse winding.
                        float x=BitConverter.ToSingle(data,a),y=BitConverter.ToSingle(data,a+4),z=-BitConverter.ToSingle(data,a+8);
                        if(float.IsNaN(x)||float.IsNaN(y)||float.IsNaN(z)||float.IsInfinity(x)||float.IsInfinity(y)||float.IsInfinity(z))throw new InvalidDataException("Non-finite vertex");
                        result.Vertices.Add(new[]{x,y,z});
                        result.Normals.Add(new[]{BitConverter.ToSingle(data,c),BitConverter.ToSingle(data,c+4),-BitConverter.ToSingle(data,c+8)});
                        result.Colors.Add(materialColor.Select(v=>(byte)Math.Round(v*255)).ToArray());
                    }
                    int component=(int)indices["componentType"],bytes=component==5125?4:component==5123?2:component==5121?1:0;
                    if(bytes==0||(string)indices["type"]!="SCALAR"||(int)indices["count"]%3!=0)throw new InvalidDataException("Invalid indices");
                    for(int n=0;n<(int)indices["count"];n++)
                    {
                        int a=Address(json,indices,n,bytes,data.Length);uint i=bytes==4?BitConverter.ToUInt32(data,a):bytes==2?BitConverter.ToUInt16(data,a):data[a];
                        if(i>=count)throw new InvalidDataException("Index out of bounds");result.Indices.Add(offset+(int)i);
                        if(n%3==2){int last=result.Indices.Count-1;int tmp=result.Indices[last];result.Indices[last]=result.Indices[last-1];result.Indices[last-1]=tmp;result.TriangleMaterials.Add(materialId);}
                    }
                }
                if(result.Vertices.Count==0||result.Indices.Count==0)throw new InvalidDataException("Empty GLB");
                return result;
            }
        }
        private static int Address(JObject json,JToken accessor,int n,int size,int length)
        {
            if(accessor["sparse"]!=null)throw new InvalidDataException("Sparse accessors unsupported");
            var view=json["bufferViews"][(int)accessor["bufferView"]];if((int)view["buffer"]!=0)throw new InvalidDataException("External buffers unsupported");
            int stride=(int?)view["byteStride"]??size;if(stride<size)throw new InvalidDataException("Invalid stride");
            int relative=checked(((int?)accessor["byteOffset"]??0)+n*stride);
            int address=checked(((int?)view["byteOffset"]??0)+relative);
            if(relative<0||relative+size>(int)view["byteLength"]||address<0||address+size>length)throw new InvalidDataException("Accessor outside buffer");return address;
        }
        internal float Radius=>Vertices.Max(p=>(float)Math.Sqrt(p[0]*p[0]+p[2]*p[2]));
        internal float Bottom=>Vertices.Min(p=>p[1]);
        internal float Height=>Vertices.Max(p=>p[1])-Bottom;
    }
}
