using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace PlanBSkyscraperMod
{
    [BepInPlugin(Id, "Plan B Skyscraper Mod", "0.15.4")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "tony.planb.skyscrapers";
        internal static Plugin Instance;
        internal ConfigEntry<bool> Enabled;
        internal ConfigEntry<int> NewBuildingPercent, Seed, ReferencePercent, DomeKeepPercent;
        internal ConfigEntry<bool> SkylineEnabled;
        internal ConfigEntry<bool> OriginalsFollowSkyline;
        internal ConfigEntry<float> SkylineRadius;
        internal VariantFactory Factory;
        internal GlbFactory ImportedFactory;
        internal OriginalTowerFactory OriginalFactory;
        internal bool SelectionFailed;
        private Harmony harmony;

        private void Awake()
        {
            Instance = this;
            Enabled = Config.Bind("General", "Enabled", true, "Enable the additional skyscrapers. Restart after changes.");
            NewBuildingPercent = Config.Bind("Mix", "NewBuildingPercent", 65,
                new ConfigDescription("Percentage of residential building slots in advanced districts using new variants. Parks and utilities keep their native frequency.", new AcceptableValueRange<int>(0, 100)));
            Seed = Config.Bind("Mix", "Seed", 173,
                "Stable city layout seed. Keep unchanged to preserve the same buildings after reload.");
            SkylineEnabled = Config.Bind("Skyline", "Enabled", true,
                "Create multiple stable skyscraper clusters across each city, each fading to lower buildings outwards. Restart after changes.");
            SkylineRadius = Config.Bind("Skyline", "Radius", 12f,
                new ConfigDescription("Height falloff radius of each cluster in approximate hex spacings. Centers are about 2.5 radii apart. Smaller values create more compact clusters.", new AcceptableValueRange<float>(4f, 60f)));
            OriginalsFollowSkyline = Config.Bind("Skyline", "OriginalsFollowSkyline", true,
                "Apply the same calmer height bands to original skyscrapers. Their materials and geometry are retained; only their vertical scale changes.");
            ReferencePercent = Config.Bind("ReferenceAssets", "DetailSetPercent", 65, new ConfigDescription("Share of imported GLB designs among new tower slots. Default 65 mixes GLBs with the 25 updated designs. House slots always use the compact imported models.", new AcceptableValueRange<int>(0, 100)));
            ImportedFactory = new GlbFactory(transform, msg => Logger.LogInfo(msg), msg => Logger.LogWarning(msg));
            DomeKeepPercent = Config.Bind("ReferenceAssets", "DomeKeepPercent", 30, new ConfigDescription("Percentage of previously selected dome slots retaining a dome. Other dome slots retain the native house. Default 30 reduces domes by about 70 percent without increasing hall repetition.", new AcceptableValueRange<int>(0, 100)));
            Factory = new VariantFactory(transform, msg => Logger.LogInfo(msg), msg => Logger.LogWarning(msg));
            OriginalFactory = new OriginalTowerFactory(transform, msg => Logger.LogWarning(msg));
            harmony = new Harmony(Id);
            harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo("Skyscrapers 0.15.4 loaded: 25 procedural designs aligned with Detailset_b PBR materials, recessed crossbars and rooftop vents; mixed skyline.");
        }

        internal CAssetModel Select(CAssetModels models, CAssetModel original, int2 coords)
        {
            if (!Enabled.Value || original?.Asset == null) return original;
            string name = original.Asset.name;
            if (!VariantFactory.IsResidential(name))
                return original;
            var neighbors = new int2[7];
            int count = SSingleton<SWorld>.Inst.GetNeighborsCoords(coords, neighbors, contiguous_5_7: true);
            var preferred = new int[count];
            for (int i = 0; i < count; i++)
                preferred[i] = neighbors[i] == int2.negative ? -1 : VariantRules.Preferred(neighbors[i].x, neighbors[i].y, Seed.Value);
            int variant = VariantRules.Choose(coords.x, coords.y, Seed.Value, preferred);
            int? skylineBand = null;
            if (SkylineEnabled.Value)
            {
                var city = SSingleton<SGame>.Inst.GetCity(coords);
                if (city != null)
                {
                    var center = city.center;
                    Vector3 origin = GHexes.pos[center.x, center.y];
                    Vector3 delta = GHexes.pos[coords.x, coords.y] - origin;
                    Quaternion rotation = SSingleton<SWorld>.Inst.GetHexTransform(center).rot;
                    var centerNeighbors = new int2[7];
                    int centerCount = SSingleton<SWorld>.Inst.GetNeighborsCoords(center, centerNeighbors, contiguous_5_7: true);
                    float spacing = 0;
                    for (int n = 0; n < centerCount; n++)
                    {
                        var neighbor = centerNeighbors[n];
                        if (neighbor == int2.negative) continue;
                        spacing = Vector3.Distance(origin, GHexes.pos[neighbor.x, neighbor.y]);
                        if (spacing > .0001f) break;
                    }
                    if (spacing > .0001f)
                    {
                        int citySeed = unchecked((int)VariantRules.Hash(center.x, center.y, Seed.Value, 0xBE5466CFu));
                        int band = VariantRules.MultiSkylineBand(Vector3.Dot(delta, rotation * Vector3.right) / spacing,
                            Vector3.Dot(delta, rotation * Vector3.forward) / spacing, SkylineRadius.Value, citySeed);
                        skylineBand = band;
                        variant = VariantRules.ChooseSkyline(coords.x, coords.y, Seed.Value, band, preferred);
                    }
                }
            }
            if (!VariantRules.UseNew(coords.x, coords.y, Seed.Value, NewBuildingPercent.Value))
                return OriginalsFollowSkyline.Value && skylineBand.HasValue && name.Contains("_Tower")
                    ? OriginalFactory.Get(models, original, skylineBand.Value) ?? original : original;
            if (name.Contains("_House") || VariantRules.Hash(coords.x, coords.y, Seed.Value, 0xA51392B7u) % 100 < ReferencePercent.Value)
            {
                int motif = ReferenceDesign.Select(name.Contains("_Tower"), VariantRules.Hash(coords.x, coords.y, Seed.Value, 0x36EF7821u));
                if (motif==4 && !VariantRules.KeepDome(coords.x,coords.y,Seed.Value,DomeKeepPercent.Value)) return original;
                variant = VariantRules.Count + motif * 5 + (skylineBand ?? variant % 5);
            }
            float spacingToNeighbor = float.PositiveInfinity;
            for(int n=0;n<count;n++)
            {
                if(neighbors[n]==int2.negative)continue;
                float distance=Vector3.Distance(GHexes.pos[coords.x,coords.y],GHexes.pos[neighbors[n].x,neighbors[n].y]);
                if(distance>.001f)spacingToNeighbor=Mathf.Min(spacingToNeighbor,distance);
            }
            if(float.IsInfinity(spacingToNeighbor))return original;
            if(variant>=VariantRules.Count)
                return ImportedFactory.Get((variant-VariantRules.Count)/5,variant%5,spacingToNeighbor*.5f,original.Asset.layer) ?? original;
            return Factory.Get(models, variant, spacingToNeighbor*.5f) ?? original;
        }

        internal void ReportSelectionFailure(Exception ex)
        {
            SelectionFailed = true;
            Logger.LogWarning("Additional skyscrapers disabled for this session after selection failure; using native buildings. " + ex);
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
            // Pooled objects still reference the generated meshes until game shutdown.
            Instance = null;
        }
    }

    [HarmonyPatch(typeof(SCities), nameof(SCities.SpawnContentCityModels))]
    internal static class SpawnScope
    {
        internal sealed class Context { internal int2 Coords; }
        [ThreadStatic] internal static Context Current;

        private static void Prefix(int2 coords, out Context __state)
        {
            __state = Current;
            Current = SSingleton<SWorld>.Inst.GetContent(coords) is CItem_ContentCity
                ? new Context { Coords = coords } : null;
        }

        private static Exception Finalizer(Exception __exception, Context __state)
        {
            Current = __state;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(CAssetModels), nameof(CAssetModels.GetModelAt))]
    internal static class SelectModelPatch
    {
        private static void Postfix(CAssetModels __instance, ref CAssetModel __result)
        {
            var plugin = Plugin.Instance;
            if (SpawnScope.Current == null || plugin == null || plugin.SelectionFailed) return;
            // The native spawn method keeps footprint, district specialisation, rotation,
            // population logic and pooling. Only its visual model selection changes.
            try { __result = plugin.Select(__instance, __result, SpawnScope.Current.Coords); }
            catch (Exception ex) { plugin.ReportSelectionFailure(ex); }
        }
    }
}
