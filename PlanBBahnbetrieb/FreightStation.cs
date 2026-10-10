using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace PlanBBahnbetrieb
{
    internal sealed class FreightStation : CItem_ContentDepot
    {
        internal static FreightStation Item;
        internal static StationBody Body;
        internal static readonly HashSet<CItemData> DepotSecondaryFields = new HashSet<CItemData>();
        public CItemData dataRotation = new CItemData(3, 5);
        public CItemData dataMirror = new CItemData(1, 1);
        internal static void Register()
        {
            Item = GItems.items.OfType<FreightStation>().FirstOrDefault();
            if (Item != null) return;
            if (GItems.items.Count > 253) throw new InvalidOperationException("Keine freien Gebäude-IDs für den Güterbahnhof.");
            var rail = GItems.items.OfType<CItem_Way>().First(x => x.wayTypes == 2 && !(x is CItem_WayStop));
            Item = new FreightStation {
                id = (byte)GItems.items.Count, codeName = "tony_freight_station", codeListName = rail.codeListName,
                uiGroup = rail.uiGroup, icon = new StationIcon(), colorItem = new Color32(85, 155, 205, 255),
                isFree = true, isDynamic = true, isSelectable = true, preventDrag = false, preventDuplicate = false,
                showBuildingPreview = false, skipContainerCreation = true, range = 3, nbDrones = 4,
                dronesPosOffset = Enumerable.Range(0, 4).Select(i => new Vector3((i - 1.5f) * .4f, 1.5f, 0)).ToArray(),
                stacksInfos = Enumerable.Range(0, 4).Select(i => new CStackInfos { nbMax = 40, pos = new Vector3((i - 1.5f) * .7f, .7f, 0) }).ToArray()
            };
            GItems.items.Add(Item);
            Body = new StationBody { id = (byte)GItems.items.Count, codeName = "tony_freight_station_body", codeListName = rail.codeListName,
                icon = rail.icon, hiddenInItemBar = true, colorItem = Item.colorItem, isFree = true, isSelectable = true,
                preventDrag = true, preventDuplicate = true, showBuildingPreview = false, skipContainerCreation = true };
            GItems.items.Add(Body);
            DepotSecondaryFields.Clear();
            foreach (var depot in GItems.items.OfType<CItem_ContentDepot>()) DepotSecondaryFields.Add(depot.dataSecondarySupply);
            Plugin.Instance.LogInfo("Güterbahnhof im Transport-Baumenü registriert, Gebäude-ID " + Item.id);
        }
        public override void RefreshTexts()
        {
            localizedName = "GÜTERBAHNHOF";
            localizedDesc = "Lagert vier Waren und versorgt zwei Durchfahrtsgleise über Drohnen.";
            localizedTooltip = localizedDesc + " Baukosten: 18 Schienen und 4 Depots mit Reichweite 3.";
        }
        public override bool IsBuildable(int2 coords, bool simulateShift = false, bool isRecursiveCall = false) => Plugin.Instance.Placement.CanBuildAt(coords);
        public override string GetBuildFailedInfosMessage(int2 coords) => Plugin.Instance.Placement.Error;
        public override bool Update_TryToBuild(int2 coords, int2 coordsLastFrame, int indexInSelection)
        {
            if (GScene3D.actionMode == GScene3D.ActionModes.Build && GInputs.itemAction2.IsKeyDown()) Plugin.Instance.Placement.Rotation = (Plugin.Instance.Placement.Rotation + 1) % 6;
            GenerateItemBuildingMesh(coords, IsBuildable(coords), this, false, indexInSelection);
            var built = int2.negative;
            if (GInputs.action.IsKeyUp()) Build_IFP(coords, indexInSelection, ref built);
            return built.Positive;
        }
        public override void GenerateItemBuildingMesh(int2 coords, bool isValid, CItem item, bool validWithShift = false, int index = 0) { }
        public override void Build(int2 coords, int indexInSelection, bool fx = true)
        {
            var rotation = Plugin.Instance.Placement.Rotation;
            var mirrored = false;
            CStacks source = null;
            if (indexInSelection >= 0 && indexInSelection < SSingleton<SSelection>.Inst.selection.Count &&
                SSingleton<SSelection>.Inst.selection[indexInSelection].item == this && GScene3D.actionMode != GScene3D.ActionModes.Build)
            {
                var selected = SSingleton<SSelection>.Inst.selection[indexInSelection];
                rotation = dataRotation.GetValue(selected.data); mirrored = dataMirror.GetValue(selected.data) != 0; source = selected.stacks;
                if (GScene3D.actionMode == GScene3D.ActionModes.Duplicate && source != null)
                source = CopyConfiguration(source);
            }
            Plugin.Instance.Placement.BuildStation(coords, rotation, mirrored, source, fx);
            if (GScene3D.actionMode != GScene3D.ActionModes.Build && indexInSelection >= 0 && indexInSelection < SSingleton<SSelection>.Inst.selection.Count)
                dataSecondarySupply.SetValue(coords, dataSecondarySupply.GetValue(SSingleton<SSelection>.Inst.selection[indexInSelection].data));
        }
        internal static CStacks CopyConfiguration(CStacks source)
        {
            var copy = source.Clone();
            for (var i = 0; i < copy.stacks.Length; i++) copy.stacks[i].nb = 0;
            return copy;
        }
        internal void BuildRoot(int2 coords, int rotation, bool mirrored, CStacks source, bool fx)
        {
            GHexes.contentId[coords.x, coords.y] = id; GHexes.contentData[coords.x, coords.y] = 0;
            dataRotation.SetValue(coords, rotation); dataMirror.SetValue(coords, mirrored ? 1 : 0);
            var stacks = GHexes.stacks[coords.x, coords.y] = new CStacks(this);
            if (source != null) for (var i = 0; i < 4; i++) stacks.stacks[i].SetItem(source.stacks[i].item, source.stacks[i].nb);
            RefreshStacksInfos(coords, stacks);
            SSingleton<SItems>.Inst.OnChangeContent(this, coords, EModification.replaced);
            BuildFinalize(coords, fx);
            SSingleton<SDrones>.Inst.OnDepotBuilt(this, coords);
        }
        public override void OnLoadDynamic(int2 coords)
        { Plugin.Instance.Registry.Add(Plugin.Instance.Placement.MakeRecord(coords, dataRotation.GetValue(coords), dataMirror.GetValue(coords) != 0)); }
        public override void RefreshStacksInfos(int2 coords, CStacks stacks)
        { for (var i = 0; i < stacks.stacks.Length; i++) stacks.stacks[i].demand = CStack.Demand.Depot; }
        public override void OnSelectItem(int2 coords) => Plugin.Instance.Hud.Show(coords);
        public override bool Update_IfSelected(int2 coords, int indexInSelection)
        {
            if (SSingleton<SSelection>.Inst.selection.Count == 1)
                SSceneSingleton<SScene3D>.Inst.uiHex_DepotRange.GenerateDepotRange(coords, range, GColors.hexDepotRange_Exterior, GColors.hexDepotRange_Interior, indexInSelection);
            if (indexInSelection == 0 && GScene3D.actionMode == GScene3D.ActionModes.None && GInputs.itemAction2.IsKeyUp())
                SSceneSingleton<SSceneHud_Selection>.Inst.uiSelection.buttonToggleSubtext.toggle.onValueChanged.Invoke(dataSecondarySupply.GetValue(coords) == 0);
            return false;
        }
        public override void FillSelectionUI(bool fullReset, int2 coords, SSceneHud_Selection.UI uiSelection)
        {
            uiSelection.item.active = false; uiSelection.itemList.active = false;
            uiSelection.buttonToggleSubtext.Set("UI_Selection_BackupSupply", "UI_Selection_BackupSupply_Depot_Subtext", dataSecondarySupply.GetValue(coords) != 0, "UI_Selection_BackupSupply_Depot_Tooltip", false);
        }
        public override void SpawnContentModels(Transform parentTr, List<CPoolElementId> hexModels, int2 coords) { }
        public override void SpawnContainersModels(List<CPoolElementId> hexModels, int2 coords, bool forceOnAltitude0 = false) { }
        public override void Update_IfVisible(int2 coords, List<CPoolElementId> models, bool updateAtBuild = false) { }
        public override void OnNewlyVisible(List<CPoolElementId> hexModels, int2 coords) { }
        public override CTransform GetStackContainerTr(CStacks stacks, int iStack, int iContainer, int2 coords = default(int2), bool forceOnAltitude0 = false)
        {
            var tr = SSingleton<SWorld>.Inst.GetHexTransform(coords);
            var record = Plugin.Instance.Registry.At(coords);
            if (record?.Model != null) return new CTransform(record.Model.transform.TransformPoint(new Vector3((iStack - 1.5f) * 1.4f, .8f + iContainer * .015f, 0)), record.Model.transform.rotation);
            return new CTransform(tr.pos + tr.rot * stacksInfos[iStack].pos, tr.rot);
        }
        public override bool IsDraggable(int2 coords)
        {
            var record = Plugin.Instance.Registry.At(coords);
            return record != null && !record.Rails.Any(c => SSingleton<SWays>.Inst.GetLine(c.Coords)?.vehicles.Count > 0);
        }
        public override void Destroy(int2 coords, bool dumpContainers = true, bool fx = false, bool comesFromLava = false)
        {
            var record = Plugin.Instance.Registry.At(coords);
            if (record == null || record.Root?.Coords != coords) return;
            if (record.Rails.Any(c => SSingleton<SWays>.Inst.GetLine(c.Coords)?.vehicles.Count > 0))
            { Plugin.Instance.Message("Bitte zuerst die Züge von den Bahnhofslinien entfernen."); return; }
            SSingleton<SDrones>.Inst.OnDepotDestroyed(coords);
            var stocks = GetStacks(coords);
            Plugin.Instance.Registry.Remove(record);
            foreach (var cell in record.Footprint)
                if (SSingleton<SWorld>.Inst.GetContent(cell.Coords) is StationBody) ClearCell(cell.Coords, Body);
            // Keep the underlying railway network, including its stops and routes, intact.
            ClearCell(coords, this);
            if (dumpContainers && stocks != null) foreach (var stack in stocks.stacks)
                if (stack.item != null && stack.nb > 0) SSingleton<SItems>.Inst.DumpContainersAround(coords, stack.item, stack.nb);
            if (!GGame.AllFree && (!dumpContainers || GItems.refundItemsOnDestroy)) Plugin.Instance.Placement.RefundBuffers();
            Plugin.Instance.Hud.Hide();
        }
        internal static void ClearCell(int2 coords, CItem_Content item)
        {
            GHexes.contentId[coords.x, coords.y] = 0; GHexes.contentData[coords.x, coords.y] = 0; GHexes.stacks[coords.x, coords.y] = null;
            SSingleton<SItems>.Inst.OnChangeContent(item, coords, EModification.removed);
            SSingleton<SViewArea>.Inst.OnChangeHex(coords);
            SSingleton<SViewWorld>.Inst.OnBuildItem_UpdateTxWorld(coords); SSingleton<SViewWorld>.Inst.OnBuildItem_UpdateTxBuildings(coords);
        }
    }
    internal sealed class StationIcon : CAssetSprite
    {
        protected override void _Load()
        {
            if (_asset != null) return;
            using (var stream = typeof(Plugin).Assembly.GetManifestResourceStream("PlanBBahnbetrieb.station.png"))
            using (var memory = new System.IO.MemoryStream())
            {
                stream.CopyTo(memory);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                var conversion = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", true);
                conversion.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) }).Invoke(null, new object[] { texture, memory.ToArray(), false });
                // The supplied preview has a solid backdrop. Keep only the building in the UI sprite.
                var pixels = texture.GetPixels32(); var background = pixels[0];
                for (var i = 0; i < pixels.Length; i++)
                    if (pixels[i].r == background.r && pixels[i].g == background.g && pixels[i].b == background.b) pixels[i].a = 0;
                texture.SetPixels32(pixels); texture.Apply();
                _asset = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
            }
        }
    }
    internal sealed class StationBody : CItem_Content
    {
        public override void RefreshTexts() { localizedName = "GÜTERBAHNHOF"; localizedDesc = localizedTooltip = "Bahnhof anklicken, um die vier internen Warenpuffer einzustellen."; }
        public override void SpawnContentModels(Transform parentTr, List<CPoolElementId> hexModels, int2 coords) { }
        public override bool IsBuildable(int2 coords, bool simulateShift = false, bool isRecursiveCall = false) => false;
        public override void Destroy(int2 coords, bool dumpContainers = true, bool fx = false, bool comesFromLava = false)
        { FreightStation.Item.Destroy(SMisc.IntToCoord(GHexes.contentData[coords.x, coords.y]), dumpContainers, fx, comesFromLava); }
        public override void Update01s(int2 coords) { }
    }
    [HarmonyPatch(typeof(SItems), nameof(SItems.Reset))]
    internal static class RegisterStationPatch { private static void Prefix() => FreightStation.Register(); }
    [HarmonyPatch(typeof(SSelection), nameof(SSelection.Rotate))]
    internal static class StationRotateSelectionPatch
    {
        private static void Postfix(SSelection __instance)
        {
            foreach (var selected in __instance.selection)
                if (selected.item is FreightStation station)
                    station.dataRotation.SetValue(ref selected.data, (station.dataRotation.GetValue(selected.data) + 1) % 6);
        }
    }
    [HarmonyPatch(typeof(SSelection), nameof(SSelection.Mirror))]
    internal static class StationMirrorSelectionPatch
    {
        private static void Postfix(SSelection __instance)
        {
            foreach (var selected in __instance.selection)
                if (selected.item is FreightStation station)
                {
                    station.dataRotation.SetValue(ref selected.data, (6 - station.dataRotation.GetValue(selected.data)) % 6);
                    station.dataMirror.SetValue(ref selected.data, 1 - station.dataMirror.GetValue(selected.data));
                }
        }
    }
    [HarmonyPatch(typeof(CItem), nameof(CItem.IsItemUnlocked))]
    internal static class StationUnlockPatch
    {
        private static bool Prefix(CItem __instance, ref bool __result)
        {
            if (__instance is StationBody) { __result = false; return false; }
            if (!(__instance is FreightStation)) return true;
            __result = GItems.items.OfType<CItem_ContentDepot>().Any(d => !(d is FreightStation) && !(d is CItem_ContentDepotCreative) && d.range >= 3 && d.IsItemUnlocked()) &&
                GItems.items.OfType<CItem_Way>().Any(w => w.wayTypes == 2 && !(w is CItem_WayStop) && w.IsItemUnlocked());
            return false;
        }
    }
    [HarmonyPatch(typeof(SSelection), nameof(SSelection.Select), new[] { typeof(int2), typeof(bool), typeof(SSelection.Type), typeof(bool) })]
    internal static class StationBodySelectionPatch
    {
        private static void Prefix(ref int2 coords)
        {
            if (coords.Positive && SSingleton<SWorld>.Inst.GetContent(coords) is StationBody)
                coords = SMisc.IntToCoord(GHexes.contentData[coords.x, coords.y]);
        }
    }
    [HarmonyPatch(typeof(CDrone), nameof(CDrone.GetNei))]
    internal static class StationBodyDroneNeighborPatch
    {
        private static void Postfix(ref int2 __result)
        {
            if (__result.Positive && SSingleton<SWorld>.Inst.GetContent(__result) is StationBody)
                __result = SMisc.IntToCoord(GHexes.contentData[__result.x, __result.y]);
        }
    }
    [HarmonyPatch(typeof(SSceneHud_ItemsBars), nameof(SSceneHud_ItemsBars.GetDeletableContent))]
    internal static class StationDeleteProtectionPatch
    {
        private static void Postfix(int2 coords, ref CItem_Content __result)
        {
            var record = Plugin.Instance?.Registry.At(coords);
            if (record?.Root == null) return;
            // The owned building is removed at its hall, not piece by piece along its tracks.
            if (Plugin.Instance.Registry.IsTrack(coords) || record.Rails.Any(c => SSingleton<SWays>.Inst.GetLine(c.Coords)?.vehicles.Count > 0)) __result = null;
        }
    }
}
