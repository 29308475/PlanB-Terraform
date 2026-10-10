using UnityEngine;
using System.Collections.Generic;

namespace Roadlife
{
    // Native terrain triangles supply the same height to the background, road and paint.
    // The estimated fan is retained as a fallback before a terrain block is available.
    internal sealed class RoadSurface
    {
        internal readonly Vector3 center;
        internal readonly Vector3[] corners;
        private List<Vector3> faces = new List<Vector3>();
        private Vector2[] faceLocal, faceMin, faceMax;
        private Vector3[] faceNormals;
        internal bool UsesTerrain { get; private set; }
        private readonly Vector3 right, forward;
        private readonly Vector3 radialUp;
        private readonly float planeRadius;
        private bool hasBridgeEdge;
        private Vector3 bridgeEdge, bridgeEdgeAxis;
        private float bridgeEdgeCoordinate;
        private sealed class RoadEdge { internal Vector3 center, side, axis; internal float[] radii; }
        private readonly List<RoadEdge> roadEdges = new List<RoadEdge>();
        internal void MatchRoadEdge(Vector3 seam, Vector3 side, float[] radii)
            => roadEdges.Add(new RoadEdge { center = seam, side = side,
                axis = Vector3.Cross(side, seam.normalized).normalized, radii = radii });
        private readonly Vector3[] bufferA = new Vector3[12], bufferB = new Vector3[12];
        internal RoadSurface(Vector3 center, Vector3[] corners)
        {
            this.center = center; this.corners = corners;
            Vector3 up = center.normalized;
            radialUp = up; planeRadius = center.magnitude;
            right = Vector3.Cross(up, Vector3.forward).normalized;
            if (right.sqrMagnitude < .1f) right = Vector3.Cross(up, Vector3.right).normalized;
            forward = Vector3.Cross(right, up).normalized;
            for (int i = 0; i < corners.Length; i++)
            { faces.Add(center); faces.Add(corners[i]); faces.Add(corners[(i + 1) % corners.Length]); }
            IndexFaces();
        }

        private void IndexFaces()
        {
            faceLocal = new Vector2[faces.Count];
            faceNormals = new Vector3[faces.Count / 3];
            faceMin = new Vector2[faces.Count / 3]; faceMax = new Vector2[faces.Count / 3];
            for (int i = 0; i + 2 < faces.Count; i += 3)
            {
                Vector2 a = Local(faces[i]), b = Local(faces[i + 1]), c = Local(faces[i + 2]);
                faceLocal[i] = a; faceLocal[i + 1] = b; faceLocal[i + 2] = c;
                faceNormals[i / 3] = Vector3.Cross(faces[i + 1] - faces[i], faces[i + 2] - faces[i]);
                faceMin[i / 3] = Vector2.Min(a, Vector2.Min(b, c));
                faceMax[i / 3] = Vector2.Max(a, Vector2.Max(b, c));
            }
        }

        internal void AppendSurface(float layer, List<Vector3> result)
        {
            foreach (var point in faces) result.Add(point + point.normalized * layer);
        }

        // Trim native terrain triangles to this tile while retaining their real heights.
        internal void UseTerrain(List<Vector3> terrain)
        {
            var clipped = new List<Vector3>();
            // Clip against the tile outline once, not against each estimated fan
            // triangle. Native triangles already supply the real terrain subdivision.
            var outline = new Vector2[corners.Length];
            float area = 0;
            for (int i = 0; i < corners.Length; i++) outline[i] = Local(corners[i]);
            for (int i = 0; i < corners.Length; i++) area += Cross(outline[i], outline[(i + 1) % corners.Length]);
            float sign = Mathf.Sign(area);
            for (int i = 0; i + 2 < terrain.Count; i += 3)
            {
                Vector3 a = terrain[i], b = terrain[i + 1], c = terrain[i + 2];
                if (Mathf.Abs(Cross(Local(b) - Local(a), Local(c) - Local(a))) < .0000001f) continue;
                bufferA[0] = a; bufferA[1] = b; bufferA[2] = c;
                int count = 3;
                Vector3[] source = bufferA, target = bufferB;
                for (int edge = 0; edge < outline.Length && count > 0; edge++)
                {
                    Vector2 start = outline[edge], end = outline[(edge + 1) % outline.Length];
                    int written = 0;
                    Vector3 previous = source[count - 1];
                    float previousDistance = sign * Cross(end - start, Local(previous) - start);
                    for (int p = 0; p < count; p++)
                    {
                        Vector3 current = source[p];
                        float distance = sign * Cross(end - start, Local(current) - start);
                        if ((distance >= -.000001f) != (previousDistance >= -.000001f))
                            target[written++] = Intersection(previous, current, previousDistance, distance);
                        if (distance >= -.000001f) target[written++] = current;
                        previous = current; previousDistance = distance;
                    }
                    count = written; var swap = source; source = target; target = swap;
                }
                Vector3 normal = Vector3.Cross(b - a, c - a);
                for (int p = 1; p + 1 < count; p++)
                {
                    if (Mathf.Abs(Cross(Local(source[p]) - Local(source[0]), Local(source[p + 1]) - Local(source[0]))) < .0000001f) continue;
                    clipped.Add(OnPlane(source[0], normal, a, 0));
                    clipped.Add(OnPlane(source[p], normal, a, 0));
                    clipped.Add(OnPlane(source[p + 1], normal, a, 0));
                }
            }
            if (clipped.Count > 0) { faces = clipped; UsesTerrain = true; IndexFaces(); }
        }

