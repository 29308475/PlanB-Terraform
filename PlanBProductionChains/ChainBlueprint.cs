using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PlanBProductionChains
{
    internal sealed class ChainBlueprint
    {
        public readonly List<SelectedContent> Items = new List<SelectedContent>();
        public int SeparateExtractors;
        public bool VariableYields;
        public string SeparateStages;
        private int validatedFrame = -1;
        private bool valid;
        private string validationMessage;
        private int errorFrame = -1;
        private double layoutScore;

        public static ChainBlueprint Create(ProductionNode root, bool includeDepots)
        {
            ChainBlueprint best = null;
            InvalidOperationException failure = null;
            foreach (var shape in new[] { 2, 1, 0 })
            {
                try
                {
                    var proposal = CreateLayout(root, includeDepots, shape);
                    if (best == null || proposal.layoutScore < best.layoutScore) best = proposal;
                }
                catch (InvalidOperationException error) { failure = error; }
            }
            return best ?? throw failure ?? new InvalidOperationException("Keine gültige Baugeometrie gefunden.");
        }

        private static ChainBlueprint CreateLayout(ProductionNode root, bool includeDepots, int shape)
        {
            var result = new ChainBlueprint();
            var completePlan = ChainBuildPlan.Balanced(root);
            var plan = BuildingRequirements.SharedPlan(root);
            result.SeparateExtractors = completePlan.Count - plan.Count;
            result.SeparateStages = string.Join(", ", completePlan.Where(x => !plan.Any(p => p.Product == x.Product && p.Route == x.Route))
                .Select(x => RecipeCatalog.Name(x.Product) + " × " + x.Count));
            result.VariableYields = completePlan.Any(x => x.Route.Building is CItem_ContentFarm);
            var needsWideRange = plan.Any(x => ((CItem_Content)x.Route.Building).is7Hex ||
                (x.Route.Building is CItem_ContentFarm farm && farm.itemField != null && farm.itemField.is7Hex));
            var depotDistance = needsWideRange ? 2 : 1;
            var maxGoods = plan.Max(x => BuildingRequirements.DepotGoods(x.Route).Count);
            var requiredRange = Math.Max(depotDistance, (maxGoods - 1) / 2);
            var availableDepots = GItems.items.OfType<CItem_ContentDepot>()
                .Where(x => x.IsItemUnlocked() && x.range >= requiredRange && !(x is CItem_ContentDepotCreative))
                .OrderBy(x => x.range).ToList();
            var depot = includeDepots ? availableDepots.FirstOrDefault() : null;
            if (includeDepots && depot == null) throw new InvalidOperationException("Für diese Baugruppe wird ein freigeschaltetes Depot mit Reichweite " + requiredRange + " benötigt.");
            var occupied = new HashSet<(int, int)>();
            var depotRequests = new List<DepotRequest>();
            var neighbors = new[] { new int2(-1, 0), new int2(0, -1), new int2(1, -1), new int2(1, 0), new int2(0, 1), new int2(-1, 1) };
            var modules = plan.SelectMany(stage => Enumerable.Repeat(stage, stage.Count)).ToList();
            var columns = (int)Math.Ceiling(Math.Sqrt(modules.Count * Math.Max(1, shape)));
            var width = columns * (needsWideRange ? 3 : 1);
            for (var i = 0; i < modules.Count; i++)
            {
                var stage = modules[i];
                if (!stage.Route.IsUnlocked || !stage.Route.Building.IsItemUnlocked()) throw new InvalidOperationException("Herstellungsweg noch gesperrt: " + RecipeCatalog.Name(stage.Product));
                var building = (CItem_Content)stage.Route.Building;
                var goods = depot == null ? new List<CItem>() : BuildingRequirements.DepotGoods(stage.Route);
                var fieldOffset = building.is7Hex ? 2 : 1;
                if (building is CItem_ContentFarm footprintFarm && footprintFarm.itemField != null && footprintFarm.itemField.is7Hex) fieldOffset++;
                if (building is CItem_ContentFarm rangeFarm && rangeFarm.itemField != null &&
                    (fieldOffset > rangeFarm.rangeFields || fieldOffset > rangeFarm.itemField.farmRange))
                    throw new InvalidOperationException("Das Feld liegt außerhalb der Reichweite von " + RecipeCatalog.Name(building));
                var footprint = new HashSet<(int, int)> { (0, 0) };
                if (building.is7Hex) foreach (var offset in neighbors) footprint.Add((offset.x, offset.y));
                if (building is CItem_ContentFarm fieldFarm && fieldFarm.itemField != null)
                {
                    footprint.Add((fieldOffset, 0));
                    if (fieldFarm.itemField is CItem_Content field && field.is7Hex)
                        foreach (var offset in neighbors) footprint.Add((fieldOffset + offset.x, offset.y));
                }
                var origin = shape == 0 ? HexModuleLayout.Place(footprint, occupied, neighbors) : HexModuleLayout.PlaceCompact(footprint, occupied, neighbors, width);
                var q = origin.x; var r = origin.y;
                uint data = 0;
                if (building is CItem_ContentFactory factory && stage.Route.Recipe != null)
                {
                    var recipe = Array.IndexOf(factory.recipes, stage.Route.Recipe);
                    if (recipe < 0) throw new InvalidOperationException("Rezept nicht mehr verfügbar.");
                    factory.dataRecipe.SetValue(ref data, recipe);
                }
                CStacks productionStacks = null;
                if (building is CItem_ContentExtractor extractor)
                { productionStacks = new CStacks(extractor); productionStacks.stacks[0].item = stage.Product; }
                result.Items.Add(new SelectedContent(building.id, data, SMisc.CubeToCoords(q, r), productionStacks, 0));
                if (building is CItem_ContentFarm farm && farm.itemField != null)
                    result.Items.Add(new SelectedContent(farm.itemField.id, 0, SMisc.CubeToCoords(q + fieldOffset, r), null, 0));
                if (depot != null)
                {
                    foreach (var input in stage.Route.Inputs.Select(x => x.Item).Distinct())
                        depotRequests.Add(new DepotRequest { Product = input, Q = q, R = r, Side = -1 });
                    if (!(building is CItem_ContentFactory outputFactory) || outputFactory.producesOutputContainers)
                        foreach (var output in stage.Route.Outputs.Select(x => x.Item).Distinct())
                            depotRequests.Add(new DepotRequest { Product = output, Q = q, R = r, Side = 1 });
                }
            }
            if (depot != null)
            {
                List<PlannedDepot> placements = null;
                InvalidOperationException failure = null;
                foreach (var candidate in availableDepots)
                {
                    try
                    {
                        var proposed = DepotNetworkLayout.Create(depotRequests, occupied, candidate.range, depotDistance);
                        if (placements == null || proposed.Count < placements.Count)
                        { placements = proposed; depot = candidate; }
                    }
                    catch (InvalidOperationException error) { failure = error; }
                }
                if (placements == null) throw failure ?? new InvalidOperationException("Keine verbundene Depotanordnung möglich.");
                foreach (var placement in placements)
                {
                    var stacks = new CStacks(depot); stacks.stacks[0].item = placement.Product;
                    result.Items.Add(new SelectedContent(depot.id, 0, SMisc.CubeToCoords(placement.Q, placement.R), stacks, 0));
                    occupied.Add((placement.Q, placement.R));
                }
            }
            var depotRows = result.Items.Where(x => x.item is CItem_ContentDepot).Select(x =>
            {
                var cell = SMisc.CoordsToCube(x.relativeCoords);
                return new PlannedDepot { Q = cell.q, R = cell.r };
            }).ToList();
            var holes = depotRows.GroupBy(x => x.R).Sum(row => row.Max(x => x.Q) - row.Min(x => x.Q) + 1 - row.Count());
            result.layoutScore = HexModuleLayout.Compactness(occupied) + holes * 12 + result.Items.Count * .2;
            return result;
        }

        public bool MatchesCurrentSelection()
        {
            if (GScene3D.actionMode != GScene3D.ActionModes.Duplicate) return false;
            var selected = SSingleton<SSelection>.Inst.selection;
            return selected.Count == Items.Count && selected.Count > 0 && ReferenceEquals(selected[0], Items[0]);
        }

        public void Begin()
        {
            var selection = SSingleton<SSelection>.Inst;
            SSceneSingleton<SSceneHud_ItemsBars>.Inst.UnselectItemAndDeleteButtonIFN();
            selection.UnselectAll();
            selection.selection.AddRange(Items);
            GScene3D.actionMode = GScene3D.ActionModes.Duplicate;
            SSceneSingleton<SScene3D>.Inst.RefreshMouseOver();
        }

        public bool CanBuild()
        {
            // Native placement calls this once per item; cache the complete check before
            // the first building changes inventory during the same click/frame.
            if (validatedFrame == Time.frameCount) return valid;
            validatedFrame = Time.frameCount;
            validationMessage = null;
            var needs = Items.GroupBy(x => x.item).ToDictionary(x => x.Key, x => x.Count());
            foreach (var need in needs)
                if (!GGame.AllFree && !need.Key.isFree && need.Key.nbOwned < need.Value)
                    return Fail("Nicht genügend " + RecipeCatalog.Name(need.Key) + ": " + need.Key.nbOwned + "/" + need.Value);
            foreach (var item in Items)
            {
                var coords = SSingleton<SSelection>.Inst.GetCoordsTranslated(item.relativeCoords, GScene3D.mouseoverCoords);
                if (coords.Negative || !GHexes.IsHex(coords) || !SSingleton<SWorld>.Inst.IsOutsideOfPoles(coords))
                    return Fail("Das Baupaket liegt außerhalb des bebaubaren Bereichs.");
                if (SSingleton<SWorld>.Inst.GetContent(coords) != null)
                    return Fail("Für das Baupaket wird eine freie Fläche benötigt.");
                var building = (CItem_Content)item.item;
                if (building.is7Hex)
                {
                    var neighbors = new int2[7];
                    var count = SSingleton<SWorld>.Inst.GetNeighborsCoords(coords, neighbors, contiguous_5_7: true);
                    for (var i = 0; i < count; i++)
                        if (!SSingleton<SWorld>.Inst.IsOutsideOfPoles(neighbors[i]) || SSingleton<SWorld>.Inst.GetContent(neighbors[i]) != null)
                            return Fail(RecipeCatalog.Name(building) + ": Die gesamte Gebäudefläche muss frei sein.");
                }
                if (building is CItem_ContentExtractor && SSingleton<SWorld>.Inst.GetGround(coords) != FindExtractorProduct(item))
                    return Fail("Ein Extraktor benötigt das passende Mineralvorkommen.");
                if (building is CItem_ContentExtractor && SSingleton<SPlanet>.Inst.IsIce(coords))
                    return Fail(RecipeCatalog.Name(building) + ": Das Mineralvorkommen liegt unter Eis.");
                if (!building.IsBuildable(coords))
                    return Fail(RecipeCatalog.Name(building) + ": " + (building.GetBuildFailedInfosMessage(coords) ?? "Standort ungeeignet."));
                if (building is CItem_ContentField field)
                {
                    var fertility = field.GetFertility_YearlyData(coords);
                    if (fertility.fertility <= 0) return Fail(RecipeCatalog.Name(field) + ": Keine Ernte möglich. " + fertility.fertilityMainReason);
                }
            }
            valid = true; return true;
        }

        private CItem FindExtractorProduct(SelectedContent item)
        {
            // The extractor's output is stored explicitly in its blueprint stack.
            return item.stacks != null && item.stacks.stacks.Length > 0 ? item.stacks.stacks[0].item : null;
        }
        private bool Fail(string message) { validationMessage = message; valid = false; return false; }
        public void ShowError()
        {
            if (errorFrame == Time.frameCount) return;
            errorFrame = Time.frameCount;
            SSceneSingleton<SSceneUIOverlay>.Inst.ShowMessage(validationMessage ?? "Baupaket kann hier nicht platziert werden.");
        }
    }
}
