using System;
using System.Collections.Generic;

namespace PlanBProductionChains
{
    internal sealed class PlannedProductionBuilding
    {
        public CItem Product;
        public ProductionRoute Route;
        public int Count = 1;
    }

    internal static class ChainBuildPlan
    {
        public static List<PlannedProductionBuilding> Collect(ProductionNode root)
        {
            var result = new List<PlannedProductionBuilding>();
            Collect(root, result);
            return result;
        }
        internal static List<PlannedProductionBuilding> Balanced(ProductionNode root)
        {
            var result = Collect(root);
            var loads = new Dictionary<PlannedProductionBuilding, double>();
            AddLoad(root, 1, result, loads);
            foreach (var stage in result)
            {
                var count = Math.Ceiling(loads.TryGetValue(stage, out var load) ? load - 1e-7 : 1);
                if (count > 4096) throw new InvalidOperationException("Die berechnete Baugruppe ist zu groß.");
                stage.Count = Math.Max(1, (int)count);
            }
            return result;
        }
        private static void AddLoad(ProductionNode node, double load, List<PlannedProductionBuilding> plan,
            Dictionary<PlannedProductionBuilding, double> loads)
        {
            if (node.IsCycle || node.Route == null) return;
            var stage = plan.Find(x => x.Product == node.Product && x.Route.Building == node.Route.Building && x.Route.Recipe == node.Route.Recipe);
            loads[stage] = (loads.TryGetValue(stage, out var existing) ? existing : 0) + load;
            foreach (var child in node.Children)
            {
                if (child.IsCycle || child.Route == null) continue;
                double childLoad = 1;
                var input = node.Route.Inputs.Find(x => x.Item == child.Product);
                var output = child.Route.Outputs.Find(x => x.Item == child.Product);
                if (input != null && output != null && output.Amount > 0 &&
                    node.Route.Recipe?.duration > 0 && child.Route.Recipe?.duration > 0)
                    childLoad = load * input.Amount / node.Route.Recipe.duration * child.Route.Recipe.duration / output.Amount;
                AddLoad(child, childLoad, plan, loads);
            }
        }
        private static void Collect(ProductionNode node, List<PlannedProductionBuilding> result)
        {
            if (node.IsCycle) return;
            if (node.Route == null) throw new InvalidOperationException("Keine Herstellungsquelle für " + RecipeCatalog.Name(node.Product));
            foreach (var child in node.Children) Collect(child, result);
            if (!(node.Route.Building is CItem_Content)) throw new InvalidOperationException("Gebäude nicht platzierbar: " + RecipeCatalog.Name(node.Route.Building));
            // Identical stages shared by multiple branches need only one module in the blueprint.
            foreach (var existing in result)
                if (existing.Route.Building == node.Route.Building && existing.Route.Recipe == node.Route.Recipe && existing.Product == node.Product) return;
            result.Add(new PlannedProductionBuilding { Product = node.Product, Route = node.Route });
        }
    }
}