        // Bring the land approach to the bridge's shared edge instead of projecting
        // that edge back down onto the riverbank. The background is emitted first.
        internal void BridgeApproach(Vector3 seam, bool fullTile = false)
        {
            Vector3 up = center.normalized;
            Vector3 along = Vector3.ProjectOnPlane(seam - center, up);
            float length = along.magnitude;
            if (length < .001f) return;
            along /= length;
            Vector3 anchor = fullTile ? center - along * length : center;
            float anchorRadius = Project(anchor, 0).magnitude;
            float startSlope;
            if (fullTile)
            {
                // Sample inside the tile; the adjoining tile retains its native
                // surface. Begin at the inland edge rather than halfway to shore.
                float insideRadius = Project(center - along * (length * .5f), 0).magnitude;
                startSlope = (insideRadius - anchorRadius) * 4f;
                length *= 2f;
            }
            else
            {
                float inlandRadius = Project(center - along * (length * .5f), 0).magnitude;
                startSlope = (anchorRadius - inlandRadius) * 2f;
            }
            var ramp = new List<Vector3>(faces.Count * 4);
            for (int i = 0; i + 2 < faces.Count; i += 3)
            {
                Vector3 a = faces[i], b = faces[i + 1], c = faces[i + 2];
                Vector3 ab = (a + b) / 2f, bc = (b + c) / 2f, ca = (c + a) / 2f;
                ramp.Add(a); ramp.Add(ab); ramp.Add(ca);
                ramp.Add(ab); ramp.Add(b); ramp.Add(bc);
                ramp.Add(ca); ramp.Add(bc); ramp.Add(c);
                ramp.Add(ab); ramp.Add(bc); ramp.Add(ca);
            }
            for (int i = 0; i < ramp.Count; i++)
            {
                Vector3 p = ramp[i];
                float t = Mathf.Clamp01(Vector3.Dot(Vector3.ProjectOnPlane(p - anchor, up), along) / length);
                // The bridge edge lies in the plane normal to its radial up vector.
                Vector3 direction = p.normalized;
                float target = seam.magnitude / Vector3.Dot(direction, seam.normalized);
                // A shore ridge can be higher than the interpolated bridge edge.
                // Lowering the ramp would expose both terrain and the city background.
                // Interpolate between fixed endpoints, not each triangle's own
                // terrain height: the latter introduces a different crest per face.
                float radius = BridgeDesign.ApproachHeight(anchorRadius, target, startSlope, t);
                if (t <= 0) radius = p.magnitude;
                ramp[i] = direction * Mathf.Max(p.magnitude, radius);
            }
            faces = ramp;
            IndexFaces();
        }

        internal void CoastalApproach(Vector3 start, Vector3 end)
        {
            Vector3 up = start.normalized;
            Vector3 axis = Vector3.ProjectOnPlane(end - start, up);
            float length = axis.magnitude;
            if (length < .001f) return;
            axis /= length;
            // The same endpoints are used by every land tile in this approach.
            // Subdivide once so the smooth profile is represented between seams.
            var ramp = new List<Vector3>(faces.Count * 4);
            for (int i = 0; i + 2 < faces.Count; i += 3)
            {
                Vector3 a = faces[i], b = faces[i + 1], c = faces[i + 2];
                Vector3 ab = (a + b) / 2f, bc = (b + c) / 2f, ca = (c + a) / 2f;
                ramp.Add(a); ramp.Add(ab); ramp.Add(ca);
                ramp.Add(ab); ramp.Add(b); ramp.Add(bc);
                ramp.Add(ca); ramp.Add(bc); ramp.Add(c);
                ramp.Add(ab); ramp.Add(bc); ramp.Add(ca);
            }
            for (int i = 0; i < ramp.Count; i++)
            {
                Vector3 p = ramp[i];
                float t = Vector3.Dot(Vector3.ProjectOnPlane(p - start, up), axis) / length;
                if (t <= 0 || t > 1.001f) continue;
                float height = BridgeDesign.ApproachHeight(start.magnitude, end.magnitude, 0, t);
                // Fade in from each native point near the inland anchor. Its
                // transverse terrain slope need not equal the center's radius.
                float fade = Mathf.Clamp01(t / .15f);
                fade = fade * fade * (3f - 2f * fade);
                ramp[i] = p.normalized * Mathf.Max(p.magnitude, Mathf.Lerp(p.magnitude, height, fade));
            }
            faces = ramp; IndexFaces();
        }

