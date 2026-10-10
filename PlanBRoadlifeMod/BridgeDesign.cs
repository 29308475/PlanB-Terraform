using System;

namespace Roadlife
{
    internal static class BridgeDesign
    {
        internal static int TowerTile(int count) => Math.Max(0, count / 2);
        internal static float TowerHeight(int count) => Math.Min(1.6f, Math.Max(.65f, count * .09f));
        internal static float DeckHeight(int count, int index, float waterFloor, float firstBank, float lastBank)
        {
            // Open fragments use the water datum, independent of seabed depth.
            if (firstBank < 0 || lastBank < 0) return waterFloor;
            // Use both shore centers as endpoints for every span length. A
            // plateau at the higher bank forces a crest into the lower approach.
            // Clamp the endpoints before interpolation, so water clearance does
            // not introduce a kink halfway across a sloping bridge.
            float start = Math.Max(waterFloor, firstBank);
            float end = Math.Max(waterFloor, lastBank);
            float t = (index + 1f) / (count + 1f);
            return start + (end - start) * t;
        }
        internal static float CableHeight(int count, int index, float progress)
        {
            float station = TowerTile(count) + .5f;
            float position = index + Math.Max(0f, Math.Min(1f, progress));
            float distance = position < station ? (station - position) / station
                : (position - station) / Math.Max(.5f, count - station);
            float rise = 1f - Math.Min(1f, distance);
            return .06f + TowerHeight(count) * rise * rise;
        }

        internal static float ApproachHeight(float start, float end, float startSlope, float t)
        {
            float delta = end - start;
            if (Math.Abs(delta) < .000001f) return start;
            // Monotone Hermite interpolation: retain the inland gradient without
            // introducing a crest, and meet the bridge with its linear gradient.
            float slope = Math.Max(0f, Math.Min(3f, startSlope / delta)) * delta;
            t = Math.Max(0f, Math.Min(1f, t));
            float t2 = t * t, t3 = t2 * t;
            return (2 * t3 - 3 * t2 + 1) * start
                + (t3 - 2 * t2 + t) * slope
                + (-2 * t3 + 3 * t2) * end + (t3 - t2) * delta;
        }
    }
}
