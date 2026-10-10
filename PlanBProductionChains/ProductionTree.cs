using System;
using System.Collections.Generic;

namespace PlanBProductionChains
{
    internal sealed class ProductionNode
    {
        public CItem Product;
        public ProductionRoute Route;
        public string Path;
        public float Amount;
        public int RouteIndex, Depth;
        public bool IsCycle, IsCollapsed;
        public float X;
        public readonly List<ProductionNode> Children = new List<ProductionNode>();
    }

    internal sealed class ProductionTree
    {
        public const float CardWidth = 180, CardHeight = 114;
        public const float Column = 196, Row = 140;
        public float Width, Height;
        public ProductionNode Root;
        private float nextLeaf;
        public static ProductionTree Build(CItem product, RecipeCatalog catalog, Dictionary<CItem, int> choices,
            HashSet<string> collapsed, Func<CItem, int> defaultRoute = null)
        {
            var tree = new ProductionTree();
            tree.Root = tree.Create(product, 0, "root", 0, catalog, choices, collapsed,
                new HashSet<CItem>(), defaultRoute ?? catalog.DefaultRoute);
            tree.Width = Math.Max(Column, tree.nextLeaf * Column);
            return tree;
        }

        private ProductionNode Create(CItem product, float amount, string path, int depth, RecipeCatalog catalog,
            Dictionary<CItem, int> choices, HashSet<string> collapsed, HashSet<CItem> ancestors, Func<CItem, int> choose)
        {
            var node = new ProductionNode { Product = product, Amount = amount, Path = path, Depth = depth,
                IsCycle = ancestors.Contains(product), IsCollapsed = collapsed.Contains(path) };
            Height = Math.Max(Height, (depth + 1) * Row);
            if (!node.IsCycle && depth < 20 && catalog.Routes.TryGetValue(product, out var routes) && routes.Count > 0)
            {
                if (!choices.TryGetValue(product, out var index) || index < 0 || index >= routes.Count) index = choose(product);
                node.RouteIndex = index;
                node.Route = routes[index];
                if (!node.IsCollapsed)
                {
                    ancestors.Add(product);
                    for (var i = 0; i < node.Route.Inputs.Count; i++)
                    {
                        var input = node.Route.Inputs[i];
                        node.Children.Add(Create(input.Item, input.Amount, path + "/" + i, depth + 1,
                            catalog, choices, collapsed, ancestors, choose));
                    }
                    ancestors.Remove(product);
                }
            }
            node.X = node.Children.Count == 0 ? nextLeaf++ * Column + Column / 2
                : (node.Children[0].X + node.Children[node.Children.Count - 1].X) / 2;
            return node;
        }
    }
}
