using System.Collections.Generic;
using UnityEngine;

namespace Roadlife
{
    internal static class RoadMarkingLayout
    {
        internal static float Start(float length, float width)
            => Mathf.Clamp(width * 1.15f / Mathf.Max(length, .001f), .35f, .85f);

        // A single global choice avoids order-dependent pairs and crossed guide curves.
        internal static bool MainPair(Vector3 center, IList<Vector3> connected, out int a, out int b)
        {
            a = b = -1;
            float best = -.4f;
            Vector3 up = center.normalized;
            for (int i = 0; i < connected.Count; i++)
            for (int j = i + 1; j < connected.Count; j++)
            {
                float dot = Vector3.Dot(Vector3.ProjectOnPlane(connected[i] - center, up).normalized,
                    Vector3.ProjectOnPlane(connected[j] - center, up).normalized);
                if (dot < best) { best = dot; a = i; b = j; }
            }
            return a >= 0;
        }

        // An arm's edge is internal to the junction wherever another arm covers it.
        internal static bool Exposed(Vector3 point, Vector3 center, IList<Vector3> connected,
            IList<float> widths, float halfWidth, int ownArm)
        {
            Vector3 up = center.normalized;
            Vector3 local = Vector3.ProjectOnPlane(point - center, up);
            if (local.magnitude < halfWidth - .012f) return false;
            for (int arm = 0; arm < connected.Count; arm++)
            {
                if (arm == ownArm) continue;
                Vector3 end = Vector3.ProjectOnPlane((connected[arm] - center) / 2f, up);
                float length = end.magnitude;
                if (length < .001f) continue;
                Vector3 direction = end / length;
                float along = Vector3.Dot(local, direction);
                if (along < 0 || along > length) continue;
                float width = Mathf.Lerp(halfWidth, widths[arm], along / length);
                if (Mathf.Abs(Vector3.Dot(local, Vector3.Cross(up, direction))) < width - .012f) return false;
            }
            return true;
        }
    }
}
