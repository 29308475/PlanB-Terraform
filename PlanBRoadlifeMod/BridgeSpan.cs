using System.Collections.Generic;
using UnityEngine;

namespace Roadlife
{
    internal sealed class BridgeSpan
    {
        internal readonly List<int2> tiles;
        internal int index;
        internal bool reversed;
        internal float deckRadius;
        private static readonly BoundedCache<int2, BridgeSpan> cache = new BoundedCache<int2, BridgeSpan>(2048);
        private sealed class Approach { internal Vector3 start, end; internal List<int2> land; }
        private static readonly BoundedCache<int2, Approach> approaches = new BoundedCache<int2, Approach>(2048);
        internal static void Clear() { cache.Clear(); approaches.Clear(); }
        internal static void Invalidate(int2 coords, System.Action<int2> changed = null)
        {
            if (approaches.TryGet(coords, out var approach) && approach != null)
                foreach (var tile in approach.land) changed?.Invoke(tile);
            if (WorldCoords.IsHex(coords))
            {
                var neighbors = new int2[7];
                int count = SSingleton<SWorld>.Inst.GetNeighborsCoords(coords, neighbors, contiguous_5_7: true);
                for (int i = 0; i < count; i++)
                    if (approaches.TryGet(neighbors[i], out approach) && approach != null)
                        foreach (var tile in approach.land) changed?.Invoke(tile);
            }
            approaches.Clear();
            if (cache.TryGet(coords, out var span))
                foreach (var tile in span.tiles) { cache.Remove(tile); changed?.Invoke(tile); }
        }
        internal static bool IsBridge(int2 coords)
            => WorldCoords.IsHex(coords) && SSingleton<SWorld>.Inst.GetContent(coords) is CItem_Way way
                && (way.wayTypes & 1u) != 0 && (way.wayTypes & 2u) == 0 && way.ComputeBridgeHeight(coords) > 0f;

        internal static bool HasBridgeNeighbor(int2 coords)
            => TryApproach(coords, out _, out _);

        internal static bool TryApproach(int2 coords, out Vector3 start, out Vector3 end)
        {
            start = end = Vector3.zero;
            if (approaches.TryGet(coords, out var known))
            { if (known == null) return false; start = known.start; end = known.end; return true; }
            var world = SSingleton<SWorld>.Inst;
            var neighbors = new int2[7];
            var pending = new Queue<List<int2>>();
            pending.Enqueue(new List<int2> { coords });
            while (pending.Count > 0)
            {
                var path = pending.Dequeue();
                int2 current = path[path.Count - 1];
                if (!WorldCoords.IsHex(current) || !(world.GetContent(current) is CItem_Way way)) continue;
                int count = world.GetNeighborsCoords(current, neighbors, contiguous_5_7: true);
                var links = new List<int2>();
                for (int i = 0; i < count; i++)
                    if (WorldCoords.IsHex(neighbors[i]) && way.HasCommonWayType(world.GetContent(neighbors[i]) as CItem_Way)) links.Add(neighbors[i]);
                if (links.Count > 2) continue;
                foreach (var next in links)
                {
                    if (path.Contains(next)) continue;
                    if (IsBridge(next))
                    {
                        // Always rebuild outward from the same shore tile. Using
                        // the search path made the profile depend on build order.
                        var land = new List<int2> { current };
                        int2 previous = next, inland = current;
                        while (land.Count < 4)
                        {
                            int n = world.GetNeighborsCoords(inland, neighbors, contiguous_5_7: true);
                            int2 continuation = int2.negative; int connections = 0;
                            var inlandWay = world.GetContent(inland) as CItem_Way;
                            for (int j = 0; j < n; j++)
                                if (WorldCoords.IsHex(neighbors[j]) && inlandWay.HasCommonWayType(world.GetContent(neighbors[j]) as CItem_Way))
                                { connections++; if (neighbors[j] != previous) continuation = neighbors[j]; }
                            if (connections != 2 || continuation.Negative || IsBridge(continuation) || land.Contains(continuation)) break;
                            land.Add(continuation); previous = inland; inland = continuation;
                        }
                        var anchorCoords = land[land.Count - 1];
                        var anchorSurface = RoadVisuals.NativeSurface(anchorCoords);
                        known = new Approach { land = land, start = anchorSurface.Project(world.GetPosAbove(anchorCoords), 0),
                            end = RoadVisuals.RoadJoin(world.GetPosAbove(current), Position(next, 0), current, next, true) };
                        foreach (var tile in land) approaches.Put(tile, known);
                        start = known.start; end = known.end; return true;
                    }
                    if (path.Count < 4) { var extended = new List<int2>(path) { next }; pending.Enqueue(extended); }
                }
            }
            approaches.Put(coords, null);
            return false;
        }

