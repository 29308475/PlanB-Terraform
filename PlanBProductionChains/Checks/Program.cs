using PlanBProductionChains;

static class Program
{
    static void Main()
    {
        Console.WriteLine("Starting catalog checks.");
        System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var path = Path.Combine(@"C:\Program Files (x86)\Steam\steamapps\common\Plan B Terraform\Plan B Terraform_Data\Managed", name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
        try { Run(); }
        catch (Exception error) { Console.Error.WriteLine(error.ToString()); Environment.ExitCode = 1; }
    }

    static void Run()
    {
        Console.WriteLine("Creating recipe fixtures.");
        // Bypass constructors that allocate Unity native objects (e.g. mineral gradients).
        var iron = Fixture<CItem_GroundMineral>("iron", "Eisen");
        var steel = Fixture<CItem_Intermediary>("steel", "Stahl");
        var waste = Fixture<CItem_Intermediary>("waste", "Abfall");
        var normal = new CRecipe();
        normal.inputs.Add(new CRecipe.CRecipeElement { item = iron, nb = 3 });
        normal.outputs.Add(new CRecipe.CRecipeElement { item = steel, nb = 1 });
        var recycling = new CRecipe();
        recycling.inputs.Add(new CRecipe.CRecipeElement { item = waste, nb = 1 });
        recycling.outputs.Add(new CRecipe.CRecipeElement { item = steel, nb = 2 });
        recycling.outputs.Add(new CRecipe.CRecipeElement { item = iron, nb = 1 });
        var factory = Fixture<CItem_ContentFactory>("factory", "Fabrik"); factory.recipes = new[] { normal, normal, recycling };
        var extractor = Fixture<CItem_ContentExtractor>("extractor", "Extraktor");
        var farm = Fixture<CItem_ContentFarm>("farm", "Farm");
        farm.itemIn = waste; farm.itemOut = steel; farm.plantingCost = .05f; farm.harvestReward = .3f;
        GItems.items = new List<CItem> { iron, steel, waste, factory, extractor, farm };
        var catalog = new RecipeCatalog(); catalog.Load();
        Require(catalog.Routes[steel].Count == 3, "Distinct alternative routes and farming");
        Require(catalog.Routes[steel][0].Inputs.Single().Amount == 3, "Actual ingredient quantity");
        Require(catalog.Routes[steel][1].Outputs.Count == 2, "Co-products retained");
        Require(catalog.Routes[iron].Any(x => x.Building == extractor && x.Inputs.Count == 0), "Mineral extraction source");
        Require(catalog.Routes[steel].Any(x => x.Building == farm && Math.Abs(x.Inputs[0].Amount - .05f) < .001), "Fractional planting input");
        Require(RecipeCatalog.Name(iron) == "Eisen", "Localized product name");
        Require(!catalog.HasProductionAlternatives(iron), "Raw minerals omit route selector despite extraction and recycling routes");
        Require(catalog.HasProductionAlternatives(steel), "Manufactured products retain alternative recipes");
        var choices = new Dictionary<CItem, int>(); var collapsed = new HashSet<string>();
        var tree = ProductionTree.Build(steel, catalog, choices, collapsed, _ => 0);
        Require(tree.Root.Children.Single().Product == iron && tree.Root.Children[0].Amount == 3, "Tree links correct ingredient and amount");
        choices[steel] = 1;
        tree = ProductionTree.Build(steel, catalog, choices, collapsed, _ => 0);
        Require(tree.Root.Children.Single().Product == waste, "Alternative route rebuilds dependency tree");
        var wasteRoute = new ProductionRoute { Building = factory };
        wasteRoute.Inputs.Add(new Ingredient { Item = steel, Amount = 1 });
        catalog.Routes[waste] = new List<ProductionRoute> { wasteRoute };
        tree = ProductionTree.Build(steel, catalog, choices, collapsed, _ => 0);
        Require(tree.Root.Children[0].Children[0].IsCycle && tree.Root.Children[0].Children[0].Children.Count == 0, "Recycling cycle terminates visibly");
        collapsed.Add("root/0");
        tree = ProductionTree.Build(steel, catalog, choices, collapsed, _ => 0);
        Require(tree.Root.Children[0].IsCollapsed && tree.Root.Children[0].Children.Count == 0, "Collapsing prunes only that branch");
        collapsed.Clear(); choices[steel] = 0;
        normal.inputs.Add(new CRecipe.CRecipeElement { item = waste, nb = 2 });
        catalog.Load(); catalog.Routes[waste] = new List<ProductionRoute> { new ProductionRoute { Building = factory } };
        tree = ProductionTree.Build(steel, catalog, choices, collapsed, _ => 0);
        Require(tree.Root.Children.Count == 2 && tree.Root.Children[1].X - tree.Root.Children[0].X >= ProductionTree.Column, "Sibling cards cannot overlap");
        Require(Math.Abs(tree.Root.X - (tree.Root.Children[0].X + tree.Root.Children[1].X) / 2) < .001, "Parent centered over branch");
        choices[iron] = catalog.Routes[iron].FindIndex(x => x.Building == extractor);
        tree = ProductionTree.Build(steel, catalog, choices, collapsed, _ => 0);
        var plan = ChainBuildPlan.Collect(tree.Root);
        Require(plan.Count == 3 && plan.Last().Product == steel, "Build includes upstream stages before selected product");
        Require(ChainBuildPlan.Collect(tree.Root.Children[0]).Count == 1, "Building a subnode excludes downstream stages");
        var shared = new ProductionNode { Product = steel, Route = catalog.Routes[steel][0] };
        shared.Children.Add(tree.Root.Children[0]); shared.Children.Add(tree.Root.Children[0]);
        Require(ChainBuildPlan.Collect(shared).Count == 2, "Shared production stage is built once");
        shared.Children.Add(new ProductionNode { Product = steel, IsCycle = true });
        Require(ChainBuildPlan.Collect(shared).Count == 2, "Cyclic terminal is not duplicated in build package");
        var unknownRejected = false;
        try { ChainBuildPlan.Collect(new ProductionNode { Product = steel }); }
        catch (InvalidOperationException) { unknownRejected = true; }
        Require(unknownRejected, "Unknown production source rejects incomplete blueprint");
        var saved = BuildGroupMemory.Capture(tree.Root);
        var restored = BuildGroupMemory.Restore(saved, catalog);
        Require(restored.Product == steel && restored.Route == tree.Root.Route, "Last build restores selected end product and recipe");
        Require(restored.Children[0].Route.Building == extractor && restored.Children.Count == 2, "Last build restores upstream choices and dependency tree");
        Require(BuildGroupMemory.Capture(restored) == saved, "Repeated builds retain stable stage identities");
        var badSnapshotRejected = false;
        try { BuildGroupMemory.Restore("invalid snapshot", catalog); }
        catch (Exception) { badSnapshotRejected = true; }
        Require(badSnapshotRejected, "Invalid persisted group cannot place an unrelated recipe");
        factory.isBuildableInGround = true;
        extractor.isBuildableInGround = true;
        Require(BuildingRequirements.NeedsOwnSite(extractor), "Mineral extraction needs independent site");
        Require(BuildingRequirements.NeedsOwnSite(Fixture<CItem_ContentPumpingStation>("pump", "Pumpe")) &&
            BuildingRequirements.NeedsOwnSite(Fixture<CItem_ContentIceExtractor>("ice", "Eis")) &&
            BuildingRequirements.NeedsOwnSite(Fixture<CItem_ContentGeyserExtractor>("geyser", "Geysir")) &&
            BuildingRequirements.NeedsOwnSite(Fixture<CItem_ContentDroneExtractor>("drone", "Drohne")), "Water ice geyser and drone sources require independent sites");
        Require(BuildingRequirements.SharedPlan(tree.Root).All(x => x.Route.Building != extractor), "Factory group excludes remote extraction sites");
        Require(BuildingRequirements.SharedPlan(tree.Root.Children[0]).Single().Route.Building == extractor, "Raw node still builds its own extraction module");
        var waterFactory = Fixture<CItem_ContentFactory>("waterFactory", "Wasserfabrik"); waterFactory.isBuildableInWater = true;
        Require(!BuildingRequirements.SharesTerrain(factory, waterFactory), "Land-only and water-only buildings cannot share a group");
        var goodsCount = BuildingRequirements.DepotGoods(catalog.Routes[steel][0]).Count;
        Require(goodsCount == 2, "Inventory outputs have no unnecessary output depot");
        factory.producesOutputContainers = true;
        Require(BuildingRequirements.DepotGoods(catalog.Routes[steel][0]).Count == 3, "Container outputs receive their own depot");
        var fastRecipe = new CRecipe { duration = 5 };
        var slowRecipe = new CRecipe { duration = 10 };
        var sourceRoute = new ProductionRoute { Building = factory, Recipe = slowRecipe };
        sourceRoute.Outputs.Add(new Ingredient { Item = iron, Amount = 2 });
        var targetRoute = new ProductionRoute { Building = factory, Recipe = fastRecipe };
        targetRoute.Inputs.Add(new Ingredient { Item = iron, Amount = 3 });
        targetRoute.Outputs.Add(new Ingredient { Item = steel, Amount = 1 });
        var balancedRoot = new ProductionNode { Product = steel, Route = targetRoute };
        balancedRoot.Children.Add(new ProductionNode { Product = iron, Route = sourceRoute });
        var balanced = ChainBuildPlan.Balanced(balancedRoot);
        Require(balanced.Single(x => x.Product == steel).Count == 1 && balanced.Single(x => x.Product == iron).Count == 3, "Three slower supply factories sustain one faster consuming factory");
        slowRecipe.duration = 1;
        Require(ChainBuildPlan.Balanced(balancedRoot).Single(x => x.Product == iron).Count == 1, "Fractional factory demand rounds up to one");
        slowRecipe.duration = 10; targetRoute.Inputs[0].Amount = 1;
        balancedRoot.Children.Add(new ProductionNode { Product = iron, Route = sourceRoute });
        Require(ChainBuildPlan.Balanced(balancedRoot).Single(x => x.Product == iron).Count == 2, "Shared supply demand accumulates across branches");
        GItems.items = null; catalog.Load(); Require(catalog.Routes.Count == 0, "No stale recipes after reload");
        var hud = typeof(SSceneHud);
        Require(hud.GetMethod("OnActivate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null, "Installed HUD activation hook");
        Require(hud.GetMethod("OnDeactivate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null, "Installed HUD deactivation hook");
        Require(hud.GetField("btTechConditionInfos")?.FieldType == typeof(UnityEngine.UI.Button), "Top-center info button anchor");
        var neighbors = new[] { new int2(-1, 0), new int2(0, -1), new int2(1, -1), new int2(1, 0), new int2(0, 1), new int2(-1, 1) };
        var occupied = new HashSet<(int, int)>();
        for (var i = 0; i < 20; i++)
        {
            var footprint = new HashSet<(int, int)> { (0, 0) };
            foreach (var offset in neighbors.Take(i % 6 + 1)) footprint.Add((offset.x, offset.y));
            if (i % 3 == 0) foreach (var offset in neighbors) footprint.Add((offset.x * 2, offset.y * 2));
            var before = new HashSet<(int, int)>(occupied);
            var origin = HexModuleLayout.Place(footprint, occupied, neighbors);
            var placed = footprint.Select(x => (x.Item1 + origin.x, x.Item2 + origin.y)).ToList();
            Require(occupied.Count == before.Count + footprint.Count, "Packed modules never overlap");
            Require(i == 0 || placed.Any(x => neighbors.Any(n => before.Contains((x.Item1 + n.x, x.Item2 + n.y)))), "Every module touches existing group without an empty hex");
        }
        for (var distance = 1; distance <= 2; distance++)
            for (var count = 1; count <= 6; count++)
            {
                occupied.Clear();
                var depotCells = new List<(int, int)>();
                for (var module = 0; module < 8; module++)
                {
                    var footprint = new HashSet<(int, int)> { (0, 0) };
                    if (distance == 2 && module % 2 == 0)
                        foreach (var offset in neighbors) footprint.Add((offset.x, offset.y));
                    var offsets = HexModuleLayout.DepotOffsets(count, distance);
                    foreach (var offset in offsets) Require(footprint.Add((offset.x, offset.y)), "Depots do not overlap factory footprint");
                    var before = new HashSet<(int, int)>(occupied);
                    var origin = HexModuleLayout.Place(footprint, occupied, neighbors);
                    Require(origin.y == 0, "All production centers share a straight axis");
                    Require(occupied.Count == before.Count + footprint.Count, "Rail-facing modules never overlap");
                    Require(module == 0 || footprint.Any(x => neighbors.Any(n => before.Contains((x.Item1 + origin.x + n.x, x.Item2 + n.y)))), "Rail-facing groups remain directly adjacent");
                    depotCells.AddRange(offsets.Select(x => (x.x + origin.x, x.y)));
                }
                Require(depotCells.All(x => !occupied.Contains((x.Item1, x.Item2 + Math.Sign(x.Item2)))), "Every depot has an unobstructed exterior rail frontage");
                Require(occupied.All(x => Math.Abs(x.Item2) <= distance), "Both continuous exterior rail corridors remain clear");
            }
        var carbon = Fixture<CItem_Intermediary>("carbon", "Kohlenstoff");
        var aluminium = Fixture<CItem_Intermediary>("aluminium", "Aluminiumstab");
        var networkBuildings = new HashSet<(int, int)> { (0, 0), (1, 0), (2, 0) };
        var requests = new List<DepotRequest>
        {
            new DepotRequest { Product = aluminium, Q = 0 },
            new DepotRequest { Product = aluminium, Q = 2 },
            new DepotRequest { Product = carbon, Q = 1 },
            new DepotRequest { Product = carbon, Q = 2 }
        };
        var network = DepotNetworkLayout.Create(requests, networkBuildings, 1, 1);
        Require(network.Count(x => x.Product == carbon) == 1, "Neighboring carbon consumers share one depot");
        CheckDepotNetwork(network, requests, networkBuildings, 1, 1);
        for (var radius = 1; radius <= 3; radius++)
        {
            networkBuildings.Clear(); requests.Clear();
            for (var factoryIndex = 0; factoryIndex < 12; factoryIndex++)
            {
                var q = factoryIndex * (radius == 1 ? 1 : 3);
                networkBuildings.Add((q, 0));
                if (radius > 1) foreach (var n in neighbors) networkBuildings.Add((q + n.x, n.y));
                requests.Add(new DepotRequest { Product = aluminium, Q = q });
                requests.Add(new DepotRequest { Product = carbon, Q = q });
            }
            network = DepotNetworkLayout.Create(requests, networkBuildings, radius, radius == 1 ? 1 : 2);
            CheckDepotNetwork(network, requests, networkBuildings, radius, radius == 1 ? 1 : 2);
        }
        var sixGoods = Enumerable.Range(0, 6).Select(i => Fixture<CItem_Intermediary>("good" + i, "Ware " + i)).ToList();
        for (var frontage = 1; frontage <= 2; frontage++)
        {
            networkBuildings.Clear(); requests.Clear();
            for (var i = 0; i < 8; i++)
            {
                var q = i * (frontage == 1 ? 1 : 3);
                networkBuildings.Add((q, 0));
                if (frontage == 2) foreach (var n in neighbors) networkBuildings.Add((q + n.x, n.y));
                foreach (var good in sixGoods) requests.Add(new DepotRequest { Product = good, Q = q, R = 0 });
            }
            network = DepotNetworkLayout.Create(requests, networkBuildings, 3, frontage);
            CheckDepotNetwork(network, requests, networkBuildings, 3, frontage);
        }
        networkBuildings = new HashSet<(int, int)> { (0, 0), (1, 0), (2, 0) };
        requests = new List<DepotRequest>
        {
            new DepotRequest { Product = aluminium, Q = 0, R = 0, Side = 1 },
            new DepotRequest { Product = aluminium, Q = 2, R = 0, Side = -1 },
            new DepotRequest { Product = carbon, Q = 1, R = 0, Side = -1 },
            new DepotRequest { Product = carbon, Q = 2, R = 0, Side = -1 }
        };
        network = DepotNetworkLayout.Create(requests, networkBuildings, 2, 1);
        CheckDepotNetwork(network, requests, networkBuildings, 2, 2);
        Require(network.Count(x => x.Product == carbon) == 1 && network.Single(x => x.Product == carbon).R < 0, "Carbon consumers share an input-side depot");
        Require(network.Any(x => x.Product == aluminium && x.R > 0) && network.Any(x => x.Product == aluminium && x.R < 0), "Intermediate product has separate connected output and input frontages");
        networkBuildings.Clear(); requests.Clear();
        for (var i = 0; i < 8; i++)
        {
            var q = i * 3;
            networkBuildings.Add((q, 0));
            foreach (var n in neighbors) networkBuildings.Add((q + n.x, n.y));
            foreach (var good in sixGoods) requests.Add(new DepotRequest { Product = good, Q = q, R = 0, Side = -1 });
        }
        network = DepotNetworkLayout.Create(requests, networkBuildings, 3, 2);
        CheckDepotNetwork(network, requests, networkBuildings, 3, 3);
        Require(network.All(x => x.R < 0), "Dense input network stays entirely on the input side");
        networkBuildings = Enumerable.Range(0, 8).Select(q => (q, 0)).ToHashSet();
        requests = new List<DepotRequest>();
        for (var q = 0; q < 4; q++)
        {
            requests.Add(new DepotRequest { Product = steel, Q = q, R = 0, Side = 1 });
            requests.Add(new DepotRequest { Product = carbon, Q = q, R = 0, Side = -1 });
        }
        requests.Add(new DepotRequest { Product = steel, Q = 7, R = 0, Side = -1 });
        requests.Add(new DepotRequest { Product = aluminium, Q = 4, R = 0, Side = 1 });
        requests.Add(new DepotRequest { Product = aluminium, Q = 6, R = 0, Side = -1 });
        var smallerDepots = DepotNetworkLayout.Create(requests, networkBuildings, 2, 1);
        var largerDepots = DepotNetworkLayout.Create(requests, networkBuildings, 3, 1);
        CheckDepotNetwork(largerDepots, requests, networkBuildings, 3, 3);
        Require(largerDepots.Count < smallerDepots.Count, "Higher depot reach reduces total depots for repeated steel production");
        Require(largerDepots.Count(x => x.Product == carbon) == 1, "Four close carbon-consuming factories share one depot");
        Require(largerDepots.Count(x => x.Product == aluminium) == 2, "Intermediate needs only a connected input and output depot when in reach");
        var block = new HashSet<(int, int)>();
        var positions = new List<int2>();
        for (var i = 0; i < 8; i++)
        {
            var before = new HashSet<(int, int)>(block);
            var origin = HexModuleLayout.PlaceCompact(new HashSet<(int, int)> { (0, 0) }, block, neighbors, 4);
            Require(block.Count == before.Count + 1, "Compact production rows never overlap");
            Require(i == 0 || neighbors.Any(n => before.Contains((origin.x + n.x, origin.y + n.y))), "Compact production block remains contiguous");
            positions.Add(origin);
        }
        Require(block.Max(x => x.Item1) == 3 && block.Max(x => x.Item2) == 1, "Eight factories form a four by two block");
        var blockRequests = requests.Select(x => new DepotRequest
        { Product = x.Product, Q = positions[x.Q].x, R = positions[x.Q].y, Side = x.Side }).ToList();
        var blockDepots = DepotNetworkLayout.Create(blockRequests, block, 3, 1);
        CheckDepotNetwork(blockDepots, blockRequests, block, 3, 4);
        Require(blockRequests.Where(x => x.Side < 0).All(x => blockDepots.Any(d => d.Product == x.Product && d.R < 0 && DepotNetworkLayout.Serves(d, x, 3))), "All compact block inputs face the same exterior side");
        Require(blockRequests.Where(x => x.Side > 0).All(x => blockDepots.Any(d => d.Product == x.Product && d.R > 1 && DepotNetworkLayout.Serves(d, x, 3))), "All compact block outputs face the opposite exterior side");
        var stripCells = new HashSet<(int, int)>(networkBuildings);
        foreach (var d in largerDepots) stripCells.Add((d.Q, d.R));
        var blockCells = new HashSet<(int, int)>(block);
        foreach (var d in blockDepots) blockCells.Add((d.Q, d.R));
        Require(HexModuleLayout.Compactness(blockCells) < HexModuleLayout.Compactness(stripCells), "Complete block including connected depots is more compact than long strip");
        var regularRequests = new List<DepotRequest>();
        for (var i = 0; i < 4; i++)
        {
            regularRequests.Add(new DepotRequest { Product = sixGoods[i], Q = i, R = 0, Side = -1 });
            regularRequests.Add(new DepotRequest { Product = sixGoods[i], Q = i, R = 1, Side = 1 });
        }
        var regularDepots = DepotNetworkLayout.Create(regularRequests, block, 3, 1);
        CheckDepotNetwork(regularDepots, regularRequests, block, 3, 4);
        Require(regularDepots.Count == 8, "Regular rows retain only one input and output depot per distinct product");
        Require(regularDepots.GroupBy(x => x.R).All(row => row.Max(x => x.Q) - row.Min(x => x.Q) + 1 == row.Count()), "Feasible depot rows close gaps without extra depots");
        Require(regularDepots.Select(x => x.R).Distinct().Count() == 2, "Regular block keeps a single input row and a single output row");
        Console.WriteLine("Catalog, tree, memory, demand, connected depots, opposite frontages, compact blocks and regular gap-free depot rows passed. UI requires an in-game check.");
        Require(typeof(SSceneMods).GetMethod("RefreshModsUI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) != null,
            "Installed game exposes native Mods-menu refresh hook");
        Require(typeof(SSceneMods).GetField("modPrefab")?.FieldType == typeof(CUiMod) && typeof(SSceneMods).GetField("modsListParent")?.FieldType == typeof(UnityEngine.Transform),
            "Native Mods menu provides row prefab and list container");
        Require(typeof(CUiMod).GetField("toggle")?.FieldType == typeof(UnityEngine.UI.Toggle) && typeof(CUiMod).GetField("buttonFolder")?.FieldType == typeof(UnityEngine.UI.Button),
            "Native rows support explicit load status and folder action");
    }
    static void CheckDepotNetwork(List<PlannedDepot> depots, List<DepotRequest> requests, HashSet<(int, int)> buildings, int range, int frontage)
    {
        Require(depots.Select(x => (x.Q, x.R)).Distinct().Count() == depots.Count &&
            depots.All(x => !buildings.Contains((x.Q, x.R))), "Global depot placement has no collisions");
        Require(requests.All(x => depots.Any(d => d.Product == x.Product && DepotNetworkLayout.Serves(d, x, range))), "Every input and output reaches a matching depot on its intended side");
        Require(depots.All(x => Math.Abs(x.R) <= frontage), "Global network leaves both exterior rail corridors clear");
        foreach (var group in depots.GroupBy(x => x.Product))
        {
            var reached = new HashSet<PlannedDepot> { group.First() };
            bool added;
            do
            {
                added = false;
                foreach (var d in group)
                    if (!reached.Contains(d) && reached.Any(other => DepotNetworkLayout.Distance(d.Q, d.R, other.Q, other.R) <= range))
                    { reached.Add(d); added = true; }
            } while (added);
            Require(reached.Count == group.Count(), "Every same-product depot connects without a train or truck");
        }
    }
    static void Require(bool valid, string description)
    { if (!valid) throw new Exception(description); }
    static T Fixture<T>(string code, string name) where T : CItem
    {
        var item = (T)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(T));
        item.codeName = code; item.localizedName = name; return item;
    }
}
