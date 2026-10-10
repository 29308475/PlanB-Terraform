using System.Collections.Generic;
using System.Linq;

namespace PlanBProductionChains
{
    internal static class BuildingRequirements
    {
        internal static bool NeedsOwnSite(CItem building) => building is CItem_ContentExtractor ||
            building is CItem_ContentIceExtractor || building is CItem_ContentGeyserExtractor ||
            building is CItem_ContentPumpingStation || building is CItem_ContentDroneExtractor;

        internal static bool SharesTerrain(CItem_Content first, CItem_Content second) =>
            (first.isBuildableInGround && second.isBuildableInGround) ||
            (first.isBuildableInWater && second.isBuildableInWater);

        internal static List<CItem> DepotGoods(ProductionRoute route)
        {
            var outputs = route.Building is CItem_ContentFactory factory && !factory.producesOutputContainers
                ? Enumerable.Empty<Ingredient>() : route.Outputs;
            return route.Inputs.Concat(outputs).Select(x => x.Item).Distinct().ToList();
        }

        internal static List<PlannedProductionBuilding> SharedPlan(ProductionNode root)
        {
            var building = (CItem_Content)root.Route.Building;
            return ChainBuildPlan.Balanced(root).Where(x =>
                (x.Product == root.Product && x.Route == root.Route) ||
                (!NeedsOwnSite(x.Route.Building) && SharesTerrain(building, (CItem_Content)x.Route.Building))).ToList();
        }
    }
}
