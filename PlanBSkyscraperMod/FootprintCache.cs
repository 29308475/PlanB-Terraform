using System;

namespace PlanBSkyscraperMod
{
    internal static class FootprintCache
    {
        // Hex spacing varies continuously over the sphere and with terrain altitude.
        // Reuse meshes in 1% size bands; round down so every cached mesh still fits.
        internal static float Radius(float actual)
        {
            if (!(actual > 0) || float.IsInfinity(actual)) throw new ArgumentOutOfRangeException(nameof(actual));
            var bucket = Math.Floor(Math.Log(actual) / Math.Log(1.01));
            var size = (float)(Math.Pow(1.01, bucket) * .999999);
            return Math.Min(actual, size);
        }
    }
}
