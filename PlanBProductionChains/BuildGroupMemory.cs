using System;
using System.Linq;
using System.Text;

namespace PlanBProductionChains
{
    internal static class BuildGroupMemory
    {
        internal static string Capture(ProductionNode root) => string.Join("\n", ChainBuildPlan.Collect(root)
            .Select(x => string.Join("\t", new[] { x.Product.codeName, x.Route.Building.codeName, x.Route.Recipe?.GetRecipeHash() ?? "" }
                .Select(value => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))))));

        internal static ProductionNode Restore(string saved, RecipeCatalog catalog)
        {
            if (string.IsNullOrEmpty(saved)) throw new InvalidOperationException("Noch keine Baugruppe ausgewählt.");
            var nodes = saved.Split('\n').Select(line =>
            {
                var values = line.Split('\t').Select(value => Encoding.UTF8.GetString(Convert.FromBase64String(value))).ToArray();
                if (values.Length != 3) throw new InvalidOperationException("Gespeicherte Baugruppe ist ungültig.");
                var product = catalog.Routes.Keys.FirstOrDefault(x => x.codeName == values[0]);
                var route = product == null ? null : catalog.Routes[product].FirstOrDefault(x =>
                    x.Building.codeName == values[1] && (x.Recipe?.GetRecipeHash() ?? "") == values[2]);
                if (route == null) throw new InvalidOperationException("Herstellungsweg der letzten Baugruppe nicht verfügbar: " + values[0]);
                return new ProductionNode { Product = product, Route = route };
            }).ToList();
            var root = nodes.Last();
            var choices = nodes.ToDictionary(x => x.Product, x => catalog.Routes[x.Product].IndexOf(x.Route));
            return ProductionTree.Build(root.Product, catalog, choices, new System.Collections.Generic.HashSet<string>()).Root;
        }
    }
}
