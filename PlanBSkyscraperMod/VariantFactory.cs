using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PlanBSkyscraperMod
{
    internal sealed class VariantFactory
    {
        private readonly Transform parent;
        private readonly Action<string> info, warning;
        private readonly Dictionary<string, CAssetModel[]> banks = new Dictionary<string, CAssetModel[]>();
        private readonly Dictionary<CAssetModels, CAssetModel[]> modelBanks = new Dictionary<CAssetModels, CAssetModel[]>();
        private readonly Dictionary<CAssetModels,float> bankRadii = new Dictionary<CAssetModels,float>();
        private readonly HashSet<string> failed = new HashSet<string>();
        internal VariantFactory(Transform parent, Action<string> info, Action<string> warning)
        { this.parent = parent; this.info = info; this.warning = warning; }
        internal static bool IsResidential(string name)
            => (name.StartsWith("C3_") || name.StartsWith("C32_") || name.StartsWith("C33_"))
               && (name.Contains("_House") || name.Contains("_Tower")) && !name.Contains("Dead") && !name.EndsWith("_7hexa");
        internal CAssetModel Get(CAssetModels models, int variant, float tileInradius)
        {
            tileInradius = FootprintCache.Radius(tileInradius);
            if (modelBanks.TryGetValue(models, out var cached) && bankRadii.TryGetValue(models,out var radius) && Mathf.Abs(radius-tileInradius)<.00001f) return cached?[variant];
            bankRadii[models]=tileInradius;
            var towers = models.models.Where(m => m.Asset != null && IsResidential(m.Asset.name)
                && m.Asset.name.Contains("_Tower")).OrderBy(m => m.Asset.name, StringComparer.Ordinal).ToArray();
            if (towers.Length == 0) { modelBanks[models] = null; return null; }
            string key = string.Join("|", towers.Select(m => m.Asset.GetInstanceID().ToString()).ToArray()) + "|" + tileInradius.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
            if (failed.Contains(key)) { modelBanks[models] = null; return null; }
            if (!banks.TryGetValue(key, out var bank))
            {
                var objects = new List<GameObject>(); var meshes = new List<Mesh>();
                try
                {
                    var bounds = towers.Select(m => BoundsOf(m.Asset)).ToArray();
                    float referenceHeight = bounds.Max(b => b.size.y);
                    float footprint = bounds.Min(b => Mathf.Min(b.size.x, b.size.z)) * .95f;
                    // Native tower bounds can contain underground geometry. The city spawn
                    // transform is the surface anchor, not the bottom of those native meshes.
                    // Our normalized designs have no underground foundations: put Y=0 here.
                    float nativeBottom = bounds.Min(b => b.min.y);
                    float groundY = 0f;
                    if (referenceHeight <= .001f || footprint <= .001f) throw new InvalidOperationException("Invalid native tower bounds");
                    // Native meshes supply dimensions and palette only, never building vertices.
                    var palette = NativePalette.DetailSet(info);
                    bank = new CAssetModel[VariantRules.Count];
                    for (int i = 0; i < bank.Length; i++)
                    {
                        int referenceId = (i - VariantRules.Count) / 5;
                        var design = i < VariantRules.Count ? BuildingDesign.Build(i) : ReferenceDesign.Build(referenceId);
                        float ratio = i < VariantRules.Count || !ReferenceDesign.IsSmall(referenceId) ? VariantRules.Heights[i % 5] : ReferenceDesign.PreviewHeights[referenceId];
                        float height = referenceHeight * ratio;
                        var mesh = BuildMesh(design, footprint, height, groundY, palette, out var materials);
                        meshes.Add(mesh);
                        FitToTile(mesh, tileInradius);
                        if (Mathf.Abs(mesh.bounds.min.y) > .0001f)
                            throw new InvalidOperationException("Surface alignment failed for " + design.Name);
                        if ((i < VariantRules.Count || !ReferenceDesign.IsSmall(referenceId)) && mesh.bounds.size.y < referenceHeight * .5f || Mathf.Abs(mesh.bounds.size.y - height) > .002f)
                            throw new InvalidOperationException("Height validation failed for " + design.Name);
                        string style = towers[0].Asset.name.Split('_')[0];
                        bool angled = towers[0].Asset.name.Contains("Angle120");
                        var go = new GameObject(style + "_PBS_" + design.Name + (angled ? "_Angle120" : ""));
                        objects.Add(go); go.SetActive(false); go.transform.SetParent(parent, false);
                        go.layer = towers[0].Asset.layer;
                        go.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
                        var nativeRenderer = towers[0].Asset.GetComponentInChildren<MeshRenderer>(true);
                        if (nativeRenderer != null)
                        {
                            renderer.shadowCastingMode = nativeRenderer.shadowCastingMode; renderer.receiveShadows = nativeRenderer.receiveShadows;
                            renderer.lightProbeUsage = nativeRenderer.lightProbeUsage; renderer.reflectionProbeUsage = nativeRenderer.reflectionProbeUsage;
                        }
                        bank[i] = new CAssetModel(go);
                    }
                    banks.Add(key, bank);
                    info($"Generated building surface anchor: local Y=0; native tower mesh minimum={nativeBottom:F4}; small model heights hall={referenceHeight * .40f:F4}, dome={referenceHeight * .26f:F4}.");
                    info($"Created 25 updated building designs with Detailset_b PBR materials for {towers[0].Asset.name}: height {referenceHeight * .55f:F3}-{referenceHeight * VariantRules.Heights[4]:F3}, own bodies/windows/roofs, no cloned base.");
                }
                catch (Exception ex)
                {
                    foreach (var go in objects) Object.Destroy(go); foreach (var mesh in meshes) Object.Destroy(mesh);
                    failed.Add(key); modelBanks[models] = null;
                    warning("Building generation failed; native buildings remain active. " + ex); return null;
                }
            }
            modelBanks[models] = bank; return bank[variant];
        }
        internal static void FitToTile(Mesh mesh,float inradius)
        {
            if(float.IsNaN(inradius)||float.IsInfinity(inradius)||inradius<=.001f)throw new InvalidOperationException("Invalid native tile radius");
            // The inscribed circle is inside all six tile edges at every native rotation.
            float limit=inradius*.92f;var vertices=mesh.vertices;float max=vertices.Max(v=>new Vector2(v.x,v.z).magnitude);
            float scale=max>limit?limit/max:1;
            for(int n=0;n<vertices.Length;n++)
            {
                var v=vertices[n];if(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z))throw new InvalidOperationException("Non-finite mesh vertex");
                v.x*=scale;v.z*=scale;vertices[n]=v;
                if(new Vector2(v.x,v.z).magnitude>limit+.00001f)throw new InvalidOperationException("Mesh exceeds tile boundary");
            }
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
        }
        internal static Matrix4x4 ToRoot(GameObject root, Transform child)
            => Matrix4x4.Scale(root.transform.localScale) * root.transform.worldToLocalMatrix * child.localToWorldMatrix;
        internal static Bounds BoundsOf(GameObject go)
        {
            var result = new Bounds(); bool initialized = false;
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var bounds = filter.sharedMesh.bounds; var matrix = ToRoot(go, filter.transform);
                for (int corner = 0; corner < 8; corner++)
                {
                    var p = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    p = matrix.MultiplyPoint3x4(p);
                    if (!initialized) { result = new Bounds(p, Vector3.zero); initialized = true; } else result.Encapsulate(p);
                }
            }
            return result;
        }
        internal static Mesh BuildMesh(BuildingDesign design, float footprint, float height, float groundY,
            NativePalette palette, out Material[] materials)
        {
            var positions = new List<Vector3>(); var colors = new List<Color>();
            var uvs = new List<Vector4>[8]; for(int c=0;c<8;c++)uvs[c]=new List<Vector4>();
            var slots = new List<Material>(); var indices = new List<List<int>>();
            foreach (var face in design.Faces)
            {
                var sample = palette.Samples[(int)face.Surface];
                int slot = slots.IndexOf(sample.Material);
                if(slot<0){slot=slots.Count;slots.Add(sample.Material);indices.Add(new List<int>());}
                int offset=positions.Count;
                foreach(var p in face.Points)
                {
                    positions.Add(new Vector3(p.X*footprint,groundY+p.Y*height,p.Z*footprint)); colors.Add(sample.Color);
                    for(int channel=0;channel<8;channel++)uvs[channel].Add(sample.Uv[channel]);
                }
                for(int n=1;n<face.Points.Length-1;n++)
                {indices[slot].Add(offset);indices[slot].Add(offset+n);indices[slot].Add(offset+n+1);}
            }
            var mesh = new Mesh { name = "PBS_" + design.Name };
            try
            {
                if(positions.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(positions);mesh.SetColors(colors);for(int c=0;c<8;c++)mesh.SetUVs(c,uvs[c]);
                mesh.subMeshCount=slots.Count;for(int n=0;n<indices.Count;n++)mesh.SetTriangles(indices[n],n,false);
                mesh.RecalculateNormals();mesh.RecalculateBounds();
                var tangents = new Vector4[positions.Count];for(int n=0;n<tangents.Length;n++)tangents[n]=new Vector4(1,0,0,1);
                mesh.tangents=tangents;materials=slots.ToArray();return mesh;
            }
            catch { Object.Destroy(mesh); throw; }
        }
    }
}
