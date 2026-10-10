using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roadlife
{
    // Replaces only the rendered road mesh. World contents and transport paths remain native.
    internal static class RoadVisuals
    {
        internal sealed class Geometry
        {
            internal Vector3[] vertices, normals;
            internal Color[] colors;
            internal int[] triangles;
            internal RoadSurface surface;
            internal bool hasBridgeStructure;
            internal void Apply(Mesh mesh)
            {
                mesh.Clear(); mesh.vertices = vertices; mesh.colors = colors;
                mesh.normals = normals; mesh.triangles = triangles; mesh.RecalculateBounds();
            }
        }

        internal static bool IsRailColor(Color color) => color == GWays.colorRailMain.ColorLinear
            || color == GWays.colorRailSide.ColorLinear || color == GWays.colorRailUp.ColorLinear;

        internal static Geometry Build(int2 coords, CItem_Way way, float halfWidth, Color cityGround, Func<int2, bool> isWide, Mesh original = null, bool replaceBridge = false)
        {
            var world = SSingleton<SWorld>.Inst;
            Vector3 origin = GHexes.Pos(coords);
            Vector3 center = replaceBridge ? BridgeSpan.Position(coords, .014f)
                : world.GetPosAbove(coords, way.ComputeBridgeHeight(coords) + .014f);
            Vector3 up = center.normalized;
            var builder = new Builder(origin);
            var neighbors = new int2[7];
            int count = world.GetNeighborsCoords(coords, neighbors, contiguous_5_7: true);
            bool land = way.ComputeBridgeHeight(coords) <= 0f && SSingleton<SPlanet>.Inst.GetWaterAboveIce(coords) <= GWater.waterDepthMin;
            if (land)
            {
                Vector3 groundCenter = world.GetPosAbove(coords);
                var corners = new List<Vector3>();
                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    if (WorldCoords.IsHex(neighbors[i]) && WorldCoords.IsHex(neighbors[next]))
                        corners.Add((groundCenter + world.GetPosAbove(neighbors[i]) + world.GetPosAbove(neighbors[next])) / 3f);
                }
                builder.surface = new RoadSurface(groundCenter, corners.ToArray());
                NativeRoadTerrain.Apply(coords, builder.surface);
                builder.Ground(cityGround);
            }
            var connected = new List<Vector3>();
            var connectedCoords = new List<int2>();
            var widths = new List<float>();
            for (int i = 0; i < count; i++)
            {
                var other = neighbors[i];
                if (WorldCoords.IsHex(other) && world.GetContent(other) is CItem_Way adjacent
                    && (adjacent.wayTypes & 1u) != 0)
                {
                    connected.Add(replaceBridge ? BridgeSpan.Position(other, .014f)
                        : world.GetPosAbove(other, adjacent.ComputeBridgeHeight(other) + .014f));
                    connectedCoords.Add(other);
                    widths.Add(Mathf.Min(halfWidth, isWide(other) ? halfWidth : .13f));
                }
            }

            if (land && replaceBridge && BridgeSpan.TryApproach(coords, out var approachStart, out var approachEnd))
                builder.surface.CoastalApproach(approachStart, approachEnd);
            if (land && replaceBridge)
                foreach (var other in connectedCoords)
                    if (BridgeSpan.IsBridge(other))
                        builder.surface.MatchBridgeEdge(RoadJoin(world.GetPosAbove(coords), BridgeSpan.Position(other, 0),
                            coords, other, true));
            if (land)
                foreach (var other in connectedCoords)
                {
                    var adjacent = (CItem_Way)world.GetContent(other);
                    if (adjacent.ComputeBridgeHeight(other) > 0f) continue;
                    Vector3 seam = Join(world.GetPosAbove(coords), world.GetPosAbove(other));
                    bool ordered = coords.x < other.x || (coords.x == other.x && coords.y < other.y);
                    Vector3 tangent = ordered ? world.GetPosAbove(other) - world.GetPosAbove(coords)
                        : world.GetPosAbove(coords) - world.GetPosAbove(other);
                    Vector3 side = Vector3.Cross(seam.normalized, tangent).normalized;
                    var neighborSurface = NativeSurface(other);
                    if (replaceBridge && BridgeSpan.TryApproach(other, out var neighborStart, out var neighborEnd))
                        neighborSurface.CoastalApproach(neighborStart, neighborEnd);
                    var radii = new float[5];
                    for (int i = 0; i < radii.Length; i++)
                    {
                        Vector3 sample = seam + side * ((i - 2) * .12f);
                        radii[i] = Mathf.Max(builder.surface.Project(sample, 0).magnitude,
                            neighborSurface.Project(sample, 0).magnitude);
                    }
                    builder.surface.MatchRoadEdge(seam, side, radii);
                }

            Color asphalt = GWays.colorRoad.ColorLinear;
            Color white = GWays.colorWhite.ColorLinear;
            Color yellow = GWays.colorRoadYellow.ColorLinear;
            if (original != null && (way.wayTypes & 2u) != 0)
            {
                // Preserve rail structures, excluding the native narrow asphalt and paint.
                var v = original.vertices;
                var colors = original.colors;
                var triangles = original.triangles;
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    if (colors.Length != v.Length || !IsRailColor(colors[a])) continue;
                    builder.SolidTriangle(v[a] + origin, v[b] + origin, v[c] + origin, colors[a]);
                }
            }
            else if (original != null)
            {
                var nativeVertices = original.vertices;
                var nativeColors = original.colors;
                var nativeTriangles = original.triangles;
                for (int i = 0; i + 2 < nativeTriangles.Length; i += 3)
                {
                    int a = nativeTriangles[i], b = nativeTriangles[i + 1], c = nativeTriangles[i + 2];
                    Color color = nativeColors.Length == nativeVertices.Length ? nativeColors[a] : asphalt;
                    float layer = color == white || color == yellow ? .016f : .014f;
                    builder.Triangle(nativeVertices[a] + origin, nativeVertices[b] + origin, nativeVertices[c] + origin, color, layer);
                }
                return Finish(builder);
            }
            if (connected.Count == 2)
            {
                BuildCurve(builder, RoadJoin(center, connected[0], coords, connectedCoords[0], replaceBridge), center, RoadJoin(center, connected[1], coords, connectedCoords[1], replaceBridge),
                    halfWidth, widths[0], widths[1], asphalt, white, yellow, way.ComputeBridgeHeight(coords) > 0f);
                if (replaceBridge && way.ComputeBridgeHeight(coords) > 0f)
                    BuildBridge(builder, RoadJoin(center, connected[0], coords, connectedCoords[0], replaceBridge), center, RoadJoin(center, connected[1], coords, connectedCoords[1], replaceBridge),
                        halfWidth, widths[0], widths[1], world.GetPosAbove(coords),
                        BridgeSpan.Find(coords, connectedCoords[0], connectedCoords[1]));
                return Finish(builder);
            }
            for (int arm = 0; arm < connected.Count; arm++)
            {
                Vector3 next = connected[arm];
                Vector3 end = Join(center, next);
                Vector3 side = Vector3.Cross(up, end - center).normalized;
                Vector3 a = center - side * halfWidth;
                Vector3 b = center + side * halfWidth;
                Vector3 c = end - side * widths[arm];
                Vector3 d = end + side * widths[arm];
                builder.Quad(a, b, c, d, asphalt);
                // Bridge deck sides; do not create a full city tile over water.
                if (way.ComputeBridgeHeight(coords) > 0f)
                {
                    builder.Quad(a, c, a - up * .025f, c - up * .025f, white);
                    builder.Quad(b, d, b - up * .025f, d - up * .025f, white);
                }
            }

            // Cover joins with a small asphalt hub.
            const int segments = 18;
            Vector3 right = Vector3.Cross(up, Vector3.forward).normalized;
            if (right.sqrMagnitude < .1f) right = Vector3.Cross(up, Vector3.right).normalized;
            Vector3 forward = Vector3.Cross(right, up).normalized;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float b = (i + 1) * Mathf.PI * 2f / segments;
                builder.Triangle(center, center + (right * Mathf.Cos(a) + forward * Mathf.Sin(a)) * halfWidth,
                    center + (right * Mathf.Cos(b) + forward * Mathf.Sin(b)) * halfWidth, asphalt);
            }
            for (int arm = 0; arm < connected.Count; arm++)
            {
                Vector3 next = connected[arm];
                Vector3 end = Join(center, next);
                Vector3 side = Vector3.Cross(up, end - center).normalized;
                float length = Vector3.Distance(center, end);
                float start = connected.Count > 2 ? RoadMarkingLayout.Start(length, halfWidth) : .6f;
                builder.Stripe(center, end, side, up, 0f, .008f, start, 1f, yellow);
                for (int edge = -1; edge <= 1; edge += 2)
                for (int step = 0; step < 12; step++)
                {
                    float t0 = Mathf.Lerp(start, 1f, step / 12f), t1 = Mathf.Lerp(start, 1f, (step + 1) / 12f);
                    float w0 = Mathf.Lerp(halfWidth, widths[arm], t0), w1 = Mathf.Lerp(halfWidth, widths[arm], t1);
                    Vector3 p0 = Vector3.Lerp(center, end, t0), p1 = Vector3.Lerp(center, end, t1);
                    if (connected.Count > 2 && (!RoadMarkingLayout.Exposed(p0 + side * edge * (w0 - .008f), center, connected, widths, halfWidth, arm)
                        || !RoadMarkingLayout.Exposed(p1 + side * edge * (w1 - .008f), center, connected, widths, halfWidth, arm))) continue;
                    builder.CurveLine(p0, p1, side, side, edge * (w0 - .008f), edge * (w1 - .008f), .006f, white);
                }
                for (int dash = 0; dash < 9; dash++)
                {
                    if (dash % 3 == 1) continue;
                    float from = Mathf.Lerp(start, 1f, dash / 9f);
                    float to = Mathf.Lerp(start, 1f, (dash + 1) / 9f);
                    Vector3 p0 = Vector3.Lerp(center, end, from), p1 = Vector3.Lerp(center, end, to);
                    float w0 = Mathf.Lerp(halfWidth, widths[arm], from), w1 = Mathf.Lerp(halfWidth, widths[arm], to);
                    builder.CurveLine(p0, p1, side, side, -w0 / 2f, -w1 / 2f, .005f, white);
                    builder.CurveLine(p0, p1, side, side, w0 / 2f, w1 / 2f, .005f, white);
                }
            }
            // One through connection, with exactly the same endpoints and widths as its approaches.
            if (connected.Count > 2 && RoadMarkingLayout.MainPair(center, connected, out int first, out int second))
            {
                Vector3 endA = (center + connected[first]) / 2f, endB = (center + connected[second]) / 2f;
                float startA = RoadMarkingLayout.Start(Vector3.Distance(center, endA), halfWidth);
                float startB = RoadMarkingLayout.Start(Vector3.Distance(center, endB), halfWidth);
                Vector3 a = Vector3.Lerp(center, endA, startA), b = Vector3.Lerp(center, endB, startB);
                float widthA = Mathf.Lerp(halfWidth, widths[first], startA), widthB = Mathf.Lerp(halfWidth, widths[second], startB);
                for (int step = 0; step < 18; step++)
                {
                        if (step % 3 == 1) continue;
                        float t0 = step / 18f, t1 = (step + 1) / 18f;
                        Vector3 p0 = Curve(a, center, b, t0), p1 = Curve(a, center, b, t1);
                        Vector3 s0 = Vector3.Cross(up, Vector3.Lerp(center - a, b - center, t0)).normalized;
                        Vector3 s1 = Vector3.Cross(up, Vector3.Lerp(center - a, b - center, t1)).normalized;
                        float w0 = Mathf.Lerp(widthA, widthB, t0), w1 = Mathf.Lerp(widthA, widthB, t1);
                        builder.CurveLine(p0, p1, s0, s1, -w0 / 2f, -w1 / 2f, .005f, white);
                        builder.CurveLine(p0, p1, s0, s1, w0 / 2f, w1 / 2f, .005f, white);
                }
            }
            return Finish(builder);
        }

        private static void BuildBridge(Builder builder, Vector3 a, Vector3 center, Vector3 b,
            float width, float startWidth, float endWidth, Vector3 ground, BridgeSpan span)
        {
            builder.hasBridgeStructure = true;
            Color concrete = new Color(.52f, .57f, .65f).linear;
            Color rail = new Color(.78f, .84f, .92f).linear;
            Color tower = new Color(.73f, .79f, .87f).linear;
            Color cable = new Color(.19f, .28f, .39f).linear;
            const int sections = 6;
            for (int i = 0; i < sections; i++)
            {
                float t0 = i / (float)sections, t1 = (i + 1) / (float)sections;
                Vector3 p0 = Curve(a, center, b, t0), p1 = Curve(a, center, b, t1);
                Vector3 up = ((p0 + p1) / 2f).normalized;
                Vector3 s0 = Vector3.Cross(p0.normalized, Vector3.Lerp(center - a, b - center, t0)).normalized;
                Vector3 s1 = Vector3.Cross(p1.normalized, Vector3.Lerp(center - a, b - center, t1)).normalized;
                float w0 = Mathf.Lerp(Mathf.Lerp(startWidth, endWidth, t0), width, Mathf.Sin(t0 * Mathf.PI));
                float w1 = Mathf.Lerp(Mathf.Lerp(startWidth, endWidth, t1), width, Mathf.Sin(t1 * Mathf.PI));
                // Tapered underside shares the deck's exact lateral endpoints.
                builder.SolidQuad(p0 - s0 * w0 - up * .03f, p0 + s0 * w0 - up * .03f,
                    p1 - s1 * w1 - up * .03f, p1 + s1 * w1 - up * .03f, concrete);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 e0 = p0 + s0 * side * (w0 + .006f), e1 = p1 + s1 * side * (w1 + .006f);
                    if (side > 0) builder.SolidQuad(e0, e1, e0 - up * .03f, e1 - up * .03f, concrete);
                    else builder.SolidQuad(e1, e0, e1 - up * .03f, e0 - up * .03f, concrete);
                    BridgeAsset.Beam(e0 + up * .04f, e1 + up * .04f, up, .012f, .012f,
                        (x, y, z) => builder.SolidTriangle(x, y, z, rail));
                    // Posts are beams with a horizontal up axis, so their frame stays valid.
                    BridgeAsset.Beam(e0, e0 + up * .04f, s0, .012f, .012f,
                        (x, y, z) => builder.SolidTriangle(x, y, z, rail));
                    if (i == sections - 1)
                        BridgeAsset.Beam(e1, e1 + up * .04f, s1, .012f, .012f,
                            (x, y, z) => builder.SolidTriangle(x, y, z, rail));
                    float h0 = BridgeDesign.CableHeight(span.tiles.Count, span.index, span.reversed ? 1f - t0 : t0);
                    float h1 = BridgeDesign.CableHeight(span.tiles.Count, span.index, span.reversed ? 1f - t1 : t1);
                    Vector3 c0 = e0 + p0.normalized * h0, c1 = e1 + p1.normalized * h1;
                    BridgeAsset.Beam(c0, c1, up, .012f, .012f,
                        (x, y, z) => builder.SolidTriangle(x, y, z, cable));
                    BridgeAsset.Beam(e0 + up * .045f, c0, s0, .005f, .005f,
                        (x, y, z) => builder.SolidTriangle(x, y, z, rail));
                }
            }
            Vector3 deck = Curve(a, center, b, .5f);
            Vector3 tangent = (b - a).normalized;
            if (span.index == BridgeDesign.TowerTile(span.tiles.Count))
            {
                Vector3 up = deck.normalized;
                Vector3 side = Vector3.Cross(up, tangent).normalized;
                float h = BridgeDesign.TowerHeight(span.tiles.Count) + .06f;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector3 foot = deck + side * sign * (width + .035f);
                    Vector3 bottom = foot.normalized * (ground.magnitude - .015f);
                    Vector3 top = foot + up * (h + .09f);
                    BridgeAsset.Beam(bottom, top, tangent, .07f, .09f,
                        (x, y, z) => builder.SolidTriangle(x, y, z, tower));
                }
                Vector3 topCenter = deck + up * (h + .065f);
                BridgeAsset.Beam(topCenter - side * (width + .07f), topCenter + side * (width + .07f), up, .055f, .045f,
                    (x, y, z) => builder.SolidTriangle(x, y, z, tower));
            }
        }

        private static Geometry Finish(Builder builder)
        {
            return new Geometry { vertices = builder.vertices.ToArray(), colors = builder.colors.ToArray(),
                normals = builder.normals.ToArray(), triangles = builder.triangles.ToArray(), surface = builder.surface,
                hasBridgeStructure = builder.hasBridgeStructure };
        }

        internal static Vector3 Curve(Vector3 a, Vector3 control, Vector3 b, float t)
        {
            float q = 1f - t;
            Vector3 value = a * q * q + control * (2f * q * t) + b * t * t;
            float radius = a.magnitude * q * q + control.magnitude * 2f * q * t + b.magnitude * t * t;
            return value.normalized * radius;
        }

        internal static Vector3 Join(Vector3 a, Vector3 b)
            => (a.normalized + b.normalized).normalized * ((a.magnitude + b.magnitude) / 2f);

        internal static RoadSurface NativeSurface(int2 coords)
        {
            var world = SSingleton<SWorld>.Inst;
            Vector3 center = world.GetPosAbove(coords);
            var neighbors = new int2[7];
            int count = world.GetNeighborsCoords(coords, neighbors, contiguous_5_7: true);
            var corners = new List<Vector3>();
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                if (WorldCoords.IsHex(neighbors[i]) && WorldCoords.IsHex(neighbors[next]))
                    corners.Add((center + world.GetPosAbove(neighbors[i]) + world.GetPosAbove(neighbors[next])) / 3f);
            }
            var surface = new RoadSurface(center, corners.ToArray());
            NativeRoadTerrain.Apply(coords, surface);
            return surface;
        }

        internal static Vector3 RoadJoin(Vector3 a, Vector3 b, int2 first, int2 second, bool replaceBridge)
        {
            Vector3 seam = Join(a, b);
            if (!replaceBridge || BridgeSpan.IsBridge(first) == BridgeSpan.IsBridge(second)) return seam;
            int2 land = BridgeSpan.IsBridge(first) ? second : first;
            var world = SSingleton<SWorld>.Inst;
            Vector3 center = world.GetPosAbove(land);
            var neighbors = new int2[7];
            int count = world.GetNeighborsCoords(land, neighbors, contiguous_5_7: true);
            var corners = new List<Vector3>();
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                if (WorldCoords.IsHex(neighbors[i]) && WorldCoords.IsHex(neighbors[next]))
                    corners.Add((center + world.GetPosAbove(neighbors[i]) + world.GetPosAbove(neighbors[next])) / 3f);
            }
            var surface = new RoadSurface(center, corners.ToArray());
            NativeRoadTerrain.Apply(land, surface);
            // Both halves sample the same native edge, irrespective of build order.
            // Sample across all four lanes so the outer edges cannot meet below terrain.
            Vector3 side = Vector3.Cross(seam.normalized, b - a).normalized;
            float clearance = (BridgeSpan.IsBridge(first) ? b : a).magnitude - center.magnitude;
            float radius = seam.magnitude;
            for (int i = -2; i <= 2; i++)
                radius = Mathf.Max(radius, surface.Project(seam + side * (i * .12f), clearance).magnitude);
            return seam.normalized * radius;
        }

        private static void BuildCurve(Builder builder, Vector3 a, Vector3 control, Vector3 b, float width, float startWidth, float endWidth,
            Color asphalt, Color white, Color yellow, bool bridge)
        {
            const int steps = 18;
            for (int i = 0; i < steps; i++)
            {
                float t0 = i / (float)steps, t1 = (i + 1) / (float)steps;
                Vector3 p0 = Curve(a, control, b, t0), p1 = Curve(a, control, b, t1);
                Vector3 s0 = Vector3.Cross(p0.normalized, Vector3.Lerp(control - a, b - control, t0)).normalized;
                Vector3 s1 = Vector3.Cross(p1.normalized, Vector3.Lerp(control - a, b - control, t1)).normalized;
                float w0 = Mathf.Lerp(Mathf.Lerp(startWidth, endWidth, t0), width, Mathf.Sin(t0 * Mathf.PI));
                float w1 = Mathf.Lerp(Mathf.Lerp(startWidth, endWidth, t1), width, Mathf.Sin(t1 * Mathf.PI));
                builder.Quad(p0 - s0 * w0, p0 + s0 * w0, p1 - s1 * w1, p1 + s1 * w1, asphalt);
                if (bridge)
                {
                    builder.Quad(p0 - s0 * w0, p1 - s1 * w1, p0 - s0 * w0 - p0.normalized * .025f, p1 - s1 * w1 - p1.normalized * .025f, white);
                    builder.Quad(p0 + s0 * w0, p1 + s1 * w1, p0 + s0 * w0 - p0.normalized * .025f, p1 + s1 * w1 - p1.normalized * .025f, white);
                }
                builder.CurveLine(p0, p1, s0, s1, 0, 0, .008f, yellow);
                builder.CurveLine(p0, p1, s0, s1, -w0 + .008f, -w1 + .008f, .006f, white);
                builder.CurveLine(p0, p1, s0, s1, w0 - .008f, w1 - .008f, .006f, white);
                if (width <= .15f || i % 3 == 1) continue;
                builder.CurveLine(p0, p1, s0, s1, -w0 / 2f, -w1 / 2f, .005f, white);
                builder.CurveLine(p0, p1, s0, s1, w0 / 2f, w1 / 2f, .005f, white);
            }
        }

        private sealed class Builder
        {
            internal readonly List<Vector3> vertices = new List<Vector3>();
            internal readonly List<Color> colors = new List<Color>();
            internal readonly List<Vector3> normals = new List<Vector3>();
            internal readonly List<int> triangles = new List<int>();
            private readonly Vector3 origin;
            internal RoadSurface surface;
            internal bool hasBridgeStructure;
            private readonly List<Vector3> projected = new List<Vector3>();
            internal Builder(Vector3 origin) { this.origin = origin; }
            internal void Ground(Color color)
            {
                projected.Clear(); surface.AppendSurface(.003f, projected);
                for (int i = 0; i + 2 < projected.Count; i += 3)
                    RawTriangle(projected[i], projected[i + 1], projected[i + 2], color);
            }
            internal void SolidTriangle(Vector3 a, Vector3 b, Vector3 c, Color color)
            {
                // Structural faces have their own winding; radial correction would flip the underside.
                int index = vertices.Count;
                vertices.Add(a - origin); vertices.Add(b - origin); vertices.Add(c - origin);
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                colors.Add(color); colors.Add(color); colors.Add(color);
                triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
            }
            internal void SolidQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
            { SolidTriangle(a, b, c, color); SolidTriangle(b, d, c, color); }
            internal void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color, float layer = .014f)
            {
                if (surface != null)
                {
                    projected.Clear(); surface.ClipTriangle(a, b, c, layer, projected);
                    for (int i = 0; i + 2 < projected.Count; i += 3)
                        RawTriangle(projected[i], projected[i + 1], projected[i + 2], color);
                    return;
                }
                // Bridge vertices already include the asphalt offset; paint needs
                // the same extra clearance as paint projected onto land.
                float lift = layer - .014f;
                RawTriangle(a + a.normalized * lift, b + b.normalized * lift, c + c.normalized * lift, color);
            }
            private void RawTriangle(Vector3 a, Vector3 b, Vector3 c, Color color)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), a.normalized) < 0f)
                { var swap = b; b = c; c = swap; }
                int index = vertices.Count;
                vertices.Add(a - origin); vertices.Add(b - origin); vertices.Add(c - origin);
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                colors.Add(color); colors.Add(color); colors.Add(color);
                triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
            }
            internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, float layer = .014f)
            { Triangle(a, c, b, color, layer); Triangle(b, c, d, color, layer); }
            internal void Stripe(Vector3 a, Vector3 b, Vector3 side, Vector3 up, float offset, float width, float from, float to, Color color)
            {
                Vector3 start = Vector3.Lerp(a, b, from) + side * offset + up * .001f;
                Vector3 end = Vector3.Lerp(a, b, to) + side * offset + up * .001f;
                Quad(start - side * width / 2f, start + side * width / 2f,
                    end - side * width / 2f, end + side * width / 2f, color, .016f);
            }
            internal void CurveLine(Vector3 a, Vector3 b, Vector3 sa, Vector3 sb, float oa, float ob, float width, Color color)
            {
                a += a.normalized * .001f + sa * oa;
                b += b.normalized * .001f + sb * ob;
                Quad(a - sa * width / 2f, a + sa * width / 2f, b - sb * width / 2f, b + sb * width / 2f, color, .016f);
            }
        }
    }
}