        internal static BridgeSpan Find(int2 coords, int2 first, int2 second)
        {
            if (cache.TryGet(coords, out var cached))
            {
                return new BridgeSpan(cached.tiles, cached.index,
                    cached.index + 1 < cached.tiles.Count ? first == cached.tiles[cached.index + 1]
                        : cached.index > 0 && second == cached.tiles[cached.index - 1], cached.deckRadius);
            }
            var span = new BridgeSpan();
            var visited = new HashSet<int2> { coords };
            var left = Walk(coords, first, visited);
            var right = Walk(coords, second, visited);
            left.Reverse(); span.tiles.AddRange(left); span.tiles.Add(coords); span.tiles.AddRange(right);
            ExtendAcrossGap(span.tiles, visited, false, WorldCoords.IsHex(first) ? first : second);
            ExtendAcrossGap(span.tiles, visited, true, WorldCoords.IsHex(second) ? second : first);
            int2 a = span.tiles[0], b = span.tiles[span.tiles.Count - 1];
            if (a.x > b.x || (a.x == b.x && a.y > b.y))
            { span.tiles.Reverse(); span.reversed = true; }
            span.index = span.tiles.IndexOf(coords);
            var world = SSingleton<SWorld>.Inst;
            foreach (var tile in span.tiles)
            {
                // Water surface is a shared datum; the capped native bridge offset
                // still varies with seabed depth and can raise short spans unnecessarily.
                span.deckRadius = Mathf.Max(span.deckRadius, world.GetPosAbove(tile, .006f, addWater: true).magnitude);
            }
            float waterFloor = span.deckRadius;
            float firstBank = -1, lastBank = -1;
            var neighbors = new int2[7];
            for (int endpoint = 0; endpoint < 2; endpoint++)
            {
                var end = endpoint == 0 ? span.tiles[0] : span.tiles[span.tiles.Count - 1];
                var way = (CItem_Way)world.GetContent(end);
                int count = world.GetNeighborsCoords(end, neighbors, contiguous_5_7: true);
                for (int i = 0; i < count; i++)
                    if (WorldCoords.IsHex(neighbors[i]) && !IsBridge(neighbors[i])
                        && way.HasCommonWayType(world.GetContent(neighbors[i]) as CItem_Way))
                    {
                        float radius = world.GetPosAbove(neighbors[i]).magnitude;
                        if (endpoint == 0) firstBank = Mathf.Max(firstBank, radius);
                        else lastBank = Mathf.Max(lastBank, radius);
                    }
            }
            // A single tile needs two distinct land anchors to count as complete.
            if (span.tiles.Count == 1)
            {
                firstBank = WorldCoords.IsHex(first) && !IsBridge(first) ? world.GetPosAbove(first).magnitude : -1;
                lastBank = WorldCoords.IsHex(second) && !IsBridge(second) ? world.GetPosAbove(second).magnitude : -1;
            }
            for (int i = 0; i < span.tiles.Count; i++)
                cache.Put(span.tiles[i], new BridgeSpan(span.tiles, i, false,
                    BridgeDesign.DeckHeight(span.tiles.Count, i, waterFloor, firstBank, lastBank)));
            span.deckRadius = BridgeDesign.DeckHeight(span.tiles.Count, span.index, waterFloor, firstBank, lastBank);
            return span;
        }
        private BridgeSpan() { tiles = new List<int2>(); }
        private BridgeSpan(List<int2> shared, int tileIndex, bool backwards, float radius)
        { tiles = shared; index = tileIndex; reversed = backwards; deckRadius = radius; }

