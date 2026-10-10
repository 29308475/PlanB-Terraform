using System;
using HarmonyLib;

namespace PlanBBahnbetrieb
{
    internal static class CargoContext
    {
        [ThreadStatic] internal static CDrone Drone;
        [ThreadStatic] internal static int Slot;
        internal static bool Own(int2 coords) => Drone != null && Drone.depotCoords == coords;
    }
    [HarmonyPatch(typeof(CDrone), nameof(CDrone.TryStartTaking))]
    internal static class StationDroneScope
    {
        private static void Prefix(CDrone __instance, CItem_ContentDepot ___depot, int ___droneDepotIndex, out CDrone __state)
        {
            __state = CargoContext.Drone;
            if (___depot is FreightStation) { CargoContext.Drone = __instance; CargoContext.Slot = ___droneDepotIndex % 4; }
        }
        private static Exception Finalizer(Exception __exception, CDrone __state)
        { CargoContext.Drone = __state; return __exception; }
    }
    [HarmonyPatch(typeof(CItem_ContentStock), nameof(CItem_ContentStock.GetStack))]
    internal static class StationDroneStackPatch
    {
        private static void Prefix(CItem_ContentStock __instance, int2 coords, ref int iStack)
        { if (__instance is FreightStation && iStack == 0 && CargoContext.Own(coords)) iStack = CargoContext.Slot; }
    }
    [HarmonyPatch(typeof(CDrone), nameof(CDrone.UpdateDepotPriority_02s))]
    internal static class StationDronePriorityPatch
    {
        private static bool Prefix(CDrone __instance, CItem_ContentDepot ___depot, int ___droneDepotIndex, ref float ___priorityRefreshWait)
        {
            if (!(___depot is FreightStation station)) return true;
            if (!__instance.CheckIsValid()) return false;
            ___priorityRefreshWait -= .2f;
            if (___priorityRefreshWait > 0) return false;
            CargoPriority.Refresh(station, __instance, ___droneDepotIndex % 4);
            ___priorityRefreshWait = 2f; return false;
        }
    }
    [HarmonyPatch(typeof(CItemData), nameof(CItemData.GetValue), new[] { typeof(int2) })]
    internal static class StationPriorityReadPatch
    {
        private static bool Prefix(CItemData __instance, int2 coords, ref int __result)
        {
            if (FreightStation.DepotSecondaryFields.Contains(__instance) && !ReferenceEquals(__instance, FreightStation.Item?.dataSecondarySupply) &&
                SSingleton<SWorld>.Inst.GetContent(coords) is FreightStation targetStation)
            { __result = targetStation.dataSecondarySupply.GetValue(coords); return false; }
            if (ReferenceEquals(__instance, FreightStation.Item?.dataSecondarySupply))
            {
                var target = SSingleton<SWorld>.Inst.GetContent(coords);
                if (!(target is FreightStation))
                { __result = target is CItem_ContentDepot depot ? depot.dataSecondarySupply.GetValue(coords) : 0; return false; }
            }
            if (!ReferenceEquals(__instance, FreightStation.Item?.dataPriority)) return true;
            var record = Plugin.Instance?.Registry.At(coords);
            if (record?.Root == null) return true;
            __result = record.Priorities[CargoContext.Own(coords) ? CargoContext.Slot : 0]; return false;
        }
    }
    [HarmonyPatch(typeof(CItemData), nameof(CItemData.SetValue), new[] { typeof(int2), typeof(int) })]
    internal static class StationPriorityWritePatch
    {
        private static bool Prefix(CItemData __instance, int2 coords, int value)
        {
            if (!ReferenceEquals(__instance, FreightStation.Item?.dataPriority)) return true;
            var record = Plugin.Instance?.Registry.At(coords);
            if (record?.Root == null) return true;
            record.Priorities[CargoContext.Own(coords) ? CargoContext.Slot : 0] = value; return false;
        }
    }
    [HarmonyPatch(typeof(CDrone), "SearchStack_GetScoreBase")]
    internal static class StationCargoScorePatch
    {
        private static bool Prefix(int2 coords, ref CStack st, ref float __result)
        {
            if (!st.IsDepot) return true;
            var record = Plugin.Instance?.Registry.At(coords);
            if (record?.Root == null || record.Root.Coords != coords) return true;
            var stacks = GHexes.stacks[coords.x,coords.y];
            for (var i = 0; i < stacks.stacks.Length; i++)
                if (stacks.stacks[i].item == st.item) { __result = record.Priorities[i]; return false; }
            return true;
        }
    }
    [HarmonyPatch(typeof(SScenePopup), "OnClickItem")]
    internal static class StationCargoPickerPatch
    {
        private static bool Prefix(CUiItem uiItem)
        {
            var hud = Plugin.Instance?.Hud;
            if (hud == null || hud.PendingSlot < 0) return true;
            hud.Choose(uiItem.item); return false;
        }
    }
    [HarmonyPatch(typeof(SScenePopup), nameof(SScenePopup.ClosePopup))]
    internal static class StationPickerCancelPatch
    { private static void Postfix() { if (Plugin.Instance != null) Plugin.Instance.Hud.PendingSlot = -1; } }
}
