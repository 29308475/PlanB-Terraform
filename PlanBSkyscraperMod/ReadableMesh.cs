using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PlanBSkyscraperMod
{
    internal static class ReadableMesh
    {
        internal static Mesh Copy(Mesh source)
        {
            if (source.isReadable) return Object.Instantiate(source);
            // Imported Unity assets can discard CPU data. Copy GPU buffers once per
            // template, retaining all vertex attributes and submesh/material ordering.
            var result = new Mesh { name = source.name + "_Readable" };
            try
            {
                result.SetVertexBufferParams(source.vertexCount, source.GetVertexAttributes());
                for (int stream = 0; stream < source.vertexBufferCount; stream++)
                {
                    using (var buffer = source.GetVertexBuffer(stream))
                    {
                        if (buffer == null) throw new InvalidOperationException("Vertex buffer unavailable: " + source.name);
                        var data = new byte[buffer.count * buffer.stride];
                        buffer.GetData(data);
                        result.SetVertexBufferData(data, 0, 0, data.Length, stream);
                    }
                }
                using (var buffer = source.GetIndexBuffer())
                {
                    if (buffer == null) throw new InvalidOperationException("Index buffer unavailable: " + source.name);
                    int width = source.indexFormat == IndexFormat.UInt16 ? 2 : 4;
                    var data = new byte[buffer.count * buffer.stride];
                    buffer.GetData(data);
                    result.SetIndexBufferParams(data.Length / width, source.indexFormat);
                    result.SetIndexBufferData(data, 0, 0, data.Length);
                }
                result.subMeshCount = source.subMeshCount;
                for (int i = 0; i < source.subMeshCount; i++) result.SetSubMesh(i, source.GetSubMesh(i));
                result.bounds = source.bounds;
                return result;
            }
            catch { Object.Destroy(result); throw; }
        }
    }
}
