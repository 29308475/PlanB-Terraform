using System.Collections.Generic;
using UnityEngine;

namespace Roadlife
{
    // Index each native terrain block once. Road generation only visits nearby triangles.
    internal static class NativeRoadTerrain
    {
        private sealed class Index
        {
            internal CBlock block;
            internal float updated;
            internal readonly List<Vector3> triangles = new List<Vector3>();
            internal readonly Dictionary<Vector3Int, List<int>> cells = new Dictionary<Vector3Int, List<int>>();
        }
        private static readonly BoundedCache<CViewBlock, Index> cache = new BoundedCache<CViewBlock, Index>(16);
        internal static void Clear() => cache.Clear();
        internal static bool Available(int2 coords)
        {
            var block = SSingleton<SBlocks>.Inst.GetBlock(coords);
            return block?.lastViewBlock != null && block.lastViewBlock.block == block
                && block.lastViewBlock.terrainMesh.nbTriangles > 0;
        }
        private static Vector3 Flat(Vector3 p) => p.normalized * GWorld.worldRadius;
        private static Vector3Int Cell(Vector3 p) => new Vector3Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));

        internal static void Apply(int2 coords, RoadSurface surface)
        {
            var block = SSingleton<SBlocks>.Inst.GetBlock(coords);
            var view = block?.lastViewBlock;
            if (view == null || view.block != block || view.terrainMesh.nbTriangles == 0) return;
            if (!cache.TryGet(view, out var index) || index.block != block || index.updated != block.lastMeshUpdate)
            {
                index = new Index { block = block, updated = block.lastMeshUpdate };
                var mesh = view.terrainMesh;
                for (int i = 0; i + 2 < mesh.nbTriangles; i += 3)
                {
                    Vector3 a = view.rootGO.transform.TransformPoint(mesh.verticesArray[mesh.trianglesArray[i]]);
                    Vector3 b = view.rootGO.transform.TransformPoint(mesh.verticesArray[mesh.trianglesArray[i + 1]]);
                    Vector3 c = view.rootGO.transform.TransformPoint(mesh.verticesArray[mesh.trianglesArray[i + 2]]);
                    int id = index.triangles.Count;
                    index.triangles.Add(a); index.triangles.Add(b); index.triangles.Add(c);
                    Vector3 fa = Flat(a), fb = Flat(b), fc = Flat(c);
                    Vector3Int min = Cell(Vector3.Min(fa, Vector3.Min(fb, fc)));
                    Vector3Int max = Cell(Vector3.Max(fa, Vector3.Max(fb, fc)));
                    for (int x = min.x; x <= max.x; x++)
                    for (int y = min.y; y <= max.y; y++)
                    for (int z = min.z; z <= max.z; z++)
                    {
                        var key = new Vector3Int(x, y, z);
                        if (!index.cells.TryGetValue(key, out var ids)) index.cells[key] = ids = new List<int>();
                        ids.Add(id);
                    }
                }
                cache.Put(view, index);
            }
            Vector3 low = Flat(surface.center), high = low;
            foreach (var corner in surface.corners)
            { Vector3 p = Flat(corner); low = Vector3.Min(low, p); high = Vector3.Max(high, p); }
            // Small tolerance covers radial projection and block seam rounding.
            var from = Cell(low - Vector3.one * .01f);
            var to = Cell(high + Vector3.one * .01f);
            var visited = new HashSet<int>();
            var nearby = new List<Vector3>();
            for (int x = from.x; x <= to.x; x++)
            for (int y = from.y; y <= to.y; y++)
            for (int z = from.z; z <= to.z; z++)
                if (index.cells.TryGetValue(new Vector3Int(x, y, z), out var ids))
                    foreach (int id in ids)
                        if (visited.Add(id))
                        { nearby.Add(index.triangles[id]); nearby.Add(index.triangles[id + 1]); nearby.Add(index.triangles[id + 2]); }
            surface.UseTerrain(nearby);
        }
    }
}
