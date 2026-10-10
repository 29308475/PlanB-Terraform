using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using PlanBBahnbetrieb;

internal static class Program
{
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Main()
    {
        Console.WriteLine("Starting freight station checks.");
        var game = @"C:\Program Files (x86)\Steam\steamapps\common\Plan B Terraform";
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            var name = new AssemblyName(e.Name).Name + ".dll";
            foreach (var folder in new[] { Path.Combine(game, "Plan B Terraform_Data", "Managed"), Path.Combine(game, "BepInEx", "core") })
                if (File.Exists(Path.Combine(folder, name))) return Assembly.LoadFrom(Path.Combine(folder, name));
            return null;
        };
        try { Run(); }
        catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
    }
    private static void Run()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        Assert(SaveIdentity.Matches(1, 123, 456, null, 123, 456, null), "Legacy station data rejected for a matching save.");
        Assert(!SaveIdentity.Matches(1, 123, 456, null, 123, 457, null), "Legacy data accepted for a different save timestamp.");
        var hash = SaveIdentity.Fingerprint(Path.Combine(root, "bin", "Release", "net48", "PlanBBahnbetrieb.dll"));
        Assert(hash.Length == 64 && SaveIdentity.Matches(3, 123, 456, hash, 123, 999, hash), "File copy timestamp invalidates a matching content hash.");
        Assert(!SaveIdentity.Matches(3, 123, 456, hash, 123, 456, "different"), "Wrong content hash accepted.");
        Assert(!SaveIdentity.Matches(3, 123, 456, hash, 124, 456, hash), "Wrong save length accepted.");
        Assert(!SaveIdentity.Matches(4, 123, 456, hash, 123, 456, hash), "Unknown sidecar version accepted.");
        Console.WriteLine("Passed: legacy save identity, copied-save timestamps, content hash and rejection of unrelated station data.");
        var footprint = StationLayout.Footprint(); var rails = StationLayout.Rails(); var buffers = StationLayout.Buffers();
        Assert(rails.Count == 18 && buffers.Count == 4, "Expected two nine-cell tracks and four buffers.");
        Assert(footprint.Distinct().Count() == footprint.Count, "Overlapping footprint cells.");
        for (var mirror = 0; mirror < 2; mirror++)
        {
            var transformedRails = rails.Select(c => mirror == 0 ? c : c.Mirror()).ToList();
            var transformedBuffers = buffers.Select(c => mirror == 0 ? c : c.Mirror()).ToList();
            var transformedFootprint = footprint.Select(c => mirror == 0 ? c : c.Mirror()).ToList();
            for (var rotation = 0; rotation < 6; rotation++)
            {
                Assert(transformedRails.Concat(transformedBuffers).All(transformedFootprint.Contains), "Components outside footprint.");
                foreach (var buffer in transformedBuffers)
                    foreach (var stop in new[] { transformedRails[4], transformedRails[13] })
                        Assert(HexCell.Distance(buffer, stop) <= 3, "A buffer cannot reach both stopping points.");
                for (var lane = 0; lane < 2; lane++)
                    for (var i = 1; i < 9; i++)
                        Assert(HexCell.Distance(transformedRails[lane * 9 + i - 1], transformedRails[lane * 9 + i]) == 1, "Track not continuous.");
                transformedRails = transformedRails.Select(c => c.Rotate()).ToList();
                transformedBuffers = transformedBuffers.Select(c => c.Rotate()).ToList();
                transformedFootprint = transformedFootprint.Select(c => c.Rotate()).ToList();
            }
        }
        var modelPath = Path.Combine(root, "Gueterbahnhof_GLB", "06_freight_station.glb");
        GlbAsset model;
        using (var input = File.OpenRead(modelPath)) model = GlbAsset.Read(input);
        Assert(model.Materials.Count == 6 && model.Indices.Count > 0, "Invalid imported station materials/geometry.");
        Assert(model.Indices.All(i => i >= 0 && i < model.Vertices.Count), "Invalid triangle index.");
        Assert(model.Normals.Count == model.Vertices.Count && model.TriangleMaterials.Count == model.Indices.Count / 3, "Mesh attribute count mismatch.");
        Assert(model.Height > 5 && model.Height < 5.2f, "Incorrect station height/axis.");
        var bytes = File.ReadAllBytes(modelPath);
        var json = JObject.Parse(Encoding.UTF8.GetString(bytes, 20, BitConverter.ToInt32(bytes, 12)));
        var expected = 0;
        foreach (var mesh in json["meshes"])
        {
            var name = (string)mesh["name"];
            if (name.StartsWith("wagon_") || name.StartsWith("tracks_")) continue;
            foreach (var primitive in mesh["primitives"]) expected += (int)json["accessors"][(int)primitive["indices"]]["count"];
        }
        Assert(model.Indices.Count == expected && expected < 7355 * 3, "Static wagon/model rails were not excluded.");
        bytes[0] = 0;
        try { using (var stream = new MemoryStream(bytes)) GlbAsset.Read(stream); throw new Exception("Corrupt GLB accepted."); }
        catch (InvalidDataException) { }

        var plugin = Assembly.LoadFrom(Path.Combine(root, "bin", "Release", "net48", "PlanBBahnbetrieb.dll"));
        Assert(plugin.GetManifestResourceNames().Contains("PlanBBahnbetrieb.station.glb"), "Model not embedded in plugin.");
        CheckCargo(plugin);
        var hooks = 0;
        foreach (var type in plugin.GetTypes())
        {
            var patch = type.GetCustomAttributes(typeof(HarmonyPatch), false).Cast<HarmonyPatch>().FirstOrDefault();
            if (patch == null) continue;
            var info = patch.info;
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var method = info.argumentTypes == null ? info.declaringType.GetMethod(info.methodName, flags) : info.declaringType.GetMethod(info.methodName, flags, null, info.argumentTypes, null);
            Assert(method != null, "Native hook missing: " + info.declaringType + "." + info.methodName);
            foreach (var hook in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Where(m => new[] { "Prefix", "Postfix", "Finalizer" }.Contains(m.Name)))
                foreach (var parameter in hook.GetParameters())
                {
                    if (parameter.Name.StartsWith("__")) continue;
                    var native = method.GetParameters().FirstOrDefault(p => p.Name == parameter.Name);
                    Assert(native != null, "Hook parameter missing: " + type.Name + "." + parameter.Name);
                    var parameterType = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType() : parameter.ParameterType;
                    var nativeType = native.ParameterType.IsByRef ? native.ParameterType.GetElementType() : native.ParameterType;
                    Assert(parameterType == nativeType, "Hook parameter type mismatch: " + type.Name + "." + parameter.Name);
                }
            hooks++;
        }
        Assert(hooks >= 10, "Missing native integration hooks.");
        Console.WriteLine("Passed: footprint and depot reach for 12 orientations, continuous dual tracks, GLB geometry/materials/filtering, malformed input, embedded asset and " + hooks + " native hook contracts. In-game placement and transport still require a live check.");
    }
    private static void CheckCargo(Assembly plugin)
    {
        var stationType = plugin.GetType("PlanBBahnbetrieb.FreightStation", true);
        var evaluate = plugin.GetType("PlanBBahnbetrieb.CargoPriority", true).GetMethod("Evaluate", BindingFlags.Static | BindingFlags.NonPublic);
        Assert((int)evaluate.Invoke(null, new object[] { false, false, -1, 0, 0 }) == 681, "An isolated buffer must use native neutral depot priority.");
        Assert((int)evaluate.Invoke(null, new object[] { true, false, -1, 0, 0 }) == 1, "An arriving train/producer must supply the buffer.");
        Assert((int)evaluate.Invoke(null, new object[] { false, true, -1, 0, 0 }) == 1023, "A departing train/consumer must draw from the buffer.");
        Assert((int)evaluate.Invoke(null, new object[] { false, false, 800, 1200, 2 }) == 600, "Matching neighbor depots must balance their priority.");
        Assert((int)evaluate.Invoke(null, new object[] { true, false, 800, 1200, 2 }) == 340, "Native producer priority limit changed.");
        Console.WriteLine("Passed: neutral, inbound/outbound train and shared-depot cargo priorities.");
        var bodyType = plugin.GetType("PlanBBahnbetrieb.StationBody", true);
        Assert(typeof(CItem_Content).IsAssignableFrom(bodyType) && !typeof(CItem_ContentNei).IsAssignableFrom(bodyType),
            "Arbitrary station coordinates must never be interpreted as native seven-hex direction bits.");
        var station = (CItem_ContentStock)Activator.CreateInstance(stationType, true);
        station.stacksInfos = Enumerable.Range(0, 4).Select(_ => new CStackInfos { nbMax = 40 }).ToArray();
        var stacks = new CStacks(station);
        var ware = new CItem();
        stacks.stacks[0].item = ware; stacks.stacks[0].nb = 23;
        var copy = (CStacks)stationType.GetMethod("CopyConfiguration", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { stacks });
        Assert(copy != stacks && copy.stacks[0].item == ware && copy.stacks.All(s => s.nb == 0), "Copy must preserve cargo choices without duplicating stored goods.");
        Assert(stacks.stacks[0].nb == 23, "Copy emptied the original station.");
        Assert(stationType.GetMethod("IsDraggable").DeclaringType == stationType, "Moving must protect active railway lines.");
        Console.WriteLine("Passed: copied cargo settings, empty copied buffers, unchanged source stocks and station move guard.");
        station.RefreshStacksInfos(default, stacks);
        Assert(stacks.stacks.Length == 4 && stacks.stacks.All(s => s.IsDepot && s.nbMax == 40), "All four buffers must be native depot-demand stacks.");
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var context = plugin.GetType("PlanBBahnbetrieb.CargoContext", true);
        var drone = (CDrone)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(CDrone));
        drone.depotCoords = new int2(12, 15);
        context.GetField("Drone", flags).SetValue(null, drone);
        var hook = plugin.GetType("PlanBBahnbetrieb.StationDroneStackPatch", true).GetMethod("Prefix", flags);
        for (var slot = 0; slot < 4; slot++)
        {
            context.GetField("Slot", flags).SetValue(null, slot);
            object[] args = { station, drone.depotCoords, 0 };
            hook.Invoke(null, args);
            Assert((int)args[2] == slot, "A station drone selected the wrong cargo buffer.");
            args = new object[] { station, new int2(13, 15), 0 }; hook.Invoke(null, args);
            Assert((int)args[2] == 0, "Station drone scope leaked into another station.");
            args = new object[] { station, drone.depotCoords, 2 }; hook.Invoke(null, args);
            Assert((int)args[2] == 2, "An explicit native stack index was changed.");
            args = new object[] { new CItem_ContentStock(), drone.depotCoords, 0 }; hook.Invoke(null, args);
            Assert((int)args[2] == 0, "Ordinary depot was changed by station routing.");
        }
        context.GetField("Drone", flags).SetValue(null, null);
        object[] outside = { station, drone.depotCoords, 0 }; hook.Invoke(null, outside);
        Assert((int)outside[2] == 0, "Cargo scope remains active outside native drone search.");
        Assert(stationType.GetMethod("OnLoadDynamic").DeclaringType == stationType, "Station reconstruction after native load is missing.");
        Console.WriteLine("Passed: four internal native cargo stacks, four drone assignments, isolation from other buildings and explicit stack accesses, native-load reconstruction.");
    }
}
