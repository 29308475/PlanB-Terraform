using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace PlanBBahnbetrieb
{
    [BepInPlugin(Id, "Güterbahnhof", "0.2.5")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal const string Id = "tony.planb.freightstation";
        internal static Plugin Instance;
        internal readonly StationModel Model = new StationModel();
        internal readonly StationRegistry Registry = new StationRegistry();
        internal readonly StationPlacement Placement = new StationPlacement();
        internal readonly StationHud Hud = new StationHud();
        private Harmony harmony;
        private float nextRefresh;
        private bool wasPlaying;
        private float retryAfter;
        private void Awake()
        {
            Instance = this; harmony = new Harmony(Id); harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo("Güterbahnhof 0.2.5 geladen: zwei native Gleise, vier interne Warenpuffer, GLB-Bahnhofsmodell.");
        }
        private void LateUpdate()
        {
            if (Time.unscaledTime < retryAfter) return;
            try
            {
                if (!GGame.isPlaying)
                {
                    if (wasPlaying) { Hud.Hide(); foreach (var station in Registry.Stations) if (station.Model != null) station.Model.SetActive(false); }
                    wasPlaying = false; Placement.Tick(); return;
                }
                wasPlaying = true;
                Hud.Ensure(); Hud.Visible(!GGame.hideUI);
                Registry.LoadPending(); Placement.Tick();
                if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + .5f; Registry.Refresh(); Hud.Refresh(); }
            }
            catch (Exception error) { Error(error); retryAfter = Time.unscaledTime + 5; }
        }
        internal void ResetWorld()
        { Registry.Clear(); Placement.Reset(); Hud.Hide(); }
        internal void LogInfo(string text) => Logger.LogInfo(text);
        internal void Error(Exception error) => Logger.LogError(error);
        internal void Message(string text) => SSceneSingleton<SSceneUIOverlay>.Inst.ShowMessage(text);
        private void OnDestroy()
        {
            harmony?.UnpatchSelf(); Hud.Dispose(); Placement.Reset(); Registry.Clear(); Model.Dispose();
            if (Instance == this) Instance = null;
        }
    }

    [HarmonyPatch(typeof(SGame), nameof(SGame.Reset))]
    internal static class ResetPatch { private static void Prefix() => Plugin.Instance?.ResetWorld(); }

    [HarmonyPatch(typeof(CItem_Content), nameof(CItem_Content.IsBuildable))]
    internal static class ReserveFootprintPatch
    {
        private static bool Prefix(CItem_Content __instance, int2 coords, ref bool __result)
        {
            var registry = Plugin.Instance?.Registry;
            if (registry == null || !registry.IsReserved(coords)) return true;
            var content = SSingleton<SWorld>.Inst.GetContent(coords);
            if (registry.IsPart(coords) && ((content is CItem_ContentDepot && !(content is FreightStation) && __instance is CItem_ContentDepot) ||
                (content is CItem_Way && __instance is CItem_Way way && (way.wayTypes & 2) != 0))) return true;
            __result = false; return false;
        }
    }

    [HarmonyPatch(typeof(CItem_Way), nameof(CItem_Way.GetTrainStop_2Dirs))]
    internal static class StationStopDirectionsPatch
    {
        private static void Postfix(int2 coords, CItem_Vehicle itemVehicle, ref ValueTuple<int, int> __result)
        {
            var registry = Plugin.Instance?.Registry;
            if (registry == null || !registry.IsTrack(coords) || itemVehicle.wayType != 2) return;
            if (!registry.IsStopPosition(coords)) { __result = (6, 6); return; }
            var neighbors = new int2[7]; var count = SSingleton<SWorld>.Inst.GetNeighborsCoords(coords, neighbors);
            var first = -1; var second = -1;
            for (var i = 0; i < count; i++)
            {
                if (neighbors[i].Negative || registry.DifferentLanes(coords, neighbors[i])) continue;
                if (!(SSingleton<SWorld>.Inst.GetContent(neighbors[i]) is CItem_Way way) || !way.CanBeUsedBy(itemVehicle)) continue;
                if (first < 0) first = i; else if (second < 0) second = i; else { first = second = 6; break; }
            }
            __result = first < 0 || second < 0 || SSingleton<SWays_PF>.Inst.IsUTurn(first, second) ? (6, 6) : (first, second);
        }
    }
    [HarmonyPatch(typeof(CItem_Vehicle), nameof(CItem_Vehicle.IsValidWay))]
    internal static class StationStoppingPointPatch
    {
        private static bool Prefix(CItem_Vehicle __instance, int2 coords, bool isFindingPath, bool onLineCreation, ref bool __result)
        {
            var registry = Plugin.Instance?.Registry;
            if (registry == null || __instance.wayType != 2 || isFindingPath || !onLineCreation || !registry.IsTrack(coords) || registry.IsStopPosition(coords)) return true;
            // Route endpoints belong at the center, where every buffer is in reach.
            __result = false; return false;
        }
    }
    [HarmonyPatch(typeof(SWays_Generation), nameof(SWays_Generation.GenerateProceduralWayMesh))]
    internal static class StationRailGraphicsPatch
    {
        private static void Prefix(CItem_Way itemWay, int2 coords, ref byte? neiHasWay)
        {
            var registry = Plugin.Instance?.Registry;
            if (registry == null || registry.At(coords) == null || (itemWay.wayTypes & 2) == 0) return;
            var neighbors = new int2[7]; var count = SSingleton<SWorld>.Inst.GetNeighborsCoords(coords, neighbors);
            byte mask = 0;
            for (var i = 0; i < count; i++)
                if (neighbors[i].Positive && !registry.DifferentLanes(coords, neighbors[i]) &&
                    (neiHasWay.HasValue ? (neiHasWay.Value & (1 << i)) != 0 : itemWay.HasCommonWayType(SSingleton<SWorld>.Inst.GetContent(neighbors[i]) as CItem_Way)))
                    mask |= (byte)(1 << i);
            neiHasWay = mask;
        }
    }
    [HarmonyPatch(typeof(SWays_PF), "ComputePath_RailPF")]
    internal static class RailPathScope
    {
        [ThreadStatic] internal static int Depth;
        private static void Prefix(out int __state) { __state = Depth; Depth++; }
        private static Exception Finalizer(Exception __exception, int __state) { Depth = __state; return __exception; }
    }
    [HarmonyPatch(typeof(SWorld), nameof(SWorld.GetNeighborsCoords))]
    internal static class StationRailPathNeighborsPatch
    {
        private static void Postfix(int2 a, int2[] neighborsCoords, int __result)
        {
            var registry = Plugin.Instance?.Registry;
            if (RailPathScope.Depth == 0 || registry == null || registry.At(a) == null) return;
            for (var i = 0; i < __result; i++)
                if (registry.DifferentLanes(a, neighborsCoords[i])) neighborsCoords[i] = int2.negative;
        }
    }
    [HarmonyPatch(typeof(SSave), nameof(SSave.Save), new[] { typeof(string) })]
    internal static class SavePatch
    {
        private static void Prefix(string filename, out long __state)
        {
            var path = Path.Combine(SSingleton<SSaveUtils>.Inst.GetSaveFolder(), filename);
            __state = File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0;
        }
        private static void Postfix(string filename, long __state)
        {
            try
            {
                var path = Path.Combine(SSingleton<SSaveUtils>.Inst.GetSaveFolder(), filename);
                if (File.Exists(path) && File.GetLastWriteTimeUtc(path).Ticks != __state) Plugin.Instance.Registry.Save(path);
            }
            catch (Exception error) { Plugin.Instance.Error(error); Plugin.Instance.Message("Zusätzliche Bahnhofsdaten konnten nicht gespeichert werden."); }
        }
    }
    [HarmonyPatch(typeof(SLoad), "Load_Coroutine")]
    internal static class LoadPatch { private static void Prefix(string filename) { if (Plugin.Instance != null) Plugin.Instance.Registry.PendingLoad = filename; } }
}
