using System.Collections.Generic;
using System.Linq;

namespace PlanBProductionChains
{
    internal sealed class Ingredient
    {
        public CItem Item;
        public float Amount;
    }

    internal sealed class ProductionRoute
    {
        public CItem Building;
        public CRecipe Recipe;
        public List<Ingredient> Inputs = new List<Ingredient>();
        public List<Ingredient> Outputs = new List<Ingredient>();
        public string Note;
        public bool IsUnlocked => Recipe == null ? Building.IsItemUnlocked() : Recipe.IsUnlocked();
    }

    internal sealed class RecipeCatalog
    {
        public readonly Dictionary<CItem, List<ProductionRoute>> Routes = new Dictionary<CItem, List<ProductionRoute>>();
        public List<CItem> Products => Routes.Keys.OrderBy(Name).ToList();
        public static string Name(CItem item) => string.IsNullOrEmpty(item.localizedName) ? item.codeName : item.localizedName;

        public void Load()
        {
            Routes.Clear();
            if (GItems.items == null) return;
            foreach (var building in GItems.items)
            {
                if (building is CItem_ContentFactory factory && factory.recipes != null)
                    foreach (var recipe in factory.recipes)
                    {
                        if (recipe == null) continue;
                        var route = new ProductionRoute { Building = building, Recipe = recipe, Note = SourceNote(factory) };
                        route.Inputs.AddRange(recipe.inputs.Where(x => x.item != null && x.nb > 0)
                            .Select(x => new Ingredient { Item = x.item, Amount = x.nb }));
                        route.Outputs.AddRange(recipe.outputs.Where(x => x.item != null && x.nb > 0)
                            .Select(x => new Ingredient { Item = x.item, Amount = x.nb }));
                        Add(route);
                    }
                if (building is CItem_ContentExtractor extractor)
                    foreach (var mineral in GItems.items.OfType<CItem_GroundMineral>())
                    {
                        var route = new ProductionRoute { Building = extractor,
                            Note = extractor.isBuildableInWater ? "Abbau auf einem unter Wasser liegenden Mineralvorkommen." : "Abbau auf einem passenden Mineralvorkommen." };
                        route.Outputs.Add(new Ingredient { Item = mineral, Amount = 1 });
                        Add(route);
                    }
                if (building is CItem_ContentFarm farm && farm.itemOut != null)
                {
                    var route = new ProductionRoute { Building = farm,
                        Note = "Je bepflanztem Feld: Saatbedarf und Ernte. Ertrag hängt von Fruchtbarkeit und Bedingungen ab. Benötigtes Feld: " + (farm.itemField == null ? "nicht angegeben" : Name(farm.itemField)) };
                    if (farm.itemIn != null) route.Inputs.Add(new Ingredient { Item = farm.itemIn, Amount = farm.plantingCost });
                    route.Outputs.Add(new Ingredient { Item = farm.itemOut, Amount = farm.harvestReward });
                    Add(route);
                }
            }
        }

        private static string SourceNote(CItem_ContentFactory factory)
        {
            if (factory is CItem_ContentIceExtractor) return "Benötigt ein unterirdisches Eisvorkommen.";
            if (factory is CItem_ContentGeyserExtractor) return "Benötigt einen Geysir.";
            if (factory is CItem_ContentPumpingStation) return "Benötigt Wasser am Standort.";
            if (factory is CItem_ContentDroneExtractor) return "Benötigt passende Mineralvorkommen in Drohnenreichweite.";
            if (factory.isExtractor) return "Gewinnung hängt von den Umgebungsbedingungen am Standort ab.";
            return null;
        }

        private void Add(ProductionRoute route)
        {
            foreach (var output in route.Outputs)
            {
                if (!Routes.TryGetValue(output.Item, out var list)) Routes[output.Item] = list = new List<ProductionRoute>();
                if (!list.Any(x => x.Building == route.Building && x.Recipe == route.Recipe)) list.Add(route);
            }
        }

        public int DefaultRoute(CItem product)
        {
            var routes = Routes[product];
            // Prefer extraction for raw minerals and available non-recycling recipes elsewhere.
            var index = routes.FindIndex(x => x.IsUnlocked && x.Inputs.Count == 0);
            if (index < 0) index = routes.FindIndex(x => x.IsUnlocked && x.Building != GItems.itemFactoryRecycle && x.Building != GItems.itemDeconstructor);
            if (index < 0) index = routes.FindIndex(x => x.IsUnlocked);
            return index < 0 ? 0 : index;
        }

        public bool HasProductionAlternatives(CItem product)
        {
            return !(product is CItem_GroundMineral) && Routes.TryGetValue(product, out var routes)
                && routes.Count > 1 && routes.Any(route => route.Inputs.Count > 0);
        }
    }
}
