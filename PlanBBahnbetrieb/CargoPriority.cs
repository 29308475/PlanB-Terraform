using System;
using System.Collections.Generic;

namespace PlanBBahnbetrieb
{
    internal static class CargoPriority
    {
        internal static int Evaluate(bool producer, bool consumer, int maximum, int total, int count)
        {
            if (consumer) return 1023;
            if (producer) return Math.Max(1, Math.Min(340, maximum - 20));
            return count == 0 ? 681 : (int)Math.Round(total / (double)count);
        }
        internal static void Refresh(FreightStation station, CDrone drone, int slot)
        {
            var anchor = drone.depotCoords;
            var record = Plugin.Instance.Registry.At(anchor);
            if (record?.Root == null) return;
            var item = station.GetStacks(anchor).stacks[slot].item;
            bool producer = false, consumer = false;
            int max = -1, total = 0, count = 0;
            var seen = new HashSet<int2> { anchor };
            var neighbors = drone.GetNeisDelta();
            for (var n = 1; item != null && n < neighbors.Count; n++)
            {
                var coords = drone.GetNei(neighbors, n);
                if (!seen.Add(coords)) continue;
                var content = SSingleton<SWorld>.Inst.GetContent(coords);
                var stacks = GHexes.stacks[coords.x, coords.y];
                if (stacks != null)
                    for (var i = 0; i < stacks.stacks.Length; i++)
                    {
                        var stack = stacks.stacks[i]; if (stack.item != item) continue;
                        if (content is CItem_ContentDepot depot)
                        {
                            var other = depot is FreightStation ? Plugin.Instance.Registry.At(coords) : null;
                            int value = other == null ? depot.dataPriority.GetValue(coords) : other.Priorities[i];
                            total += value; count++; max = Math.Max(max, value);
                        }
                        else { producer |= stack.IsProducer; consumer |= stack.IsConsumer; }
                    }
                if (content is CItem_WayStop)
                {
                    var line = SSingleton<SWays>.Inst.GetLine(coords);
                    if (line?.itemTransported == item && line.stops.Count > 1)
                    { consumer |= line.stops[0].coords == coords; producer |= line.stops[1].coords == coords; }
                }
            }
            record.Priorities[slot] = Evaluate(producer, consumer, max, total, count);
        }
    }
}
