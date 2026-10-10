using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PlanBBahnbetrieb
{
    internal sealed class StationModel : IDisposable
    {
        private GameObject template;
        private Mesh mesh;
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Material> previewMaterials = new List<Material>();

        public void Load(Transform parent)
        {
            if (template != null) return;
            GlbAsset asset;
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("PlanBBahnbetrieb.station.glb"))
            { if (input == null) throw new InvalidOperationException("Bahnhofsmodell fehlt."); asset = GlbAsset.Read(input); }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("URP/Lit-Shader fehlt.");
            foreach (var spec in asset.Materials)
            {
                var material = new Material(shader) { name = "Gueterbahnhof_" + spec.Name, enableInstancing = true };
                material.SetColor("_BaseColor", new Color(spec.Color[0], spec.Color[1], spec.Color[2], spec.Color[3]).gamma);
                material.SetTexture("_BaseMap", Texture2D.whiteTexture);
                material.SetFloat("_Metallic", spec.Metallic); material.SetFloat("_Smoothness", 1 - spec.Roughness);
                material.SetFloat("_Cull", spec.DoubleSided ? 0 : 2); materials.Add(material);
                var preview = new Material(material) { name = material.name + "_Vorschau", renderQueue = 3000 };
                preview.SetFloat("_Surface", 1); preview.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                preview.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); preview.SetFloat("_ZWrite", 0);
                preview.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); previewMaterials.Add(preview);
            }
            mesh = new Mesh { name = "Gueterbahnhof_GLB", indexFormat = IndexFormat.UInt32 };
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            foreach (var p in asset.Vertices) vertices.Add(new Vector3(p[0], p[1] - asset.Bottom, p[2]));
            foreach (var p in asset.Normals) normals.Add(new Vector3(p[0], p[1], p[2]));
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.subMeshCount = materials.Count;
            for (var material = 0; material < materials.Count; material++)
            {
                var triangles = new List<int>();
                for (var t = 0; t < asset.TriangleMaterials.Count; t++)
                    if (asset.TriangleMaterials[t] == material)
                        for (var k = 0; k < 3; k++) triangles.Add(asset.Indices[t * 3 + k]);
                mesh.SetTriangles(triangles, material);
            }
            mesh.RecalculateBounds();
            template = new GameObject("Gueterbahnhof_Vorlage"); template.SetActive(false); template.transform.SetParent(parent, false);
            template.AddComponent<MeshFilter>().sharedMesh = mesh;
            template.AddComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
        }

        public GameObject Spawn(Transform parent, bool preview)
        {
            Load(parent);
            var result = Object.Instantiate(template, parent, false); result.name = preview ? "Gueterbahnhof_Bauvorschau" : "Gueterbahnhof";
            var renderer = result.GetComponent<MeshRenderer>();
            if (preview) { renderer.sharedMaterials = previewMaterials.ToArray(); renderer.shadowCastingMode = ShadowCastingMode.Off; }
            result.SetActive(true); return result;
        }

        public void PreviewColor(bool valid)
        {
            foreach (var material in previewMaterials) material.SetColor("_BaseColor", valid ? new Color(1f, .63f, .12f, .55f) : new Color(1f, .12f, .10f, .55f));
        }

        public static void Position(GameObject model, int2 first, int2 last, int2 track0, int2 track1, bool mirrored)
        {
            var world = SSingleton<SWorld>.Inst;
            var origin = world.GetHexTransform(track0).pos;
            var up = origin.normalized;
            var right = Vector3.ProjectOnPlane(world.GetHexTransform(last).pos - world.GetHexTransform(first).pos, up).normalized;
            var step = Vector3.ProjectOnPlane(world.GetHexTransform(track1).pos - origin, up);
            var forward = Vector3.Cross(right, up).normalized;
            if (Vector3.Dot(step, forward) > 0) { right = -right; forward = -forward; }
            var scale = Mathf.Abs(Vector3.Dot(step, forward)) / 1.58f;
            var rotation = Quaternion.LookRotation(forward, up);
            // GLB is reflected to Unity coordinates: first track Z=-2.05.
            // Sink the foundation to its top edge so it does not cover native rails.
            model.transform.SetPositionAndRotation(origin - rotation * new Vector3(0, .18f * scale, -2.05f * scale), rotation);
            model.transform.localScale = new Vector3(mirrored ? -scale : scale, scale, scale);
        }

        public void Dispose()
        {
            if (template != null) Object.Destroy(template); if (mesh != null) Object.Destroy(mesh);
            foreach (var material in materials) Object.Destroy(material);
            foreach (var material in previewMaterials) Object.Destroy(material);
            materials.Clear(); previewMaterials.Clear();
        }
    }
}
