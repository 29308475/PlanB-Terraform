using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace PlanBBahnbetrieb
{
    internal sealed class Cell
    {
        public int X, Y;
        public Cell() { }
        public Cell(int2 value) { X = value.x; Y = value.y; }
        [JsonIgnore] public int2 Coords => new int2(X, Y);
    }
    internal sealed class StationRecord
    {
        public Cell[] Rails, Buffers, Footprint;
        public Cell Root; public int Rotation;
        [JsonIgnore] internal readonly int[] Priorities = { 681, 681, 681, 681 };
        public bool Mirrored;
        [JsonIgnore] public GameObject Model;
        [JsonIgnore] public bool Active;
        public bool HasNativeParts()
        {
            if (Rails == null || Rails.Length != 18 || Buffers == null || (Buffers.Length != 4 && Buffers.Length != 1) || Footprint == null || Footprint.Length > 100) return false;
            var world = SSingleton<SWorld>.Inst;
            return Buffers.All(p => world.IsInWorld(p.Coords) && world.GetContent(p.Coords) is CItem_ContentDepot) &&
                Rails.All(p => world.IsInWorld(p.Coords) && world.GetContent(p.Coords) is CItem_Way way && (way.wayTypes & 2) != 0);
        }
    }

    internal sealed class StationRegistry
    {
        internal readonly List<StationRecord> Stations = new List<StationRecord>();
        private readonly Dictionary<int2, (StationRecord Station, int Lane)> tracks = new Dictionary<int2, (StationRecord, int)>();
        private readonly Dictionary<int2, StationRecord> occupied = new Dictionary<int2, StationRecord>();
        private readonly Dictionary<int2, StationRecord> buffers = new Dictionary<int2, StationRecord>();
        internal string PendingLoad;
        private sealed class SaveData
        {
            public int Version = 3, Seed;
            public string SaveHash;
            public long SaveLength, SaveWriteTicks;
            public List<StationRecord> Stations;
        }
        internal void Clear()
        {
            foreach (var station in Stations) if (station.Model != null) UnityEngine.Object.Destroy(station.Model);
            Stations.Clear(); tracks.Clear(); buffers.Clear(); occupied.Clear();
        }
        internal void Add(StationRecord station)
        {
            if (Stations.Any(x => x.Buffers[0].Coords == station.Buffers[0].Coords)) return;
            Stations.Add(station); Refresh();
        }
        internal void Refresh()
        {
            tracks.Clear(); buffers.Clear(); occupied.Clear();
            foreach (var station in Stations)
            {
                station.Active = station.HasNativeParts();
                if (!station.Active) { if (station.Model != null) station.Model.SetActive(false); continue; }
                for (var i = 0; i < station.Rails.Length; i++) tracks[station.Rails[i].Coords] = (station, i / 9);
                foreach (var cell in station.Buffers) buffers[cell.Coords] = station;
                foreach (var cell in station.Footprint) occupied[cell.Coords] = station;
                if (!GGame.isPlaying) continue;
                if (station.Model == null) station.Model = Plugin.Instance.Model.Spawn(Plugin.Instance.transform, false);
                StationModel.Position(station.Model, station.Rails[0].Coords, station.Rails[8].Coords, station.Rails[4].Coords, station.Rails[13].Coords, station.Mirrored);
                station.Model.SetActive(GGame.isPlaying);
            }
        }
        internal StationRecord At(int2 coords)
        {
            if (tracks.TryGetValue(coords, out var track)) return track.Station;
            if (buffers.TryGetValue(coords, out var station)) return station;
            return occupied.TryGetValue(coords, out station) ? station : null;
        }
        internal bool IsReserved(int2 coords) => occupied.ContainsKey(coords);
        internal bool IsPart(int2 coords) => tracks.ContainsKey(coords) || buffers.ContainsKey(coords);
        internal bool IsTrack(int2 coords) => tracks.ContainsKey(coords);
        internal bool IsStopPosition(int2 coords) => tracks.TryGetValue(coords, out var track) && track.Station.Rails[track.Lane * 9 + 4].Coords == coords;
        internal bool DifferentLanes(int2 a, int2 b) => tracks.TryGetValue(a, out var first) && tracks.TryGetValue(b, out var second) && first.Station == second.Station && first.Lane != second.Lane;

        internal void LoadPending()
        {
            if (PendingLoad == null) return;
            var path = PendingLoad; PendingLoad = null;
            if (!File.Exists(path + ".bahnbetrieb.json")) return;
            try
            {
                var file = new FileInfo(path);
                var data = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(path + ".bahnbetrieb.json"));
                if (data == null || !SaveIdentity.Matches(data.Version, data.SaveLength, data.SaveWriteTicks, data.SaveHash,
                    file.Length, file.LastWriteTimeUtc.Ticks, data.Version == 3 ? SaveIdentity.Fingerprint(path) : null))
                {
                    Plugin.Instance.LogInfo("Veraltete Bahnhof-Zusatzdatei übersprungen; im Spielstand enthaltene Bahnhöfe bleiben geladen. Datei: " + path);
                    return;
                }
                if (data.Stations == null || data.Stations.Count > 10000) throw new InvalidDataException("Ungültige Bahnhofsliste.");
                foreach (var station in data.Stations)
                    if (station != null && station.HasNativeParts()) { if (station.Root == null) Migrate(station); else Add(station); }
                Refresh(); Plugin.Instance.LogInfo("Güterbahnhöfe geladen: " + Stations.Count);
            }
            catch (Exception error) { Plugin.Instance.Error(error); Plugin.Instance.Message("Bahnhofsdaten konnten nicht geladen werden. Native Gleise und Waren bleiben erhalten."); }
        }
        internal void Remove(StationRecord station)
        {
            if (station.Model != null) UnityEngine.Object.Destroy(station.Model);
            Stations.Remove(station); Refresh();
        }
        private void Migrate(StationRecord old)
        {
            var anchor = old.Buffers[0].Coords;
            StationRecord replacement = null;
            for (var rotation = 0; rotation < 6; rotation++)
                foreach (var mirror in new[] { false, true })
                {
                    var candidate = Plugin.Instance.Placement.MakeRecord(anchor, rotation, mirror);
                    if (candidate.Rails.Select(c => c.Coords).SequenceEqual(old.Rails.Select(c => c.Coords))) replacement = candidate;
                }
            if (replacement == null) throw new InvalidDataException("Alte Bahnhofsgeometrie konnte nicht zugeordnet werden.");
            var cargo = new CStacks(FreightStation.Item);
            foreach (var buffer in old.Buffers) SSingleton<SDrones>.Inst.OnDepotDestroyed(buffer.Coords);
            for (var i = 0; i < 4; i++)
            {
                var c = old.Buffers[i].Coords;
                var stock = GHexes.stacks[c.x,c.y];
                cargo.stacks[i].SetItem(stock.stacks[0].item, stock.stacks[0].nb);
                FreightStation.ClearCell(c, SSingleton<SWorld>.Inst.GetContent(c));
            }
            FreightStation.Item.BuildRoot(anchor, replacement.Rotation, replacement.Mirrored, cargo, false);
            foreach (var cell in replacement.Footprint)
            {
                var c = cell.Coords;
                if (c == anchor || replacement.Rails.Any(r => r.Coords == c)) continue;
                if (SSingleton<SWorld>.Inst.GetContent(c) != null) continue;
                GHexes.contentId[c.x,c.y] = FreightStation.Body.id;
                GHexes.contentData[c.x,c.y] = (uint)SMisc.CoordToInt(anchor);
                SSingleton<SViewArea>.Inst.OnChangeHex(c);
            }
            Add(replacement);
            Plugin.Instance.LogInfo("Bestehenden Bahnhof auf interne Warenpuffer umgestellt: " + anchor);
        }
        internal void Save(string path)
        {
            var file = new FileInfo(path);
            if (!file.Exists) return;
            var data = new SaveData { Seed = GWorld.randomSeedUsed, SaveLength = file.Length, SaveWriteTicks = file.LastWriteTimeUtc.Ticks, SaveHash = SaveIdentity.Fingerprint(path), Stations = Stations };
            var target = path + ".bahnbetrieb.json";
            var temporary = target + ".tmp";
            File.WriteAllText(temporary, JsonConvert.SerializeObject(data, Formatting.Indented));
            if (File.Exists(target)) File.Replace(temporary, target, null); else File.Move(temporary, target);
        }
    }
}