        internal void MatchBridgeEdge(Vector3 seam)
        {
            Vector3 axis = Vector3.ProjectOnPlane(seam - center, radialUp);
            float length = axis.magnitude;
            if (length < .001f) return;
            axis /= length;
            hasBridgeEdge = true;
            bridgeEdge = seam; bridgeEdgeAxis = axis;
            Vector3 edgeFlat = seam * (planeRadius / Vector3.Dot(seam, radialUp)) - center;
            bridgeEdgeCoordinate = Vector3.Dot(edgeFlat, axis);
            Vector3 seamUp = seam.normalized;
            for (int i = 0; i < faces.Count; i++)
            {
                Vector3 point = faces[i];
                // Perspective coordinates give the same boundary for every
                // height: a radial displacement must not move the seam test.
                Vector3 flat = point * (planeRadius / Vector3.Dot(point, radialUp)) - center;
                Vector3 seamFlat = seam * (planeRadius / Vector3.Dot(seam, radialUp)) - center;
                float distance = Vector3.Dot(seamFlat - flat, axis);
                float blend = Mathf.Clamp01(1f - distance / (length * .5f));
                blend = blend * blend * (3f - 2f * blend);
                if (blend <= 0) continue;
                Vector3 direction = point.normalized;
                float target = seam.magnitude / Vector3.Dot(direction, seamUp);
                float radius = Mathf.Lerp(point.magnitude, target, blend);
                // Keep terrain clearance inland; the shared edge itself is
                // fixed by the maximum native height sampled across the lanes.
                if (blend < .9999f) radius = Mathf.Max(point.magnitude, radius);
                faces[i] = direction * radius;
            }
            IndexFaces();
        }

        internal Vector3 Project(Vector3 position, float offset)
        {
            Vector3 direction = position.normalized;
            Vector2 local = Local(position);
            float px = local.x, py = local.y;
            for (int i = 0; i + 2 < faces.Count; i += 3)
            {
                Vector3 origin = faces[i];
                if (px < faceMin[i / 3].x - .002f || px > faceMax[i / 3].x + .002f
                    || py < faceMin[i / 3].y - .002f || py > faceMax[i / 3].y + .002f) continue;
                Vector2 basePoint = faceLocal[i];
                float x = px - basePoint.x, y = py - basePoint.y;
                Vector2 edgeA = faceLocal[i + 1] - basePoint, edgeB = faceLocal[i + 2] - basePoint;
                float ax = edgeA.x, ay = edgeA.y;
                float bx = edgeB.x, by = edgeB.y;
                float determinant = ax * by - ay * bx;
                if (Mathf.Abs(determinant) < .000001f) continue;
                float u = (x * by - y * bx) / determinant;
                float v = (ax * y - ay * x) / determinant;
                if (u < -.00001f || v < -.00001f || u + v > 1.00001f) continue;
                Vector3 normal = faceNormals[i / 3];
                float denominator = Vector3.Dot(normal, direction);
                if (Mathf.Abs(denominator) < .000001f) continue;
                float radius = Vector3.Dot(normal, origin) / denominator;
                return AtBridgeEdge(direction * radius, offset);
            }
            // A road seam can lie slightly beyond the tile; keep its supplied elevation.
            return AtBridgeEdge(position, offset);
        }