        internal static Vector3 Position(int2 coords, float offset)
        {
            if (!WorldCoords.IsHex(coords)) return Vector3.zero;
            var world = SSingleton<SWorld>.Inst;
            var way = world.GetContent(coords) as CItem_Way;
            if (way == null) return world.GetPosAbove(coords, offset);
            if (!IsBridge(coords)) return world.GetPosAbove(coords, way.ComputeBridgeHeight(coords) + offset);
            if (!cache.TryGet(coords, out var span))
            {
                var neighbors = new int2[7];
                int count = world.GetNeighborsCoords(coords, neighbors, contiguous_5_7: true);
                int2 first = int2.negative, second = int2.negative; int links = 0;
                for (int i = 0; i < count; i++)
                    if (WorldCoords.IsHex(neighbors[i]) && way.HasCommonWayType(world.GetContent(neighbors[i]) as CItem_Way))
                    { if (links == 0) first = neighbors[i]; else second = neighbors[i]; links++; }
                if (links > 2) return world.GetPosAbove(coords, way.ComputeBridgeHeight(coords) + offset);
                span = Find(coords, first, second);
            }
            return GHexes.upVector[coords.x, coords.y] * (span.deckRadius + offset);
        }

        private static List<int2> Walk(int2 previous, int2 current, HashSet<int2> visited)
        {
            var result = new List<int2>();
            var neighbors = new int2[7];
            var world = SSingleton<SWorld>.Inst;
            while (IsBridge(current) && visited.Count < 256 && visited.Add(current))
            {
                result.Add(current);
                var way = (CItem_Way)world.GetContent(current);
                int count = world.GetNeighborsCoords(current, neighbors, contiguous_5_7: true);
                int connections = 0;
                int2 next = int2.negative;
                for (int i = 0; i < count; i++)
                    if (WorldCoords.IsHex(neighbors[i]) && way.HasCommonWayType(world.GetContent(neighbors[i]) as CItem_Way))
                    {
                        connections++;
                        if (neighbors[i] != previous) next = neighbors[i];
                    }
                // End at branches instead of inventing a route through a bridge junction.
                if ((connections != 2 && !(connections == 1 && !IsBridge(previous))) || next.Negative) break;
                previous = current; current = next;
            }
            return result;
        }

        // A short construction gap must not assign a new elevation/tower station
        // to each aligned fragment. Missing tiles are layout samples only.
        private static void ExtendAcrossGap(List<int2> tiles, HashSet<int2> visited, bool atStart, int2 bankFallback)
        {
            if (tiles.Count >= 248 || (tiles.Count < 2 && !WorldCoords.IsHex(bankFallback))) return;
            var world = SSingleton<SWorld>.Inst;
            int2 current = atStart ? tiles[0] : tiles[tiles.Count - 1];
            int2 previous = tiles.Count == 1 ? bankFallback : atStart ? tiles[1] : tiles[tiles.Count - 2];
            Vector3 heading = Vector3.ProjectOnPlane(GHexes.Pos(current) - GHexes.Pos(previous), GHexes.Pos(current).normalized).normalized;
            var gap = new List<int2>();
            var neighbors = new int2[7];
            var way = world.GetContent(current) as CItem_Way;
            if (way == null) return;
            for (int step = 0; step < 8; step++)
            {
                int count = world.GetNeighborsCoords(current, neighbors, contiguous_5_7: true);
                int2 next = int2.negative; float best = .9f;
                for (int i = 0; i < count; i++)
                {
                    if (!WorldCoords.IsHex(neighbors[i]) || visited.Contains(neighbors[i])) continue;
                    float dot = Vector3.Dot(heading, (GHexes.Pos(neighbors[i]) - GHexes.Pos(current)).normalized);
                    if (dot > best) { best = dot; next = neighbors[i]; }
                }
                if (next.Negative) return;
                if (IsBridge(next))
                {
                    if (gap.Count == 0 || !way.HasCommonWayType(world.GetContent(next) as CItem_Way)) return;
                    var extension = Walk(current, next, visited);
                    gap.AddRange(extension);
                    if (atStart) { gap.Reverse(); tiles.InsertRange(0, gap); }
                    else tiles.AddRange(gap);
                    return;
                }
                if (world.GetContent(next) != null || SSingleton<SPlanet>.Inst.GetWaterAboveIce(next) <= GWater.waterDepthMin) return;
                gap.Add(next); previous = current; current = next;
            }
        }
    }
}
