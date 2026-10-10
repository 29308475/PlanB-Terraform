using System;
using System.Collections.Generic;
using System.Linq;

namespace PlanBBahnbetrieb
{
    internal struct HexCell
    {
        public int Q, R;
        public HexCell(int q, int r) { Q = q; R = r; }
        public HexCell Rotate() => new HexCell(-R, Q + R);
        public HexCell Mirror() => new HexCell(-Q - R, R);
        public static int Distance(HexCell a, HexCell b) => Math.Max(Math.Abs(a.Q - b.Q), Math.Max(Math.Abs(a.R - b.R), Math.Abs(a.Q + a.R - b.Q - b.R)));
    }

    internal static class StationLayout
    {
        public const int RailLength = 9, RailCount = 18, BufferCount = 4;
        public static List<HexCell> Rails() => Enumerable.Range(0, 2).SelectMany(r => Enumerable.Range(-4, RailLength).Select(q => new HexCell(q, r))).ToList();
        public static List<HexCell> Buffers() => Enumerable.Range(0, BufferCount).Select(q => new HexCell(q, -2)).ToList();
        public static List<HexCell> Footprint()
        {
            var result = new List<HexCell>();
            // Rectangular footprint in physical space, staggered in axial coordinates.
            for (var r = -4; r <= 2; r++)
                for (var q = (int)Math.Ceiling(-4.5 - r * .5); q <= (int)Math.Floor(4.5 - r * .5); q++)
                    result.Add(new HexCell(q, r));
            return result;
        }
    }
}
