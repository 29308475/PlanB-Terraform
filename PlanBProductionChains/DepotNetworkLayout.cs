using System;
using System.Collections.Generic;
using System.Linq;

namespace PlanBProductionChains
{
    internal sealed class DepotRequest
    {
        public CItem Product;
        public int Q, R;
        public int Side; // -1 input frontage, +1 output frontage, 0 unrestricted.
        public int InputEdge, OutputEdge;
    }

    internal sealed class PlannedDepot
    {
        public CItem Product;
        public int Q, R;
    }

    internal static class DepotNetworkLayout
    {
        internal static int Distance(int q, int r, int otherQ, int otherR) =>
            Math.Max(Math.Max(Math.Abs(q - otherQ), Math.Abs(r - otherR)), Math.Abs(q + r - otherQ - otherR));

        internal static List<PlannedDepot> Create(List<DepotRequest> requests, HashSet<(int, int)> buildings, int range, int frontage)
        {
            var band = requests.Any(x => x.Side != 0) ? range : frontage;
            if (requests.Count == 0) return new List<PlannedDepot>();
            foreach (var request in requests)
            { request.InputEdge = buildings.Min(x => x.Item2); request.OutputEdge = buildings.Max(x => x.Item2); }
            List<PlannedDepot> result;
            try { result = CreateShared(requests, buildings, range, frontage, band); }
            catch (InvalidOperationException)
            {
                // Dense multi-product strips need reserved repeating depot positions,
                // so an early product cannot consume another product's transfer path.
                if (requests.Min(x => x.R) != requests.Max(x => x.R)) throw;
                result = requests.Any(x => x.Side != 0) ? CreateOriented(requests, buildings, range, frontage, band) : CreateStriped(requests, buildings, range, frontage);
            }
            Minimize(result, requests, buildings, range, frontage, band);
            Regularize(result, requests, buildings, range, band);
            return result;
        }

        internal static double Regularity(List<PlannedDepot> depots, HashSet<(int, int)> buildings)
        {
            var cells = new HashSet<(int, int)>(buildings);
            foreach (var depot in depots) cells.Add((depot.Q, depot.R));
            var holes = depots.GroupBy(x => x.R).Sum(row => row.Max(x => x.Q) - row.Min(x => x.Q) + 1 - row.Count());
            var minQ = buildings.Min(x => x.Item1); var maxQ = buildings.Max(x => x.Item1);
            var overhang = depots.Sum(x => Math.Max(0, minQ - x.Q) + Math.Max(0, x.Q - maxQ));
            return HexModuleLayout.Compactness(cells) + holes * 12 + overhang * 4;
        }

        private static void Regularize(List<PlannedDepot> result, List<DepotRequest> requests, HashSet<(int, int)> buildings, int range, int band)
        {
            var minQ = buildings.Min(x => x.Item1) - range;
            var maxQ = buildings.Max(x => x.Item1) + range;
            var minR = requests.Min(x => x.R) - band;
            var maxR = requests.Max(x => x.R) + band;
            var inputEdge = requests[0].InputEdge; var outputEdge = requests[0].OutputEdge;
            var score = Regularity(result, buildings);
            for (var pass = 0; pass < 4; pass++)
            {
                var changed = false;
                foreach (var depot in result)
                {
                    var demand = requests.Where(x => x.Product == depot.Product).ToList();
                    var network = result.Where(x => x.Product == depot.Product).ToList();
                    var blocked = new HashSet<(int, int)>(buildings);
                    foreach (var other in result.Where(x => x != depot)) blocked.Add((other.Q, other.R));
                    var oldQ = depot.Q; var oldR = depot.R;
                    var bestQ = oldQ; var bestR = oldR; var bestScore = score;
                    for (var r = minR; r <= maxR; r++)
                    {
                        if (oldR < inputEdge ? r >= inputEdge : oldR > outputEdge ? r <= outputEdge : r < inputEdge || r > outputEdge) continue;
                        for (var q = minQ; q <= maxQ; q++)
                        {
                            if (blocked.Contains((q, r))) continue;
                            depot.Q = q; depot.R = r;
                            var proposal = Regularity(result, buildings);
                            if (proposal >= bestScore - .001 || !ValidProduct(network, demand, range)) continue;
                            bestScore = proposal; bestQ = q; bestR = r;
                        }
                    }
                    depot.Q = bestQ; depot.R = bestR;
                    if (bestQ != oldQ || bestR != oldR) { score = bestScore; changed = true; }
                }
                if (!changed) break;
            }
        }

