using System;
using System.Collections.Generic;
using System.Collections;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Roadlife
{
    [BepInPlugin("tony.planb.roadlife", "Stadtleben", "0.4.21")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private static Plugin instance;
        private Harmony harmony;
        private ConfigEntry<bool> trafficEnabled;
        private ConfigEntry<int> maximum;
        private ConfigEntry<float> density, speed, height, lane;
        private ConfigEntry<float> growth, densityCap, cityRadius, truckSlowdown;
        private ConfigEntry<bool> affectTrucks;
        private ConfigEntry<bool> growingRoads;
        private ConfigEntry<bool> replaceBridges;
        private ConfigEntry<int> widenPopulation, narrowPopulation;
        private ConfigEntry<float> visualMargin, wideHalfWidth;
        private readonly Dictionary<CCity, bool> cityUpgrades = new Dictionary<CCity, bool>();
        private readonly Dictionary<int2, int> roadStyles = new Dictionary<int2, int>();
        private readonly BoundedCache<int2, RoadVisuals.Geometry> roadGeometry = new BoundedCache<int2, RoadVisuals.Geometry>(2048);
        private readonly BoundedCache<int2, bool> roadErrors = new BoundedCache<int2, bool>(128);
        private static readonly Color CityPavement = new Color(.38f, .40f, .43f).linear;
        private readonly List<CityTraffic> cities = new List<CityTraffic>();
        private readonly List<float> roadDensities = new List<float>();
        private float roadDensityTotal, nextCityRefresh;
        private readonly List<CFxVehicleModel> models = new List<CFxVehicleModel>();
        private readonly List<Car> cars = new List<Car>();
        private readonly List<int2> roads = new List<int2>();
        private readonly int2[] neighbors = new int2[7];
        private readonly int2[] changedNeighbors = new int2[7];
        private readonly List<int2> choices = new List<int2>();
        private float nextScan;
        private bool failed;
        private int bridgeDiagnostics;
        private readonly Queue<int2> bridgeRefresh = new Queue<int2>();
        private readonly HashSet<int2> bridgeRefreshSet = new HashSet<int2>();
        private bool refreshingBridgeModels;
        private IEnumerator scanWork;
        private readonly Dictionary<int2, float> truckFactors = new Dictionary<int2, float>();
        private readonly Dictionary<int2, bool> wideRoads = new Dictionary<int2, bool>();

        private sealed class CityTraffic
        {
            public Vector3 center;
            public double population;
            public CCity city;
            public float radius;
            public bool wide;
        }

        private sealed class Car
        {
            public int2 previous, from, to;
            public float progress, speed;
            public CFxVehicleModel model;
            public bool outerLane;
        }

        private void Awake()
        {
            instance = this;
            trafficEnabled = Config.Bind("Traffic", "Enabled", true, "Dekorativen Straßenverkehr aktivieren.");
            maximum = Config.Bind("Traffic", "MaximumVehicles", 150, new ConfigDescription("Maximale Fahrzeugzahl.", new AcceptableValueRange<int>(0, 500)));
            density = Config.Bind("Traffic", "VehiclesPerRoadTile", .15f, new ConfigDescription("Fahrzeuge pro sichtbarem Straßenfeld.", new AcceptableValueRange<float>(0f, 1f)));
            growth = Config.Bind("Traffic", "CityGrowth", .75f, new ConfigDescription("Langsames logarithmisches Verkehrswachstum mit der Stadtbevölkerung.", new AcceptableValueRange<float>(0f, 2f)));
            densityCap = Config.Bind("Traffic", "MaximumDensity", .4375f, new ConfigDescription("Obergrenze der Fahrzeuge pro Straßenfeld, auch in Großstädten.", new AcceptableValueRange<float>(.01f, 1f)));
            cityRadius = Config.Bind("Traffic", "CityInfluenceRadius", 30f, new ConfigDescription("Reichweite einer Stadt in Weltkoordinaten; Einfluss nimmt mit Entfernung ab.", new AcceptableValueRange<float>(1f, 200f)));
            affectTrucks = Config.Bind("Trucks", "AffectedByTraffic", true, "LKW-Geschwindigkeit durch örtliche Verkehrsdichte begrenzen, unabhängig von Kamera und FX.");
            truckSlowdown = Config.Bind("Trucks", "MaximumSlowdown", .25f, new ConfigDescription("Maximale Verlangsamung bei maximaler Dichte (0,25 = 25 Prozent).", new AcceptableValueRange<float>(0f, .5f)));
            growingRoads = Config.Bind("Roads", "GrowWithCity", true, "Straßen nahe größeren Städten auf vier sichtbare Spuren erweitern.");
            replaceBridges = Config.Bind("Bridges", "ReplaceExisting", true, "Vorhandene Straßenbrücken automatisch mit Roadlife-Deck, Geländern und Pfeilern darstellen.");
            widenPopulation = Config.Bind("Roads", "UpgradePopulation", 10000, new ConfigDescription("Einwohnerzahl für vier Fahrspuren.", new AcceptableValueRange<int>(100, 10000000)));
            narrowPopulation = Config.Bind("Roads", "DowngradePopulation", 8000, new ConfigDescription("Rückbau unter dieser Einwohnerzahl; höchstens UpgradePopulation.", new AcceptableValueRange<int>(0, 10000000)));
            visualMargin = Config.Bind("Roads", "CityRoadMargin", 6f, new ConfigDescription("Zusätzliche Reichweite außerhalb der geschätzten Stadtfläche in Weltkoordinaten.", new AcceptableValueRange<float>(0f, 30f)));
            wideHalfWidth = Config.Bind("Roads", "FourLaneHalfWidth", .24f, new ConfigDescription("Halbe Breite der vierspurigen Straßenfläche.", new AcceptableValueRange<float>(.16f, .32f)));
            speed = Config.Bind("Traffic", "TilesPerSecond", .35f, new ConfigDescription("Fahrgeschwindigkeit in Straßenfeldern pro Sekunde.", new AcceptableValueRange<float>(.01f, 2f)));
            height = Config.Bind("Appearance", "Height", .02f, new ConfigDescription("Abstand über der Straßenhöhe einschließlich Brücken.", new AcceptableValueRange<float>(0f, 1f)));
            lane = Config.Bind("Appearance", "LaneOffset", .04f, new ConfigDescription("Seitlicher Fahrspurabstand in Weltkoordinaten.", new AcceptableValueRange<float>(0f, .5f)));
            harmony = new Harmony("tony.planb.roadlife");
            harmony.PatchAll(typeof(Plugin));
            Logger.LogInfo("Stadtleben 0.4.21 geladen: verbreiterte Bahnübergänge mit getrennten Straßen- und Gleisanschlüssen.");
        }

        [HarmonyPatch(typeof(SWays_Generation), "GenerateProceduralWayMesh"), HarmonyPostfix]
        private static void GrowingRoadMesh(SWays_Generation __instance, CItem_Way itemWay, Transform parentTr, List<CPoolElementId> hexModels, int2 coords, Mesh mesh, byte? neiHasWay)
        {
            var mod = instance;
            if (mod == null || mod.failed || neiHasWay.HasValue || (itemWay.wayTypes & 1u) == 0) return;
            if (mod.roadErrors.TryGet(coords, out _)) return;
            try
            {
                mod.RefreshCities();
                var city = mod.RoadCity(coords);
                bool bridge = mod.replaceBridges.Value && itemWay.ComputeBridgeHeight(coords) > 0f;
                bool approach = mod.replaceBridges.Value && !bridge && BridgeSpan.HasBridgeNeighbor(coords);
                if (city == null && !bridge && !approach) return;
                if (!mod.roadGeometry.TryGet(coords, out var geometry))
                {
                    Mesh rails = null;
                    try
                    {
                        if ((itemWay.wayTypes & 2u) != 0)
                        {
                            // Native mixed ways otherwise connect rails to road-only neighbors.
                            var neighbors = new int2[7];
                            var world = SSingleton<SWorld>.Inst;
                            int count = world.GetNeighborsCoords(coords, neighbors, contiguous_5_7: false);
                            byte mask = 0;
                            for (int i = 0; i < count; i++)
                                if (WorldCoords.IsHex(neighbors[i]) && world.GetContent(neighbors[i]) is CItem_Way adjacent
                                    && (adjacent.wayTypes & 2u) != 0) mask |= (byte)(1 << i);
                            rails = new Mesh();
                            __instance.GenerateProceduralWayMesh(itemWay, parentTr, hexModels, coords, rails, mask);
                        }
                        geometry = RoadVisuals.Build(coords, itemWay, city == null ? .13f : mod.wideHalfWidth.Value,
                            CityPavement, mod.IsWide, rails ?? (itemWay is CItem_WayStop ? mesh : null), mod.replaceBridges.Value);
                    }
                    finally { if (rails != null) UnityEngine.Object.Destroy(rails); }
                    mod.roadGeometry.Put(coords, geometry);
                }
                geometry.Apply(mesh);
                if (bridge && mod.bridgeDiagnostics < 3)
                {
                    mod.bridgeDiagnostics++;
                    mod.Logger.LogInfo("Brückenmesh " + coords + ": Tragwerk=" + geometry.hasBridgeStructure
                        + ", Vertices=" + geometry.vertices.Length + ", Deckhöhe=" + itemWay.ComputeBridgeHeight(coords));
                }
            }
            catch (Exception error)
            {
                mod.roadGeometry.Remove(coords); mod.roadErrors.Put(coords, true);
                mod.Logger.LogError("Straßenmesh " + coords + " bleibt vorübergehend nativ; andere Straßen bleiben aktiv. " + error);
            }
        }

        [HarmonyPatch(typeof(SViewArea), "OnChangeHex"), HarmonyPrefix]
        private static void RoadChanged(int2 c, SViewArea.FlagsUpdate flags)
        {
            if (instance == null || (flags & (SViewArea.FlagsUpdate.Models | SViewArea.FlagsUpdate.Vertices)) == 0) return;
            if (instance.refreshingBridgeModels) return;
            instance.roadGeometry.Remove(c); instance.wideRoads.Remove(c);
            instance.roadErrors.Remove(c);
            BridgeSpan.Invalidate(c, instance.BridgeTileChanged);
            var neis = instance.changedNeighbors;
            int count = SSingleton<SWorld>.Inst.GetNeighborsCoords(c, neis, contiguous_5_7: true);
            for (int i = 0; i < count; i++)
            { instance.roadGeometry.Remove(neis[i]); instance.wideRoads.Remove(neis[i]); instance.roadErrors.Remove(neis[i]); BridgeSpan.Invalidate(neis[i], instance.BridgeTileChanged); }
        }

        private void BridgeTileChanged(int2 coords)
        {
            roadGeometry.Remove(coords);
            roadErrors.Remove(coords);
            if (bridgeRefreshSet.Add(coords)) bridgeRefresh.Enqueue(coords);
        }

        private void RefreshBridgeModels()
        {
            var view = SSingleton<SViewArea>.Inst;
            int budget = 2;
            while (budget-- > 0 && bridgeRefresh.Count > 0)
            {
                int2 coords = bridgeRefresh.Dequeue(); bridgeRefreshSet.Remove(coords);
                if (!WorldCoords.IsHex(coords) || !view.IsVisible(coords, false)) continue;
                roadGeometry.Remove(coords);
                refreshingBridgeModels = true;
                try { view.OnChangeHex(coords, SViewArea.FlagsUpdate.Models); }
                finally { refreshingBridgeModels = false; }
            }
        }

        private CityTraffic RoadCity(int2 coords)
        {
            if (!growingRoads.Value) return null;
            Vector3 position = SSingleton<SWorld>.Inst.GetPosAbove(coords);
            CityTraffic nearest = null;
            float distance = float.MaxValue;
            foreach (var city in cities)
            {
                if (!city.wide) continue;
                float current = Vector3.Distance(position, city.center);
                if (current <= city.radius && current < distance) { nearest = city; distance = current; }
            }
            return nearest;
        }

        private bool IsWide(int2 coords)
        {
            if (wideRoads.TryGetValue(coords, out bool cached)) return cached;
            bool wide = SSingleton<SWorld>.Inst.GetContent(coords) is CItem_Way way
                && !(way is CItem_WayStop) && (way.wayTypes & 1u) != 0 && RoadCity(coords) != null;
            wideRoads[coords] = wide;
            return wide;
        }

        private float LaneOffset(int2 coords, bool outer)
        {
            return IsWide(coords) ? wideHalfWidth.Value * (outer ? .75f : .25f) : lane.Value;
        }

        // Apply the cap at the original acceleration step, after the game's stop,
        // loading and spacing decisions. No shared vehicle definitions are changed.
        [HarmonyPatch(typeof(CVehicle), "Update"), HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> TruckSpeedPatch(IEnumerable<CodeInstruction> instructions)
        {
            var moveTowards = AccessTools.Method(typeof(Mathf), "MoveTowards", new[] { typeof(float), typeof(float), typeof(float) });
            var replacement = AccessTools.Method(typeof(Plugin), nameof(TrafficMoveTowards));
            int matches = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(moveTowards))
                {
                    matches++;
                    var vehicle = new CodeInstruction(OpCodes.Ldarg_0);
                    vehicle.labels.AddRange(instruction.labels);
                    vehicle.blocks.AddRange(instruction.blocks);
                    yield return vehicle;
                    yield return new CodeInstruction(OpCodes.Call, replacement);
                }
                else yield return instruction;
            }
            if (matches != 1) throw new InvalidOperationException("Roadlife: unerwartete CVehicle.Update-Struktur; LKW-Patch nicht kompatibel.");
        }

        private static float TrafficMoveTowards(float current, float target, float delta, CVehicle vehicle)
        {
            var mod = instance;
            if (mod != null && !mod.failed && mod.trafficEnabled.Value && mod.affectTrucks.Value
                && vehicle?.line != null && (vehicle.ItemVehicle.wayType & (uint)CItem_Way.Type.Road) != 0)
            {
                try
                {
                    mod.RefreshCities();
                    var coords = vehicle.line.GetCoord(vehicle.t);
                    if (mod.IsRoad(coords))
                    {
                        if (!mod.truckFactors.TryGetValue(coords, out float factor))
                        {
                            factor = TrafficRules.TruckFactor(mod.LocalDensity(coords), mod.densityCap.Value, mod.truckSlowdown.Value);
                            mod.truckFactors[coords] = factor;
                        }
                        target = Mathf.Min(target, vehicle.ItemVehicle.speedMax * factor);
                    }
                }
                catch (Exception error)
                {
                    mod.failed = true;
                    mod.Logger.LogError(error);
                }
            }
            return Mathf.MoveTowards(current, target, delta);
        }

        private void RefreshCities()
        {
            if (Time.unscaledTime < nextCityRefresh) return;
            cities.Clear();
            truckFactors.Clear(); wideRoads.Clear();
            foreach (var city in GGame.cities)
                if (city != null && WorldCoords.IsHex(city.center))
                {
                    cityUpgrades.TryGetValue(city, out bool previous);
                    bool wide = TrafficRules.WideRoad(previous, city.population, widenPopulation.Value, narrowPopulation.Value);
                    cityUpgrades[city] = wide;
                    cities.Add(new CityTraffic { center = SSingleton<SWorld>.Inst.GetPosAbove(city.center), population = city.population,
                        city = city, wide = wide, radius = Mathf.Sqrt(city.hexes.Count) * .65f + visualMargin.Value });
                }
            nextCityRefresh = Time.unscaledTime + 2f;
        }

        private float LocalDensity(int2 coords)
        {
            Vector3 position = SSingleton<SWorld>.Inst.GetPosAbove(coords);
            float value = TrafficRules.Density(density.Value, 0, 0, growth.Value, densityCap.Value);
            foreach (var city in cities)
            {
                float proximity = Mathf.Clamp01(1f - Vector3.Distance(position, city.center) / cityRadius.Value);
                value = Mathf.Max(value, TrafficRules.Density(density.Value, city.population, proximity, growth.Value, densityCap.Value));
            }
            return value;
        }

        private Vector3 RoadPosition(int2 coords)
        {
            var world = SSingleton<SWorld>.Inst;
            var way = (CItem_Way)world.GetContent(coords);
            if (replaceBridges.Value) return BridgeSpan.Position(coords, height.Value);
            return world.GetPosAbove(coords, way.ComputeBridgeHeight(coords) + height.Value);
        }

        [HarmonyPatch(typeof(CVehicle), "GetPos"), HarmonyPostfix]
        private static void BridgeTruckPosition(CVehicle __instance, float t, ref Vector3 __result)
        {
            var mod = instance;
            if (mod == null || mod.failed || !mod.replaceBridges.Value || __instance.ItemVehicle.wayType != 1u) return;
            int2 coords = __instance.GetCoord(t);
            if (!WorldCoords.IsHex(coords)) return;
            float clearance = .014f + __instance.ItemVehicle.altitudeAboveWay;
            if (BridgeSpan.IsBridge(coords))
                __result = __result.normalized * BridgeSpan.Position(coords, clearance).magnitude;
            else if (mod.roadGeometry.TryGet(coords, out var geometry) && geometry.surface != null)
                __result = geometry.surface.Project(__result, clearance);
        }

        [HarmonyPatch(typeof(SEnvironmentFXVehicles), "Reset"), HarmonyPostfix]
        private static void WorldReset() { instance?.Clear(); }

        [HarmonyPatch(typeof(SSceneHud), "OnDeactivate"), HarmonyPostfix]
        private static void WorldClosed() { instance?.Clear(); }

        [HarmonyPatch(typeof(SEnvironmentFXVehicles), "Update"), HarmonyPostfix]
        private static void DrawTraffic()
        {
            if (instance == null || instance.failed) return;
            try { instance.Tick(); }
            catch (Exception error)
            {
                instance.failed = true;
                instance.cars.Clear();
                instance.Logger.LogError(error);
            }
        }

        private void Clear()
        {
            cars.Clear(); roads.Clear(); models.Clear(); cities.Clear(); roadDensities.Clear();
            cityUpgrades.Clear(); roadStyles.Clear();
            roadGeometry.Clear();
            roadErrors.Clear();
            bridgeDiagnostics = 0;
            NativeRoadTerrain.Clear();
            BridgeSpan.Clear();
            bridgeRefresh.Clear(); bridgeRefreshSet.Clear(); refreshingBridgeModels = false;
            (scanWork as IDisposable)?.Dispose(); scanWork = null;
            truckFactors.Clear(); wideRoads.Clear();
            nextScan = 0; nextCityRefresh = 0; roadDensityTotal = 0; failed = false;
        }

        private bool IsRoad(int2 coords)
        {
            return WorldCoords.IsHex(coords) && SSingleton<SWorld>.Inst.GetContent(coords) is CItem_Way way
                && (way.wayTypes & (uint)CItem_Way.Type.Road) != 0;
        }

        private bool ChooseNext(int2 current, int2 previous, out int2 next)
        {
            choices.Clear();
            int count = SSingleton<SWorld>.Inst.GetNeighborsCoords(current, neighbors);
            for (int i = 0; i < count; i++)
                if (neighbors[i] != previous && IsRoad(neighbors[i])) choices.Add(neighbors[i]);
            if (choices.Count > 0) { next = choices[UnityEngine.Random.Range(0, choices.Count)]; return true; }
            next = previous;
            return IsRoad(previous);
        }

        private bool IsTrafficOrigin(int2 coords)
        {
            if (!IsRoad(coords)) return false;
            var way = (CItem_Way)SSingleton<SWorld>.Inst.GetContent(coords);
            return way.ComputeBridgeHeight(coords) <= 0f
                && SSingleton<SPlanet>.Inst.GetWaterAboveIce(coords) <= GWater.waterDepthMin;
        }

        private IEnumerator Scan()
        {
            var collectedRoads = new List<int2>();
            var collectedDensities = new List<float>();
            float collectedTotal = 0;
            var slice = System.Diagnostics.Stopwatch.StartNew();
            RefreshCities();
            var view = SSingleton<SViewArea>.Inst;
            var min = view.YMin;
            var max = view.YMax;
            if (min == null || max == null) yield break;
            min = (int[])min.Clone(); max = (int[])max.Clone();
            int visited = 0, changes = 0;
            for (int x = 0; x < min.Length; x++)
                for (int y = min[x]; y < max[x]; y++)
                {
                    var coords = new int2(x, y);
                    if (IsRoad(coords) && view.IsVisible(coords, false))
                    {
                        collectedRoads.Add(coords);
                        var visualCity = RoadCity(coords);
                        var roadItem = (CItem_Way)SSingleton<SWorld>.Inst.GetContent(coords);
                        int style = visualCity != null ? visualCity.city.cityId + 1 : 0;
                        if (replaceBridges.Value && roadItem.ComputeBridgeHeight(coords) > 0f && (roadItem.wayTypes & 2u) == 0)
                            style = style == 0 ? -1 : style;
                        else if (replaceBridges.Value && style == 0 && (roadItem.wayTypes & 2u) == 0 && BridgeSpan.HasBridgeNeighbor(coords))
                            style = -2;
                        bool knownStyle = roadStyles.TryGetValue(coords, out int previousStyle);
                        bool knownGeometry = roadGeometry.TryGet(coords, out var geometry);
                        bool terrainReady = style != 0 && knownGeometry
                            && geometry.surface != null && !geometry.surface.UsesTerrain && NativeRoadTerrain.Available(coords);
                        // The spawn hook may already have applied the desired appearance.
                        // Do not immediately discard and regenerate it in the first scan.
                        if ((knownStyle && style != previousStyle) || (!knownStyle && style != 0 && !knownGeometry) || terrainReady)
                        {
                            SSingleton<SViewArea>.Inst.OnChangeHex(coords, SViewArea.FlagsUpdate.Models);
                            changes++;
                        }
                        roadStyles[coords] = style;
                        collectedTotal += LocalDensity(coords);
                        collectedDensities.Add(collectedTotal);
                    }
                    visited++;
                    if (changes >= 8 || (visited % 32 == 0 && slice.Elapsed.TotalMilliseconds >= 2))
                    { yield return null; changes = 0; slice.Restart(); }
                }
            roads.Clear(); roads.AddRange(collectedRoads);
            roadDensities.Clear(); roadDensities.AddRange(collectedDensities); roadDensityTotal = collectedTotal;
            // Keep known styles while off-screen so returning does not trigger a rebuild.
            nextScan = Time.unscaledTime + 3f;
        }

        private void Tick()
        {
            RefreshBridgeModels();
            // Street appearance is independent of the optional vehicle FX.
            bool closeView = SSingleton<SCamera>.Inst.GetZoom() < GCamera.Zoom.Intermediary;
            if (closeView)
            {
                if (scanWork == null && Time.unscaledTime >= nextScan) scanWork = Scan();
                if (scanWork != null && !scanWork.MoveNext()) { (scanWork as IDisposable)?.Dispose(); scanWork = null; }
            }
            else if (scanWork != null)
            { (scanWork as IDisposable)?.Dispose(); scanWork = null; nextScan = 0; }
            if (!trafficEnabled.Value || !GOptions.itemsFx.value || SSingleton<SCamera>.Inst.GetZoom() >= GCamera.Zoom.Intermediary)
            { cars.Clear(); return; }
            if (models.Count == 0 && GEnvironmentFX.vehiclesAssetModel != null)
                foreach (var entry in GEnvironmentFX.vehiclesAssetModel.models)
                {
                    var asset = entry.Asset;
                    if (asset != null && asset.TryGetComponent<MeshFilter>(out var filter)
                        && asset.TryGetComponent<MeshRenderer>(out var renderer) && filter.sharedMesh != null
                        && renderer.sharedMaterials.Length >= filter.sharedMesh.subMeshCount)
                        models.Add(new CFxVehicleModel { model = asset, mesh = filter.sharedMesh, materials = renderer.sharedMaterials });
                }
            if (models.Count == 0) return;
            int target = Mathf.Min(maximum.Value, Mathf.CeilToInt(roadDensityTotal));
            if (cars.Count > target) cars.RemoveRange(target, cars.Count - target);
            for (int attempts = 0; cars.Count < target && roads.Count > 0 && attempts < 16; attempts++)
            {
                // Weighted spawning concentrates traffic near larger cities.
                float sample = UnityEngine.Random.value * roadDensityTotal;
                int index = roadDensities.BinarySearch(sample);
                if (index < 0) index = ~index;
                var start = roads[Mathf.Min(index, roads.Count - 1)];
                // Roads are sampled for density and visuals, including bridges.
                // Only dry land may introduce a new vehicle into that network.
                if (!IsTrafficOrigin(start)) continue;
                if (!ChooseNext(start, int2.negative, out var next)) continue;
                ChooseNext(start, next, out var previous);
                // The rendered curve spans both neighboring half-tiles. Keep
                // those halves on land too; existing cars may still drive across.
                if (!IsTrafficOrigin(next) || !IsTrafficOrigin(previous)) continue;
                cars.Add(new Car { previous = previous, from = start, to = next,
                    progress = UnityEngine.Random.value, speed = UnityEngine.Random.Range(.8f, 1.2f),
                    outerLane = UnityEngine.Random.value > .5f,
                    model = models[UnityEngine.Random.Range(0, models.Count)] });
            }
            for (int i = cars.Count - 1; i >= 0; i--)
            {
                var car = cars[i];
                if (!IsRoad(car.from) || !IsRoad(car.to) || !SSingleton<SViewArea>.Inst.IsVisible(car.from, false))
                { cars.RemoveAt(i); continue; }
                car.progress += Mathf.Min(Time.deltaTime, .1f) * speed.Value * car.speed;
                if (car.progress >= 1f)
                {
                    if (!ChooseNext(car.to, car.from, out var next)) { cars.RemoveAt(i); continue; }
                    car.previous = car.from; car.from = car.to; car.to = next; car.progress -= 1f;
                }
                Vector3 center = RoadPosition(car.from);
                Vector3 nextPosition = RoadPosition(car.to);
                Vector3 previousPosition = IsRoad(car.previous) ? RoadPosition(car.previous) : center;
                Vector3 a = RoadVisuals.Join(previousPosition, center);
                Vector3 b = RoadVisuals.Join(center, nextPosition);
                if (car.previous == car.to) a = center;
                Vector3 position = RoadVisuals.Curve(a, center, b, car.progress);
                Vector3 forward = Vector3.Lerp(center - a, b - center, car.progress).normalized;
                if (forward.sqrMagnitude < .001f) continue;
                Vector3 up = position.normalized;
                float startOffset = Mathf.Min(LaneOffset(car.from, car.outerLane), IsRoad(car.previous) ? LaneOffset(car.previous, car.outerLane) : lane.Value);
                float endOffset = Mathf.Min(LaneOffset(car.from, car.outerLane), LaneOffset(car.to, car.outerLane));
                float offset = Mathf.Lerp(Mathf.Lerp(startOffset, endOffset, car.progress), LaneOffset(car.from, car.outerLane), Mathf.Sin(car.progress * Mathf.PI));
                position += Vector3.Cross(up, forward).normalized * offset;
                if (roadGeometry.TryGet(car.from, out var groundGeometry) && groundGeometry.surface != null)
                    position = groundGeometry.surface.Project(position, height.Value);
                Quaternion rotation = Quaternion.LookRotation(forward, up);
                for (int sub = 0; sub < car.model.mesh.subMeshCount; sub++)
                    Graphics.DrawMesh(car.model.mesh, position, rotation, car.model.materials[sub], 0, null, sub);
            }
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf(); Clear(); if (instance == this) instance = null;
        }
    }
}
