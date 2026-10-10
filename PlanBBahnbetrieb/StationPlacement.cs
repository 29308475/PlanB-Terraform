using System;
using System.Linq;
using UnityEngine;

namespace PlanBBahnbetrieb
{
    internal sealed class StationPlacement
    {
        private CItem_ContentDepot depot;
        private CItem_Way rails;
        private GameObject ghost;
        internal int Rotation;
        internal string Error;
        internal bool Matches => (GSceneHud.itemInBarSelected is FreightStation && GScene3D.actionMode == GScene3D.ActionModes.Build) ||
            ((GScene3D.actionMode == GScene3D.ActionModes.Duplicate || GScene3D.actionMode == GScene3D.ActionModes.Drag) &&
             SSingleton<SSelection>.Inst.selection.Any(s => s.item is FreightStation));
        private void Orientation(out int rotation, out bool mirrored)
        {
            rotation = Rotation; mirrored = false;
            if (GScene3D.actionMode == GScene3D.ActionModes.Build) return;
            var selected = SSingleton<SSelection>.Inst.selection.FirstOrDefault(s => s.item is FreightStation);
            if (selected == null) return;
            rotation = FreightStation.Item.dataRotation.GetValue(selected.data);
            mirrored = FreightStation.Item.dataMirror.GetValue(selected.data) != 0;
        }
        internal bool Available(out string reason)
        {
            rails = GItems.items.OfType<CItem_Way>().FirstOrDefault(x => x.wayTypes == 2 && !(x is CItem_WayStop) && x.IsItemUnlocked());
            depot = GItems.items.OfType<CItem_ContentDepot>().Where(x => !(x is FreightStation) && !(x is CItem_ContentDepotCreative) && x.range >= 3 && x.IsItemUnlocked()).OrderBy(x => x.range).FirstOrDefault();
            if (rails == null || depot == null) { reason = "Benötigt freigeschaltete Schienen und Depots mit Reichweite 3."; return false; }
            reason = "Baukosten: 18 × " + rails.localizedName + ", 4 × " + depot.localizedName + ".";
            return true;
        }
        internal static int2 Translate(int2 anchor, HexCell cell, int rotation, bool mirrored)
        {
            // Root is the first former buffer, q=0/r=-2. Rotate the entire footprint about it.
            cell.R += 2;
            if (mirrored) cell = cell.Mirror();
            for (var i = 0; i < rotation; i++) cell = cell.Rotate();
            var cube = SMisc.CoordsToCube(anchor);
            return SMisc.CubeToCoords(cube.q + cell.Q, cube.r + cell.R);
        }
        internal StationRecord MakeRecord(int2 anchor, int rotation, bool mirrored) => new StationRecord {
            Root = new Cell(anchor), Rotation = rotation, Mirrored = mirrored,
            Rails = StationLayout.Rails().Select(c => new Cell(Translate(anchor, c, rotation, mirrored))).ToArray(),
            Buffers = new[] { new Cell(anchor) },
            Footprint = StationLayout.Footprint().Select(c => new Cell(Translate(anchor, c, rotation, mirrored))).ToArray()
        };
        internal bool CanBuild() => CanBuildAt(GScene3D.mouseoverCoords);
        internal bool CanBuildAt(int2 coords)
        {
            Error = null;
            if (!Available(out var reason)) return Fail(reason);
            Orientation(out var rotation, out var mirrored);
            var record = MakeRecord(coords, rotation, mirrored);
            var world = SSingleton<SWorld>.Inst;
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            foreach (var cell in record.Footprint)
            {
                var c = cell.Coords;
                if (!world.IsInWorld(c) || !GHexes.IsHexStandard(c) || !world.IsOutsideOfPoles(c)) return Fail("Der Bahnhof passt hier nicht ins Hexraster.");
                var content = world.GetContent(c);
                var railIndex = Array.FindIndex(record.Rails, r => r.Coords == c);
                if (Plugin.Instance.Registry.IsReserved(c) || (content != null &&
                    (railIndex < 0 || !(content is CItem_Way way) || way.wayTypes != 2)))
                    return Fail("Gebäude und quer verlaufende Gleise blockieren die Bahnhofsfläche.");
                if (content is CItem_WayStop && railIndex != 4 && railIndex != 13)
                    return Fail("Vorhandene Haltepunkte müssen in der Mitte eines Bahnhofsgleises liegen.");
                if (GHexes.IsMountain(c) || SSingleton<SPlanet>.Inst.GetWaterAboveIce(c) >= GWater.waterDepthMin || SSingleton<SPlanet>.Inst.IsIce(c)) return Fail("Benötigt trockenen, eisfreien Boden ohne Berge.");
                min = Mathf.Min(min, GHexes.altitude[c.x,c.y]); max = Mathf.Max(max, GHexes.altitude[c.x,c.y]);
            }
            var spacing = Vector3.Distance(GHexes.Pos(record.Rails[3].Coords), GHexes.Pos(record.Rails[4].Coords));
            if (max - min > spacing * .08f) return Fail("Die Bahnhofsfläche ist zu uneben. Bitte zuerst das Gelände ebnen.");
            var extensions = new[] { new HexCell(-5, 0), new HexCell(5, 0), new HexCell(-5, 1), new HexCell(5, 1) }
                .Select(c => Translate(coords, c, rotation, mirrored)).ToArray();
            var neighbors = new int2[7];
            foreach (var rail in record.Rails)
            {
                var count = world.GetNeighborsCoords(rail.Coords, neighbors);
                for (var i = 0; i < count; i++)
                    if (world.GetContent(neighbors[i]) is CItem_Way neighbor && (neighbor.wayTypes & 2) != 0 &&
                        !record.Rails.Any(r => r.Coords == neighbors[i]) && !extensions.Contains(neighbors[i]))
                        return Fail("Eine Gleisabzweigung liegt zu nah an der Bahnhofshalle. Der Bahnhof benötigt gerade Durchfahrtsgleise.");
            }
            var missing = record.Rails.Count(c => world.GetContent(c.Coords) == null);
            if (!GGame.AllFree && ((!rails.isFree && rails.nbOwned < missing) || (!depot.isFree && depot.nbOwned < 4)))
                return Fail("Benötigt " + missing + " zusätzliche Schienen und 4 Depots mit Reichweite 3.");
            return true;
        }
        private bool Fail(string reason) { Error = reason; return false; }
        internal void BuildStation(int2 coords, int rotation, bool mirrored, CStacks source, bool fx)
        {
            var record = MakeRecord(coords, rotation, mirrored);
            if (!Available(out var reason)) throw new InvalidOperationException(reason);
            foreach (var cell in record.Rails)
                if (SSingleton<SWorld>.Inst.GetContent(cell.Coords) == null) rails.Build(cell.Coords, -1, false);
            if (!GGame.AllFree && !depot.isFree) depot.nbOwned -= 4;
            FreightStation.Item.BuildRoot(coords, rotation, mirrored, source, fx);
            foreach (var cell in record.Footprint)
            {
                var c = cell.Coords;
                if (c == coords || record.Rails.Any(r => r.Coords == c)) continue;
                GHexes.contentId[c.x,c.y] = FreightStation.Body.id;
                GHexes.contentData[c.x,c.y] = (uint)SMisc.CoordToInt(coords); GHexes.stacks[c.x,c.y] = null;
                SSingleton<SItems>.Inst.OnChangeContent(FreightStation.Body, c, EModification.replaced);
                SSingleton<SViewArea>.Inst.OnChangeHex(c);
            }
            Plugin.Instance.Registry.Add(record);
            foreach (var rail in record.Rails) SSingleton<SViewArea>.Inst.OnChangeHex(rail.Coords);
            Plugin.Instance.LogInfo("Güterbahnhof mit vier internen Warenpuffern gebaut: " + coords);
        }
        internal void RefundBuffers()
        {
            // Rails are ordinary owned track pieces: their native destruction handles refunds.
            var buffer = GItems.items.OfType<CItem_ContentDepot>().Where(d => !(d is FreightStation) && !(d is CItem_ContentDepotCreative) && d.range >= 3).OrderBy(d => d.range).FirstOrDefault();
            if (buffer != null && !buffer.isFree) buffer.nbOwned += 4;
        }
        internal void RefundRail()
        {
            var rail = GItems.items.OfType<CItem_Way>().FirstOrDefault(w => w.wayTypes == 2 && !(w is CItem_WayStop));
            if (rail != null && !rail.isFree) rail.nbOwned++;
        }
        internal void Tick()
        {
            if (!GGame.isPlaying || !Matches || GScene3D.mouseoverCoords.Negative) { if (ghost != null) ghost.SetActive(false); return; }
            if (ghost == null) ghost = Plugin.Instance.Model.Spawn(Plugin.Instance.transform, true);
            Orientation(out var rotation, out var mirrored);
            var anchor = GScene3D.mouseoverCoords;
            if (GScene3D.actionMode != GScene3D.ActionModes.Build)
            {
                var selected = SSingleton<SSelection>.Inst.selection.First(s => s.item is FreightStation);
                anchor = SSingleton<SSelection>.Inst.GetCoordsTranslated(selected.relativeCoords, anchor);
            }
            var record = MakeRecord(anchor, rotation, mirrored);
            if (record.Rails.Any(c => !SSingleton<SWorld>.Inst.IsInWorld(c.Coords) || !GHexes.IsHex(c.Coords))) { ghost.SetActive(false); return; }
            ghost.SetActive(true); Plugin.Instance.Model.PreviewColor(CanBuildAt(anchor));
            StationModel.Position(ghost, record.Rails[0].Coords, record.Rails[8].Coords, record.Rails[4].Coords, record.Rails[13].Coords, false);
        }
        internal void Reset() { Rotation = 0; if (ghost != null) UnityEngine.Object.Destroy(ghost); ghost = null; }
    }
}