        private Vector3 AtBridgeEdge(Vector3 point, float layer)
        {
            Vector3 direction = point.normalized;
            float radius = point.magnitude;
            foreach (var edge in roadEdges)
            {
                Vector3 flat = point * (edge.center.magnitude / Vector3.Dot(point, edge.center.normalized)) - edge.center;
                float distance = Mathf.Abs(Vector3.Dot(flat, edge.axis));
                float lateral = Vector3.Dot(flat, edge.side);
                if (distance >= .02f || Mathf.Abs(lateral) > .27f) continue;
                float index = Mathf.Clamp(lateral / .12f + 2f, 0, 4);
                int lower = Mathf.Min(3, Mathf.FloorToInt(index));
                float target = Mathf.Lerp(edge.radii[lower], edge.radii[lower + 1], index - lower);
                float blend = 1f - distance / .02f;
                blend = blend * blend * (3f - 2f * blend);
                radius = Mathf.Lerp(radius, target, blend);
            }
            if (hasBridgeEdge)
            {
                Vector3 flat = point * (planeRadius / Vector3.Dot(point, radialUp)) - center;
                float distance = Mathf.Abs(Vector3.Dot(flat, bridgeEdgeAxis) - bridgeEdgeCoordinate);
                // Clipping can create new seam vertices after the surface faces
                // were aligned. Snap those generated points to the same plane.
                float blend = Mathf.Clamp01(1f - distance / .02f);
                blend = blend * blend * (3f - 2f * blend);
                float target = bridgeEdge.magnitude / Vector3.Dot(direction, bridgeEdge.normalized);
                radius = Mathf.Lerp(radius, target, blend);
            }
            return direction * (radius + layer);
        }

        private Vector2 Local(Vector3 point)
        {
            // Perspective projection onto the tile's tangent plane preserves
            // straight triangle edges and avoids a normalization for every clip test.
            Vector3 p = point * (planeRadius / Vector3.Dot(point, radialUp)) - center;
            return new Vector2(Vector3.Dot(p, right), Vector3.Dot(p, forward));
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private Vector3 Intersection(Vector3 previous, Vector3 current, float previousDistance, float distance)
        {
            float a = previousDistance * Vector3.Dot(previous, radialUp);
            float b = distance * Vector3.Dot(current, radialUp);
            return Vector3.Lerp(previous, current, a / (a - b));
        }

        internal void ClipTriangle(Vector3 a, Vector3 b, Vector3 c, float layer, List<Vector3> result, bool preserveHeight = false)
        {
            Vector2 localA = Local(a), localB = Local(b), localC = Local(c);
            Vector2 min = Vector2.Min(localA, Vector2.Min(localB, localC));
            Vector2 max = Vector2.Max(localA, Vector2.Max(localB, localC));
            for (int fan = 0; fan + 2 < faces.Count; fan += 3)
            {
                if (max.x < faceMin[fan / 3].x - .000001f || min.x > faceMax[fan / 3].x + .000001f
                    || max.y < faceMin[fan / 3].y - .000001f || min.y > faceMax[fan / 3].y + .000001f) continue;
                Vector2 co = faceLocal[fan], ca = faceLocal[fan + 1], cb = faceLocal[fan + 2];
                float area = Cross(ca - co, cb - co);
                if (Mathf.Abs(area) < .0000001f) continue;
                if (preserveHeight && Mathf.Abs(Cross(Local(b) - Local(a), Local(c) - Local(a))) < .0000001f) continue;
                float sign = Mathf.Sign(area);
                bufferA[0] = a; bufferA[1] = b; bufferA[2] = c;
                int count = 3;
                Vector3[] source = bufferA, target = bufferB;
                for (int edge = 0; edge < 3 && count > 0; edge++)
                {
                    Vector2 start = edge == 0 ? co : edge == 1 ? ca : cb;
                    Vector2 end = edge == 0 ? ca : edge == 1 ? cb : co;
                    int written = 0;
                    Vector3 previous = source[count - 1];
                    float previousDistance = sign * Cross(end - start, Local(previous) - start);
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 current = source[i];
                        float distance = sign * Cross(end - start, Local(current) - start);
                        bool inside = distance >= -0.000001f, previousInside = previousDistance >= -0.000001f;
                        if (inside != previousInside)
                            target[written++] = Intersection(previous, current, previousDistance, distance);
                        if (inside) target[written++] = current;
                        previous = current; previousDistance = distance;
                    }
                    count = written; var swap = source; source = target; target = swap;
                }
                Vector3 planeOrigin = preserveHeight ? a : faces[fan];
                Vector3 normal = preserveHeight ? Vector3.Cross(b - a, c - a)
                    : faceNormals[fan / 3];
                for (int i = 1; i + 1 < count; i++)
                {
                    if (Mathf.Abs(Cross(Local(source[i]) - Local(source[0]), Local(source[i + 1]) - Local(source[0]))) < .0000001f) continue;
                    result.Add(OnPlane(source[0], normal, planeOrigin, layer));
                    result.Add(OnPlane(source[i], normal, planeOrigin, layer));
                    result.Add(OnPlane(source[i + 1], normal, planeOrigin, layer));
                }
            }
        }

        private Vector3 OnPlane(Vector3 position, Vector3 normal, Vector3 planeOrigin, float layer)
        {
            Vector3 direction = position.normalized;
            return AtBridgeEdge(direction * (Vector3.Dot(normal, planeOrigin) / Vector3.Dot(normal, direction)), layer);
        }
    }
}