        private static bool ValidProduct(List<PlannedDepot> depots, List<DepotRequest> requests, int range)
        {
            if (depots.Count == 0 || !requests.All(r => depots.Any(d => Serves(d, r, range)))) return false;
            var reached = new HashSet<PlannedDepot> { depots[0] };
            bool changed;
            do
            {
                changed = false;
                foreach (var depot in depots)
                    if (!reached.Contains(depot) && reached.Any(d => Distance(d.Q, d.R, depot.Q, depot.R) <= range))
                    { reached.Add(depot); changed = true; }
            } while (changed);
            return reached.Count == depots.Count;
        }

        private static void Minimize(List<PlannedDepot> result, List<DepotRequest> requests, HashSet<(int, int)> buildings, int range, int frontage, int band)
        {
            foreach (var group in requests.GroupBy(x => x.Product))
            {
                var demand = group.ToList();
                var current = result.Where(x => x.Product == group.Key).ToList();
                var blocked = new HashSet<(int, int)>(buildings);
                foreach (var depot in result.Where(x => x.Product != group.Key)) blocked.Add((depot.Q, depot.R));
                var candidates = new List<PlannedDepot>();
                for (var q = demand.Min(x => x.Q) - range; q <= demand.Max(x => x.Q) + range; q++)
                    for (var r = requests.Min(x => x.R) - band; r <= requests.Max(x => x.R) + band; r++)
                    {
                        if ((r >= demand[0].InputEdge && r <= demand[0].OutputEdge) || blocked.Contains((q, r))) continue;
                        var candidate = new PlannedDepot { Product = group.Key, Q = q, R = r };
                        if (demand.Any(x => Serves(candidate, x, range))) candidates.Add(candidate);
                    }
                candidates = candidates.OrderByDescending(d => demand.Count(r => Serves(d, r, range)))
                    .ThenBy(d => demand.Sum(r => Distance(d.Q, d.R, r.Q, r.R))).ToList();
                var single = candidates.FirstOrDefault(d => demand.All(r => Serves(d, r, range)));
                if (single != null) current = new List<PlannedDepot> { single };
                else if (current.Count > 2 && demand.Max(x => x.Q) - demand.Min(x => x.Q) <= range * 3)
                {
                    var found = false;
                    for (var a = 0; a < candidates.Count && !found; a++)
                        for (var b = a + 1; b < candidates.Count && !found; b++)
                        {
                            var pair = new List<PlannedDepot> { candidates[a], candidates[b] };
                            if (!ValidProduct(pair, demand, range)) continue;
                            current = pair; found = true;
                        }
                }
                for (var i = current.Count - 1; i >= 0; i--)
                {
                    var trial = current.Where((d, index) => index != i).ToList();
                    if (ValidProduct(trial, demand, range)) current = trial;
                }
                result.RemoveAll(x => x.Product == group.Key);
                result.AddRange(current);
            }
        }

        internal static bool Serves(PlannedDepot depot, DepotRequest request, int range) =>
            (request.Side == 0 || (request.Side < 0 ? depot.R < request.InputEdge : depot.R > request.OutputEdge)) && Distance(depot.Q, depot.R, request.Q, request.R) <= range;

