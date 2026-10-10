using System;

namespace PlanBSkyscraperMod
{
    // No Unity dependencies: stable across reloads, camera movement and generation order.
    internal static class VariantRules
    {
        internal const int Count = 25;
        internal static bool KeepDome(int x,int y,int seed,int percent)
            => Hash(x,y,seed,0x751D48ABu)%100 < percent;
        internal static readonly float[] Heights = { 0.55f, 0.72f, 0.90f, 1.10f, 1.30f };

        internal static uint Hash(int x, int y, int seed, uint salt)
        {
            unchecked
            {
                uint h = (uint)x * 0x9E3779B9u ^ (uint)y * 0x85EBCA6Bu ^ (uint)seed ^ salt;
                h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu;
                return h ^ (h >> 16);
            }
        }

        internal static bool UseNew(int x, int y, int seed, int percent)
            => Hash(x, y, seed, 0xA341316Cu) % 100 < Math.Max(0, Math.Min(100, percent));

        internal static int Preferred(int x, int y, int seed)
            => (int)(Hash(x, y, seed, 0xC8013EA4u) % Count);

        internal static int SkylineBand(double x, double y, double radius, int citySeed)
        {
            radius = Math.Max(4, radius);
            // A coherent elliptical downtown mound rather than independent tower heights.
            double angle = Hash(0, 0, citySeed, 0x31D4A52Fu) / (double)uint.MaxValue * Math.PI;
            double u = Math.Cos(angle) * x + Math.Sin(angle) * y;
            double v = -Math.Sin(angle) * x + Math.Cos(angle) * y;
            double distanceSquared = (u * u / 1.21 + v * v / .81) / (radius * radius);
            double strength = Math.Exp(-2 * distanceSquared);
            // Broad medium-height shoulders; very tall accents only at the heart.
            return strength > .97 ? 4 : strength > .80 ? 3 : strength > .30 ? 2 : strength > .025 ? 1 : 0;
        }

        internal static void ClusterCenter(int cellX, int cellY, double radius, int citySeed, out double x, out double y)
        {
            double spacing = Math.Max(4, radius) * 2.5;
            // Stable neighborhood centers: growing cities reveal more clusters without
            // relocating the old ones. Small jitter avoids a perfectly regular grid.
            double jitterX = (Hash(cellX, cellY, citySeed, 0xD32E619Bu) / (double)uint.MaxValue - .5) * .08;
            double jitterY = (Hash(cellX, cellY, citySeed, 0x761A09C5u) / (double)uint.MaxValue - .5) * .08;
            x = cellX == 0 && cellY == 0 ? 0 : (cellX + jitterX) * spacing;
            y = cellX == 0 && cellY == 0 ? 0 : (cellY + jitterY) * spacing;
        }

        internal static int MultiSkylineBand(double x, double y, double radius, int citySeed)
        {
            radius = Math.Max(4, radius);
            // Rotate the entire layout per city, independent of district/style.
            double angle = Hash(0, 0, citySeed, 0x8417D65Fu) / (double)uint.MaxValue * Math.PI * 2;
            double u = Math.Cos(angle) * x + Math.Sin(angle) * y;
            double v = -Math.Sin(angle) * x + Math.Cos(angle) * y;
            return MultiSkylineLocalBand(u, v, radius, citySeed);
        }

        internal static int MultiSkylineLocalBand(double x, double y, double radius, int citySeed)
        {
            radius = Math.Max(4, radius);
            double spacing = radius * 2.5;
            int cellX = (int)Math.Floor(x / spacing + .5);
            int cellY = (int)Math.Floor(y / spacing + .5);
            int band = 0;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int cx = cellX + dx, cy = cellY + dy;
                ClusterCenter(cx, cy, radius, citySeed, out double peakX, out double peakY);
                int peakSeed = unchecked((int)Hash(cx, cy, citySeed, 0x9BC42710u));
                // Use the strongest local peak rather than adding heights: valleys
                // remain lower where clusters meet.
                band = Math.Max(band, SkylineBand(x - peakX, y - peakY, radius, peakSeed));
            }
            return band;
        }

        internal static int ChooseSkyline(int x, int y, int seed, int band, int[] neighborPreferences)
        {
            int start = Preferred(x, y, seed) / 5;
            int bestProfile = start, bestCount = int.MaxValue;
            // Change the silhouette to reduce repetitions, never the skyline height band.
            for (int step = 0; step < 5; step++)
            {
                int profile = (start + step) % 5;
                int count = 0;
                foreach (int neighbor in neighborPreferences)
                    if (neighbor >= 0 && neighbor / 5 == profile) count++;
                if (count < bestCount) { bestProfile = profile; bestCount = count; }
            }
            return bestProfile * 5 + Math.Max(0, Math.Min(4, band));
        }

        internal static int Choose(int x, int y, int seed, int[] neighborPreferences)
        {
            int preferred = Preferred(x, y, seed);
            int stride = 1 + (int)(Hash(x, y, seed, 0xAD90777Du) % 24);
            if (stride % 5 == 0) stride++; // Coprime to 25: visits the full catalogue.
            // Reserve the neighbors' preferred variants. This reduces local repetitions
            // without storing a city map or making the result depend on spawn order.
            for (int step = 0; step < Count; step++)
            {
                int candidate = (preferred + step * stride) % Count;
                bool conflict = false;
                for (int n = 0; n < neighborPreferences.Length; n++)
                    if (neighborPreferences[n] == candidate) { conflict = true; break; }
                if (!conflict) return candidate;
            }
            return preferred;
        }
    }
}
