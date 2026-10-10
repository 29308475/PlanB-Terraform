using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Roadlife
{
    // Original OBJ module embedded in the DLL. Bake its instances into the road mesh.
    internal static class BridgeAsset
    {
        private static readonly Vector3[] vertices;
        private static readonly int[] triangles;
        static BridgeAsset()
        {
            var points = new List<Vector3>(); var indices = new List<int>();
            using (var stream = typeof(BridgeAsset).Assembly.GetManifestResourceStream("Roadlife.Assets.BridgeModule.obj"))
            using (var reader = new StreamReader(stream ?? throw new InvalidOperationException("BridgeModule.obj fehlt.")))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var fields = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (fields.Length == 4 && fields[0] == "v")
                        points.Add(new Vector3(float.Parse(fields[1], CultureInfo.InvariantCulture),
                            float.Parse(fields[2], CultureInfo.InvariantCulture), float.Parse(fields[3], CultureInfo.InvariantCulture)));
                    if (fields.Length == 4 && fields[0] == "f")
                        for (int i = 1; i < 4; i++) indices.Add(int.Parse(fields[i], CultureInfo.InvariantCulture) - 1);
                }
            }
            vertices = points.ToArray(); triangles = indices.ToArray();
        }

        internal static void Beam(Vector3 a, Vector3 b, Vector3 up, float width, float thickness,
            Action<Vector3, Vector3, Vector3> emit)
        {
            if (Vector3.Distance(a, b) < .00001f) return;
            Vector3 forward = b - a;
            Vector3 side = Vector3.Cross(up, forward).normalized * width;
            Vector3 vertical = Vector3.Cross(forward.normalized, side.normalized).normalized * thickness;
            Vector3 center = (a + b) / 2f;
            for (int i = 0; i < triangles.Length; i += 3)
                emit(Transform(vertices[triangles[i]], center, side, vertical, forward),
                    Transform(vertices[triangles[i + 1]], center, side, vertical, forward),
                    Transform(vertices[triangles[i + 2]], center, side, vertical, forward));
        }
        private static Vector3 Transform(Vector3 v, Vector3 center, Vector3 side, Vector3 up, Vector3 forward)
            => center + side * v.x + up * v.y + forward * v.z;
    }
}