        private static List<PlannedDepot> CreateOriented(List<DepotRequest> requests, HashSet<(int, int)> buildings, int range, int frontage, int band)
        {
            var result = new List<PlannedDepot>();
            var occupied = new HashSet<(int, int)>(buildings);
            foreach (var side in new[] { -1, 1 })
            {
                var groups = requests.Where(x => x.Side == side).GroupBy(x => x.Product).ToList();
                var rows = band - frontage + 1;
                if (groups.Count > rows * range) throw new InvalidOperationException("Für die getrennten Eingangs- und Ausgangsseiten ist eine größere Depotreichweite nötig.");
                for (var i = 0; i < groups.Count; i++)
                {
                    var r = side * (frontage + i % rows); var phase = i / rows;
                    var min = groups[i].Min(x => x.Q); var max = groups[i].Max(x => x.Q);
                    var candidates = Enumerable.Range(min - range * 2, max - min + range * 4 + 1)
                        .Where(q => (q - phase) % range == 0 && !occupied.Contains((q, r))).ToList();
                    var first = candidates.Where(q => Distance(q, r, min, 0) <= range).DefaultIfEmpty(int.MinValue).Max();
                    var last = candidates.Where(q => Distance(q, r, max, 0) <= range).DefaultIfEmpty(int.MaxValue).Min();
                    if (first == int.MinValue || last == int.MaxValue) throw new InvalidOperationException("Keine erreichbare Eingangs-/Ausgangsseite möglich.");
                    for (var q = first; q <= Math.Max(first, last); q += range)
                    {
                        if (!occupied.Add((q, r))) throw new InvalidOperationException("Depotverbindung überschneidet ein Gebäude.");
                        result.Add(new PlannedDepot { Product = groups[i].Key, Q = q, R = r });
                    }
                }
            }
            foreach (var request in requests)
                if (!result.Any(d => d.Product == request.Product && Serves(d, request, range)))
                    throw new InvalidOperationException("Ein Gebäude erreicht seine Eingangs-/Ausgangsdepots nicht.");
            var minQ = buildings.Min(x => x.Item1) - range - 3;
            var maxQ = buildings.Max(x => x.Item1) + range + 3;
            foreach (var group in result.GroupBy(x => x.Product).ToList())
            {
                var role = requests.Where(x => x.Product == group.Key).Select(x => x.Side).Distinct().ToList();
                Connect(group.ToList(), result, occupied, minQ, maxQ, range, band, role.Count == 1 ? role[0] : 0);
            }
            return result;
        }

        private static List<PlannedDepot> CreateStriped(List<DepotRequest> requests, HashSet<(int, int)> buildings, int range, int frontage)
        {
            var groups = requests.GroupBy(x => x.Product).ToList();
            if (groups.Count > range * 2) throw new InvalidOperationException("Für direkte Übergaben dieser Baugruppe werden Depots mit größerer Reichweite benötigt.");
            var result = new List<PlannedDepot>();
            for (var i = 0; i < groups.Count; i++)
            {
                var phase = i / 2; var r = i % 2 == 0 ? -frontage : frontage;
                var min = groups[i].Min(x => x.Q); var max = groups[i].Max(x => x.Q);
                var candidates = Enumerable.Range(min - range * 2, max - min + range * 4 + 1)
                    .Where(q => (q - phase) % range == 0 && !buildings.Contains((q, r))).ToList();
                var first = candidates.Where(q => Distance(q, r, min, 0) <= range).DefaultIfEmpty(int.MinValue).Max();
                var last = candidates.Where(q => Distance(q, r, max, 0) <= range).DefaultIfEmpty(int.MaxValue).Min();
                if (first == int.MinValue || last == int.MaxValue) throw new InvalidOperationException("Keine direkte Depotversorgung möglich.");
                last = Math.Max(first, last);
                for (var q = first; q <= last; q += range)
                {
                    if (buildings.Contains((q, r))) throw new InvalidOperationException("Eine Depotverbindung kollidiert mit einem Gebäude.");
                    result.Add(new PlannedDepot { Product = groups[i].Key, Q = q, R = r });
                }
                if (!groups[i].All(request => result.Any(d => d.Product == request.Product && Distance(d.Q, d.R, request.Q, request.R) <= range)))
                    throw new InvalidOperationException("Eine Produktionsstufe ist außerhalb der Depotreichweite.");
            }
            return result;
        }

