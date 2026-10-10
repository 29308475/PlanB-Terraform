using System;
using System.Collections.Generic;
using System.Linq;

namespace PlanBProductionChains
{
    internal static class HexModuleLayout
    {
        internal static double Compactness(HashSet<(int, int)> cells)
        {
            var width = cells.Max(x => x.Item1 + x.Item2 * .5) - cells.Min(x => x.Item1 + x.Item2 * .5) + 1;
            var height = (cells.Max(x => x.Item2) - cells.Min(x => x.Item2)) * .8660254 + 1;
            return width * height + Math.Abs(width - height) * 2;
        }
        internal static int2 PlaceCompact(HashSet<(int, int)> footprint, HashSet<(int, int)> occupied, int2[] neighbors, int width)
        {
            for (var r = 0; r <= occupied.Count + 4; r++)
                for (var q = 0; q < width; q++)
                {
                    if (footprint.Any(x => occupied.Contains((x.Item1 + q, x.Item2 + r)))) continue;
                    if (occupied.Count > 0 && !footprint.Any(x => neighbors.Any(n => occupied.Contains((x.Item1 + q + n.x, x.Item2 + r + n.y))))) continue;
                    foreach (var cell in footprint) occupied.Add((cell.Item1 + q, cell.Item2 + r));
                    return new int2(q, r);
                }
            throw new InvalidOperationException("Keine kompakte Gebäudeanordnung möglich.");
        }
        internal static List<int2> DepotOffsets(int count, int distance)
        {
            var result = new List<int2>();
            for (var i = 0; i < count; i++)
                result.Add(i % 2 == 0 ? new int2(i / 2, -distance) : new int2(-(i / 2), distance));
            return result;
        }

        internal static int2 Place(HashSet<(int, int)> footprint, HashSet<(int, int)> occupied, int2[] neighbors)
        {
            if (occupied.Count == 0)
            {
                occupied.UnionWith(footprint);
                return new int2(0, 0);
            }
            // Keep all production centers on a straight hex axis. Depots face the
            // two exterior rail frontages instead of becoming buried in a cluster.
            var limit = occupied.Max(x => x.Item1) - footprint.Min(x => x.Item1) + 1;
            for (var q = 1; q <= limit; q++)
            {
                if (footprint.Any(x => occupied.Contains((x.Item1 + q, x.Item2)))) continue;
                if (!footprint.Any(x => neighbors.Any(n => occupied.Contains((x.Item1 + q + n.x, x.Item2 + n.y))))) continue;
                foreach (var cell in footprint) occupied.Add((cell.Item1 + q, cell.Item2));
                return new int2(q, 0);
            }
            throw new InvalidOperationException("Keine zusammenhängende Bauanordnung gefunden.");
        }

    }
}
