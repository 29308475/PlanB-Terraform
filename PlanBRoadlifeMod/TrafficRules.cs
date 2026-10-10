using System;

namespace Roadlife
{
    internal static class TrafficRules
    {
        internal static bool WideRoad(bool wasWide, double population, int upgrade, int downgrade)
        {
            return wasWide ? population >= Math.Min(upgrade, downgrade) : population >= upgrade;
        }

        internal static float Density(float baseline, double population, float proximity, float growth, float cap)
        {
            double boost = Math.Log10(1d + Math.Max(0d, population) / 1000d) * growth;
            return (float)Math.Min(cap, Math.Max(0d, baseline * (1d + boost * Math.Max(0f, Math.Min(1f, proximity)))));
        }

        internal static float TruckFactor(float density, float cap, float slowdown)
        {
            return 1f - Math.Max(0f, Math.Min(.5f, slowdown)) * Math.Max(0f, Math.Min(1f, cap > 0 ? density / cap : 0f));
        }
    }
}