        private static List<PlannedDepot> CreateShared(List<DepotRequest> requests, HashSet<(int, int)> buildings, int range, int frontage, int band)
        {
            var result = new List<PlannedDepot>();
            if (requests.Count == 0) return result;
            var occupied = new HashSet<(int, int)>(buildings);
            var minQ = buildings.Min(x => x.Item1) - range - 2;
            var maxQ = buildings.Max(x => x.Item1) + range + 2;
            foreach (var group in requests.GroupBy(x => x.Product).OrderByDescending(x => x.Count()))
            {
                var pending = group.ToList();
                var productDepots = new List<PlannedDepot>();
                while (pending.Count > 0)
                {
                    PlannedDepot best = null; var bestScore = long.MinValue;
                    for (var q = minQ; q <= maxQ; q++)
                        foreach (var r in Enumerable.Range(requests.Min(x => x.R) - band, requests.Max(x => x.R) - requests.Min(x => x.R) + band * 2 + 1)
                            .Where(y => y < pending[0].InputEdge || y > pending[0].OutputEdge))
                        {
                            if (occupied.Contains((q, r))) continue;
                            var covered = pending.Count(x => Serves(new PlannedDepot { Q = q, R = r }, x, range));
                            if (covered == 0) continue;
                            var connection = productDepots.Count == 0 ? 0 : productDepots.Min(x => Distance(q, r, x.Q, x.R));
                            long score = covered * 100000L - connection * 100L - pending.Sum(x => Distance(q, r, x.Q, x.R));
                            if (score <= bestScore) continue;
                            bestScore = score; best = new PlannedDepot { Product = group.Key, Q = q, R = r };
                        }
                    if (best == null) throw new InvalidOperationException("Keine erreichbare Depotanordnung für " + RecipeCatalog.Name(group.Key));
                    pending.RemoveAll(x => Serves(best, x, range));
                    occupied.Add((best.Q, best.R)); productDepots.Add(best); result.Add(best);
                }
                var roles = group.Select(x => x.Side).Distinct().ToList();
                Connect(productDepots, result, occupied, minQ, maxQ, range, band, roles.Count == 1 ? roles[0] : 0,
                    requests.Min(x => x.R), requests.Max(x => x.R), requests[0].InputEdge, requests[0].OutputEdge);
            }
            return result;
        }

        private static void Connect(List<PlannedDepot> depots, List<PlannedDepot> result, HashSet<(int, int)> occupied,
            int minQ, int maxQ, int range, int frontage, int side = 0, int minCenterRow = 0, int maxCenterRow = 0, int inputEdge = 0, int outputEdge = 0)
        {
            var connected = new HashSet<(int, int)> { (depots[0].Q, depots[0].R) };
            foreach (var target in depots.Skip(1))
            {
                var targetCell = (target.Q, target.R);
                var queue = new Queue<(int, int)>();
                var previous = new Dictionary<(int, int), (int, int)>();
                foreach (var cell in connected) { queue.Enqueue(cell); previous[cell] = cell; }
                var found = false;
                while (queue.Count > 0 && !found)
                {
                    var cell = queue.Dequeue();
                    for (var dq = -range; dq <= range && !found; dq++)
                        for (var dr = -range; dr <= range && !found; dr++)
                        {
                            if (Distance(0, 0, dq, dr) > range) continue;
                            var next = (cell.Item1 + dq, cell.Item2 + dr);
                            if (next.Item1 < minQ || next.Item1 > maxQ || next.Item2 < minCenterRow - frontage || next.Item2 > maxCenterRow + frontage || previous.ContainsKey(next)) continue;
                            if ((side < 0 && next.Item2 >= inputEdge) || (side > 0 && next.Item2 <= outputEdge)) continue;
                            if (occupied.Contains(next) && next != targetCell && !depots.Any(x => x.Q == next.Item1 && x.R == next.Item2)) continue;
                            previous[next] = cell;
                            if (next == targetCell) { found = true; break; }
                            queue.Enqueue(next);
                        }
                }
                if (!found) throw new InvalidOperationException("Keine direkte Depotverbindung für " + RecipeCatalog.Name(target.Product));
                var path = targetCell;
                while (!connected.Contains(path))
                {
                    connected.Add(path);
                    if (!occupied.Contains(path))
                    {
                        var bridge = new PlannedDepot { Product = target.Product, Q = path.Item1, R = path.Item2 };
                        result.Add(bridge); occupied.Add(path);
                    }
                    path = previous[path];
                }
            }
        }
    }
}
