using System;
using System.Linq;
using System.IO;
using Mono.Cecil;
using System.Reflection;
using UnityEngine;
using Roadlife;


static void Check(bool result, string message)
{
    if (!result) throw new Exception(message);
}

float small = TrafficRules.Density(.12f, 1000, 1, .75f, .35f);
float large = TrafficRules.Density(.12f, 100000, 1, .75f, .35f);
Check(large > small, "Große Städte müssen mehr Verkehr erzeugen.");
Check(TrafficRules.Density(.12f, 1e12, 1, .75f, .35f) == .35f, "Dichte muss begrenzt bleiben.");
Check(TrafficRules.Density(.12f, 1e12, 0, .75f, .35f) == .12f, "Entfernte Städte dürfen Grunddichte nicht erhöhen.");
Check(TrafficRules.Density(0, 100000, 1, .75f, .35f) == 0, "Dichte null muss Verkehr abschalten.");
Check(TrafficRules.Density(.12f, 100000, 1, 0, .35f) == .12f, "Wachstum muss abschaltbar sein.");
Check(TrafficRules.TruckFactor(.35f, .35f, .25f) == .75f, "LKW-Verlangsamung muss auf 25 Prozent begrenzt sein.");
Check(TrafficRules.TruckFactor(0, .35f, .25f) == 1, "Ohne Verkehr keine Verlangsamung.");
Check(TrafficRules.TruckFactor(.35f, .35f, 0) == 1, "Einfluss muss abschaltbar sein.");
Console.WriteLine("8 Verkehrsregel-Prüfungen bestanden.");
Check(!TrafficRules.WideRoad(false, 9999, 10000, 8000), "Keine Verbreiterung unter der Wachstumsschwelle.");
Check(TrafficRules.WideRoad(false, 10000, 10000, 8000), "Verbreiterung beim Erreichen der Wachstumsschwelle.");
Check(TrafficRules.WideRoad(true, 9000, 10000, 8000), "Vier Spuren müssen zwischen den Schwellen erhalten bleiben.");
Check(TrafficRules.WideRoad(true, 8000, 10000, 8000), "Rückbau erst unter der Rückbauschwelle.");
Check(!TrafficRules.WideRoad(true, 7999, 10000, 8000), "Rückbau unter der Rückbauschwelle.");
Check(TrafficRules.WideRoad(true, 10000, 10000, 15000), "Vertauschte Konfigurationsschwellen dürfen nicht flackern.");
Console.WriteLine("6 Wachstumsschwellen-Prüfungen bestanden.");

var gamePath = args.Length > 0 ? args[0] : @"C:\Program Files (x86)\Steam\steamapps\common\Plan B Terraform";
using var game = ModuleDefinition.ReadModule(Path.Combine(gamePath, "Plan B Terraform_Data", "Managed", "Assembly-CSharp.dll"));
var update = game.Types.Single(x => x.Name == "CVehicle").Methods.Single(x => x.Name == "Update");
var calls = update.Body.Instructions.Where(x => x.Operand is MethodReference method
    && method.DeclaringType.FullName == "UnityEngine.Mathf" && method.Name == "MoveTowards").ToList();
Check(calls.Count == 1, "Die installierte Spielversion muss genau eine passende LKW-Beschleunigungsstelle enthalten.");
var signature = (MethodReference)calls[0].Operand;
Check(signature.Parameters.Count == 3 && signature.Parameters.All(x => x.ParameterType.FullName == "System.Single"), "Beschleunigungssignatur muss kompatibel sein.");
using var mod = ModuleDefinition.ReadModule(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../bin/Release/net48/Roadlife.dll")));
var replacement = mod.Types.Single(x => x.FullName == "Roadlife.Plugin").Methods.Single(x => x.Name == "TrafficMoveTowards");
Check(replacement.IsStatic && replacement.ReturnType.FullName == "System.Single"
    && replacement.Parameters.Count == 4 && replacement.Parameters[3].ParameterType.Name == "CVehicle", "Ersatz muss Beschleunigung und Fahrzeuginstanz entgegennehmen.");
Console.WriteLine("3 LKW-Patch-Strukturprüfungen gegen die installierte Spielassembly bestanden.");
var roadGenerator = game.Types.Single(x => x.Name == "SWays_Generation").Methods.Single(x => x.Name == "GenerateProceduralWayMesh");
Check(roadGenerator.Parameters.Any(x => x.Name == "itemWay" && x.ParameterType.Name == "CItem_Way")
    && roadGenerator.Parameters.Any(x => x.Name == "coords" && x.ParameterType.Name == "int2")
    && roadGenerator.Parameters.Any(x => x.Name == "mesh" && x.ParameterType.Name == "Mesh"), "Straßenmesh-Hook muss zur installierten Spielversion passen.");
Console.WriteLine("Straßenmesh-Hook-Strukturprüfung bestanden.");

var runtimeMod = Assembly.LoadFrom(mod.FileName);
System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    foreach (var folder in new[] { Path.Combine(gamePath, "Plan B Terraform_Data", "Managed"), Path.Combine(gamePath, "BepInEx", "core") })
    {
        string file = Path.Combine(folder, name.Name + ".dll");
        if (File.Exists(file)) return context.LoadFromAssemblyPath(file);
    }
    return null;
};
var curve = runtimeMod.GetType("Roadlife.RoadVisuals").GetMethod("Curve", BindingFlags.NonPublic | BindingFlags.Static);
Vector3 Evaluate(Vector3 a, Vector3 control, Vector3 b, float t)
    => (Vector3)curve.Invoke(null, new object[] { a, control, b, t });
var curveStart = new Vector3(0, 0, 100);
var curveControl = new Vector3(1, 0, 100);
var curveEnd = new Vector3(1, 1, 100);
Check(Vector3.Distance(Evaluate(curveStart, curveControl, curveEnd, 0), curveStart) < .0001f, "Kurve muss nahtlos am Straßenrand beginnen.");
Check(Vector3.Distance(Evaluate(curveStart, curveControl, curveEnd, 1), curveEnd) < .0001f, "Kurve muss nahtlos am Straßenrand enden.");
for (int i = 0; i <= 20; i++)
{
    float t = i / 20f;
    var point = Evaluate(curveStart, curveControl, curveEnd, t);
    Check(point.magnitude >= 99.9999f && point.magnitude <= 100.011f, "Kurvenfläche darf nicht in die Kugeloberfläche absinken.");
    Check(!float.IsNaN(point.x) && point.x >= -.0001f && point.x <= 1.001f, "Kurve muss im Straßenkorridor bleiben.");
}
Console.WriteLine("Kurven-Regressionsprüfungen für Anschlüsse, Kugelhöhe und Straßenkorridor bestanden.");

var cache = new BoundedCache<int, object>(128);
var originalGeometry = new object();
cache.Put(1, originalGeometry);
for (int i = 2; i < 100; i++) cache.Put(i, new object());
Check(cache.TryGet(1, out var returnedGeometry) && ReferenceEquals(returnedGeometry, originalGeometry), "Zurückscrollen muss dieselbe Geometrie wiederverwenden.");
for (int i = 100; i < 128; i++) cache.Put(i, new object());
cache.TryGet(1, out _);
cache.Put(128, new object()); cache.Put(129, new object());
Check(cache.TryGet(1, out returnedGeometry) && ReferenceEquals(returnedGeometry, originalGeometry), "Kürzlich sichtbare Geometrie muss im Cache bleiben.");
Check(!cache.TryGet(2, out _), "Älteste ungenutzte Geometrie muss bei vollem Cache entfernt werden.");
cache.Remove(1);
Check(!cache.TryGet(1, out _), "Straßenänderung muss alte Geometrie ungültig machen.");
for (int i = 0; i < 100000; i++) cache.Put(i, new object());
Check(cache.Count == 128, "Cache darf auch bei 100.000 Straßenfeldern seine Speichergrenze nicht überschreiten.");
cache.Clear();
Check(cache.Count == 0, "Weltwechsel muss den Geometriecache freigeben.");
Console.WriteLine("Cache-Regressionsprüfungen für Zurückscrollen, Änderungen und Speichergrenze bestanden.");

var ridge = new RoadSurface(new Vector3(0, 0, 101), new[] {
    new Vector3(1, 0, 100), new Vector3(0, 1, 100),
    new Vector3(-1, 0, 100), new Vector3(0, -1, 100)
});
var pieces = new System.Collections.Generic.List<Vector3>();
ridge.ClipTriangle(new Vector3(-.5f, -.3f, 101), new Vector3(.5f, -.3f, 101), new Vector3(0, .6f, 101), .014f, pieces);
Check(pieces.Count >= 12 && pieces.Count % 3 == 0, "Fahrbahn über einer Geländekuppe muss entlang mehrerer Bodenflächen geteilt werden.");
Check(pieces.Any(p => Mathf.Abs(p.x) < .0001f && Mathf.Abs(p.y) < .0001f && p.z > 101.013f), "Die Kuppe innerhalb der Fahrbahn muss als höherer Geometriepunkt erhalten bleiben.");
foreach (var p in pieces)
{
    Vector3 ground = ridge.Project(p, .003f);
    Check(p.magnitude - ground.magnitude > .009f, "Jedes Fahrbahnstück muss über dem eingefärbten Boden liegen.");
    Vector3 paint = ridge.Project(p, .016f);
    Check(paint.magnitude > p.magnitude, "Fahrbahnmarkierung muss über der Fahrbahn liegen.");
}
Console.WriteLine("Geländekuppen-Regressionsprüfungen für vollständig sichtbare Fahrbahn bestanden.");

// A native peak displaced from the tile center must override the estimated flat fan.
var actual = new RoadSurface(new Vector3(0, 0, 100), new[] {
    new Vector3(1, 1, 100), new Vector3(-1, 1, 100),
    new Vector3(-1, -1, 100), new Vector3(1, -1, 100)
});
var peak = new Vector3(.25f, .1f, 100.3f);
var native = new System.Collections.Generic.List<Vector3>();
for (int i = 0; i < actual.corners.Length; i++)
{ native.Add(peak); native.Add(actual.corners[i]); native.Add(actual.corners[(i + 1) % actual.corners.Length]); }
actual.UseTerrain(native);
Check(actual.UsesTerrain, "Native Geländefläche muss übernommen werden.");
Check(Vector3.Distance(actual.Project(peak, .003f), peak + peak.normalized * .003f) < .0001f,
    "Eine versetzte native Kuppe darf nicht durch die geschätzte Fläche ersetzt werden.");
pieces.Clear();
actual.ClipTriangle(new Vector3(-.6f, -.6f, 100), new Vector3(.8f, -.6f, 100), new Vector3(.25f, .9f, 100), .014f, pieces);
Check(pieces.Any(p => Vector3.Distance(p, peak + peak.normalized * .014f) < .0002f),
    "Eine Kuppe im Inneren des Kreisverkehrs muss die Fahrbahn anheben.");
foreach (var p in pieces)
{
    Check(!float.IsNaN(p.x) && Mathf.Abs(p.magnitude - actual.Project(p, 0).magnitude - .014f) < .0002f,
        "Alle Teilflächen müssen dem nativen Gelände mit festem Abstand folgen.");
}
var previousPeak = actual.Project(peak, 0);
actual.UseTerrain(new System.Collections.Generic.List<Vector3>());
Check(Vector3.Distance(actual.Project(peak, 0), previousPeak) < .0001f,
    "Ein leerer Geländeblock darf die vorhandene Oberfläche nicht löschen.");
Console.WriteLine("Native Geländeprüfungen für versetzte Kuppen, Straßenhöhe und leere Blöcke bestanden.");

var junctionCenter = new Vector3(0, 0, 100);
var arms = new[] { new Vector3(1, 0, 100), new Vector3(.5f, .866f, 100),
    new Vector3(-1, 0, 100), new Vector3(-.5f, -.866f, 100) };
Check(RoadMarkingLayout.MainPair(junctionCenter, arms, out int mainA, out int mainB),
    "Eine Kreuzung muss eine durchgehende Hauptführung erhalten.");
Check(Vector3.Dot((arms[mainA] - junctionCenter).normalized, (arms[mainB] - junctionCenter).normalized) < -.999f,
    "Die Hauptführung muss das geradeste Straßenpaar verbinden.");
var unequal = new[] { new Vector3(1, 0, 100), new Vector3(-.5f, .866f, 100),
    new Vector3(0, 1, 100), new Vector3(0, -1, 100) };
RoadMarkingLayout.MainPair(junctionCenter, unequal, out mainA, out mainB);
Check(mainA == 2 && mainB == 3, "Die Auswahl darf nicht das erste schräge Paar statt des späteren geraden Paares bevorzugen.");
var widths = new[] { .24f, .24f, .24f, .24f };
Check(!RoadMarkingLayout.Exposed(new Vector3(.2f, .23f, 100), junctionCenter, arms, widths, .24f, 0),
    "Randmarkierungen dürfen nicht innerhalb einer anderen Kreuzungszufahrt liegen.");
Check(RoadMarkingLayout.Exposed(new Vector3(.45f, -.232f, 100), junctionCenter, arms, widths, .24f, 0),
    "Der äußere Fahrbahnrand muss seine Markierung behalten.");
var tJunction = new[] { arms[0], arms[1], new Vector3(-.5f, -.866f, 100) };
Check(RoadMarkingLayout.MainPair(junctionCenter, tJunction, out mainA, out mainB),
    "Auch schräge Kreuzungen müssen eine Spurführung behalten.");
float junctionStart = RoadMarkingLayout.Start(.5f, .24f);
Check(junctionStart > 0 && junctionStart < 1, "Führung und Zufahrt müssen denselben Anschluss innerhalb des Arms verwenden.");
Console.WriteLine("Kreuzungsprüfungen für Hauptführung, schräge Anschlüsse und verdeckte Randlinien bestanden.");

var approach = new RoadSurface(new Vector3(0, 0, 100), new[] {
    new Vector3(1, -1, 100), new Vector3(1, 1, 100),
    new Vector3(-1, 1, 100), new Vector3(-1, -1, 100)
});
var bridgeSeam = new Vector3(1, 0, 100.3f);
approach.BridgeApproach(bridgeSeam);
Check(Vector3.Distance(approach.Project(new Vector3(0, 0, 100), 0), new Vector3(0, 0, 100)) < .0002f,
    "Die Rampe darf das Landzentrum nicht anheben.");
Check(Vector3.Distance(approach.Project(new Vector3(-1, 0, 100), 0), new Vector3(-1, 0, 100)) < .0002f,
    "Die gegenüberliegende Landzufahrt muss ihre Höhe behalten.");
for (int i = -1; i <= 1; i++)
{
    var edge = new Vector3(1, i * .24f, 100.3f);
    var expected = edge.normalized * (bridgeSeam.magnitude / Vector3.Dot(edge.normalized, bridgeSeam.normalized) + .014f);
    Check(Vector3.Distance(approach.Project(edge, .014f), expected) < .002f,
        "Die komplette Brückenkante muss die Deckhöhe erreichen, einschließlich beider Spurränder.");
}
float lastHeight = 100;
for (int i = 0; i <= 20; i++)
{
    var p = approach.Project(new Vector3(i / 20f, 0, 100), 0);
    Check(!float.IsNaN(p.z) && p.z >= lastHeight - .0002f && p.z <= 100.301f,
        "Die Brückenrampe muss ohne Sprung oder Überhöhung ansteigen.");
    lastHeight = p.z;
}
Console.WriteLine("Brückenprüfungen für gemeinsame Deckkante, Spurränder und kontinuierliche Rampe bestanden.");

// Regression: the target deck can be below a shore ridge, but the road must never
// be blended beneath the unchanged background/native terrain.
var shoreCorners = new[] { new Vector3(1, -1, 100), new Vector3(1, 1, 100),
    new Vector3(-1, 1, 100), new Vector3(-1, -1, 100) };
var shore = new RoadSurface(new Vector3(0, 0, 100), shoreCorners);
var shoreRamp = new RoadSurface(new Vector3(0, 0, 100), shoreCorners);
var shorePeak = new Vector3(.5f, 0, 100.6f);
var shoreTriangles = new System.Collections.Generic.List<Vector3>();
for (int i = 0; i < shoreCorners.Length; i++)
{ shoreTriangles.Add(shorePeak); shoreTriangles.Add(shoreCorners[i]); shoreTriangles.Add(shoreCorners[(i + 1) % shoreCorners.Length]); }
shore.UseTerrain(shoreTriangles); shoreRamp.UseTerrain(shoreTriangles);
shoreRamp.BridgeApproach(new Vector3(1, 0, 100.1f));
for (int x = -8; x <= 8; x++)
for (int y = -8; y <= 8; y++)
{
    var sample = new Vector3(x / 10f, y / 10f, 100);
    float backgroundHeight = shore.Project(sample, .003f).magnitude;
    float asphaltHeight = shoreRamp.Project(sample, .014f).magnitude;
    Check(asphaltHeight - backgroundHeight >= .0105f,
        "Die Brückenauffahrt darf weder Gelände noch Stadtboden durch die Fahrbahn zeigen lassen.");
}
Console.WriteLine("Uferkanten-Regressionsprüfung: Rampe bleibt auch bei tieferem Brückenziel über Gelände und Boden.");

var assetFaces = new System.Collections.Generic.List<Vector3>();
BridgeAsset.Beam(new Vector3(0, 0, 0), new Vector3(0, 0, 2), new Vector3(0, 1, 0), .4f, .2f,
    (a, b, c) => { assetFaces.Add(a); assetFaces.Add(b); assetFaces.Add(c); });
Check(assetFaces.Count == 36, "Das eingebettete Brückenmodul muss zwölf geschlossene Dreiecke liefern.");
Check(Mathf.Abs(assetFaces.Min(p => p.x) + .2f) < .0001f && Mathf.Abs(assetFaces.Max(p => p.x) - .2f) < .0001f,
    "Die Breite des Brückenmoduls muss skalierbar sein.");
Check(Mathf.Abs(assetFaces.Min(p => p.y) + .1f) < .0001f && Mathf.Abs(assetFaces.Max(p => p.y) - .1f) < .0001f,
    "Die Höhe des Brückenmoduls muss skalierbar sein.");
Check(assetFaces.Min(p => p.z) == 0 && assetFaces.Max(p => p.z) == 2,
    "Brückenmodule müssen exakt zwischen ihren Anschlüssen liegen.");
for (int i = 0; i < assetFaces.Count; i += 3)
{
    Vector3 a = assetFaces[i], b = assetFaces[i + 1], c = assetFaces[i + 2];
    Check(Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - new Vector3(0, 0, 1)) > 0,
        "Alle Assetflächen müssen nach außen zeigen, einschließlich der Unterseite.");
}
assetFaces.Clear();
BridgeAsset.Beam(new Vector3(0, 0, 0), new Vector3(0, 2, 0), new Vector3(0, 0, 1), .4f, .2f,
    (a, b, c) => { assetFaces.Add(a); assetFaces.Add(b); assetFaces.Add(c); });
Check(assetFaces.Count == 36 && assetFaces.All(p => !float.IsNaN(p.x)),
    "Vertikale Geländerpfosten und Pfeiler müssen ein gültiges Mesh behalten.");
Console.WriteLine("Brückenasset-Prüfungen für eingebettetes OBJ, Skalierung, Flächenausrichtung und Pfeiler bestanden.");

foreach (int spanLength in new[] { 1, 2, 7, 20, 100 })
{
    int stations = 0;
    for (int tile = 0; tile < spanLength; tile++)
    {
        if (tile == BridgeDesign.TowerTile(spanLength)) stations++;
        if (tile + 1 < spanLength)
            Check(Math.Abs(BridgeDesign.CableHeight(spanLength, tile, 1) - BridgeDesign.CableHeight(spanLength, tile + 1, 0)) < .00001f,
                "Tragkabel müssen an jedem Feldübergang dieselbe Höhe besitzen.");
    }
    Check(stations == 1, "Eine zusammenhängende Brücke darf nur eine Station mit zwei parallelen Pylonen erhalten.");
    float top = BridgeDesign.CableHeight(spanLength, BridgeDesign.TowerTile(spanLength), .5f);
    Check(Math.Abs(top - BridgeDesign.TowerHeight(spanLength) - .06f) < .00001f,
        "Tragkabel müssen am zentralen Pylonenpaar anliegen.");
    Check(BridgeDesign.TowerHeight(spanLength) >= .65f && BridgeDesign.TowerHeight(spanLength) <= 1.6f,
        "Pylone müssen sichtbar über die Fahrbahn ragen, bei begrenzter Höhe.");
    Check(Math.Abs(BridgeDesign.CableHeight(spanLength, 0, 0) - .06f) < .00001f
        && Math.Abs(BridgeDesign.CableHeight(spanLength, spanLength - 1, 1) - .06f) < .00001f,
        "Tragkabel müssen an beiden Enden zur Fahrbahn zurücklaufen.");
}
Console.WriteLine("Hängebrückenprüfungen für genau ein Pylonenpaar, durchgehende Tragkabel und Endanschlüsse bestanden.");

var oldFan = new RoadSurface(junctionCenter, shoreCorners);
var oldFragments = new System.Collections.Generic.List<Vector3>();
for (int i = 0; i < shoreTriangles.Count; i += 3)
    oldFan.ClipTriangle(shoreTriangles[i], shoreTriangles[i + 1], shoreTriangles[i + 2], 0, oldFragments, true);
var leanSurface = new RoadSurface(junctionCenter, shoreCorners);
leanSurface.UseTerrain(shoreTriangles);
var leanFaces = new System.Collections.Generic.List<Vector3>();
leanSurface.AppendSurface(0, leanFaces);
Check(leanFaces.Count < oldFragments.Count, "Native Geländedreiecke dürfen nicht erneut am geschätzten Fächer vervielfacht werden.");
var oldSurface = new RoadSurface(junctionCenter, shoreCorners);
oldSurface.UseTerrain(oldFragments);
float largestDifference = 0; Vector3 differenceAt = Vector3.zero;
for (int x = -8; x <= 8; x++)
for (int y = -8; y <= 8; y++)
{
    var p = new Vector3(x / 10f, y / 10f, 100);
    float difference = Vector3.Distance(leanSurface.Project(p, .014f), oldSurface.Project(p, .014f));
    if (difference > largestDifference) { largestDifference = difference; differenceAt = p; }
}
Console.WriteLine($"Geländevergleich: maximale Abweichung {largestDifference} bei {differenceAt}.");
Check(largestDifference < .0003f, "Weniger Teilflächen müssen dieselbe native Geländeform bewahren.");
Console.WriteLine($"Geometrie-Regressionsprüfung: {oldFragments.Count / 3} auf {leanFaces.Count / 3} Bodenflächen im Ufer-Testfall, bei gleicher Geländeform.");

var joinMethod = runtimeMod.GetType("Roadlife.RoadVisuals").GetMethod("Join", BindingFlags.NonPublic | BindingFlags.Static);
Vector3 JoinRoad(Vector3 a, Vector3 b) => (Vector3)joinMethod.Invoke(null, new object[] { a, b });
var longBridge = new Vector3[62];
for (int i = 0; i < longBridge.Length; i++)
{
    float angle = i * .012f;
    longBridge[i] = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * 100.4f;
}
for (int i = 1; i + 1 < longBridge.Length; i++)
{
    var a = JoinRoad(longBridge[i - 1], longBridge[i]);
    var b = JoinRoad(longBridge[i], longBridge[i + 1]);
    for (int sample = 0; sample <= 20; sample++)
        Check(Mathf.Abs(Evaluate(a, longBridge[i], b, sample / 20f).magnitude - 100.4f) < .0002f,
            "Das lange Brückendeck darf nicht an jedem Feldmittelpunkt absinken oder ansteigen.");
    if (i + 2 < longBridge.Length)
    {
        Vector3 nextEnd = JoinRoad(longBridge[i + 1], longBridge[i + 2]);
        Check(Vector3.Distance(Evaluate(a, longBridge[i], b, 1), Evaluate(b, longBridge[i + 1], nextEnd, 0)) < .0001f,
            "Benachbarte Brückenfelder müssen exakt denselben Anschluss besitzen.");
        Check(Vector3.Dot((b - longBridge[i]).normalized, (longBridge[i + 1] - b).normalized) > .999f,
            "Die Fahrtrichtung darf am Feldübergang keinen sichtbaren Knick erhalten.");
    }
}
Console.WriteLine("Langbrücken-Regressionsprüfung: 60 Felder ohne periodische Höhenwellen oder getrennte Anschlüsse.");

var truckGetPos = game.Types.Single(x => x.Name == "CVehicle").Methods.Single(x => x.Name == "GetPos");
Check(truckGetPos.Parameters.Count == 1 && truckGetPos.Parameters[0].Name == "t"
    && truckGetPos.ReturnType.FullName == "UnityEngine.Vector3",
    "Der Höhen-Hook für native LKWs muss mit der installierten GetPos-Signatur übereinstimmen.");
Console.WriteLine("LKW-Höhen-Hook gegen die installierte Spielassembly geprüft.");

Check(Math.Abs(BridgeDesign.DeckHeight(1, 0, 100, 100, 100.2f) - 100.1f) < .00002f,
    "Eine kurze Brücke darf nicht auf die höhere Uferhöhe zu einem Buckel angehoben werden.");
for (int length = 1; length <= 120; length++)
for (int index = 0; index < length; index++)
{
    float radius = BridgeDesign.DeckHeight(length, index, 100, 100, 100.3f);
    Check(radius >= 100 && radius <= 100.3f,
        "Kurze vollständige Brücken müssen zwischen ihren beiden Uferhöhen bleiben.");
    Check(Math.Abs(BridgeDesign.DeckHeight(length, index, 100.006f, 100, 100) - 100.006f) < .00002f,
        "Gleich hohe Ufer dürfen keinen zusätzlichen Höhenbuckel erzeugen.");
    Check(BridgeDesign.DeckHeight(length, index, 100.006f, -1, 100.3f) == 100.006f
        && BridgeDesign.DeckHeight(length, index, 100.006f, 100.3f, -1) == 100.006f,
        "Offene Brückenteile müssen unabhängig vom verbundenen Ufer dieselbe Wasserhöhe verwenden.");
}
foreach (int length in new[] { 2, 4, 30, 120 })
foreach (bool downhill in new[] { false, true })
{
    float start = downhill ? 100.3f : 100.1f;
    float end = downhill ? 100.1f : 100.3f;
    float previousHeight = start;
    float step = (end - start) / (length + 1);
    for (int index = 0; index <= length; index++)
    {
        float height = index == length ? end : BridgeDesign.DeckHeight(length, index, 100, start, end);
        Check(Math.Abs(height - previousHeight - step) < .00002f,
            "Die Steigung muss in beiden Richtungen bis zu den Uferanschlüssen konstant bleiben.");
        previousHeight = height;
    }
}
for (int index = 0; index < 30; index++)
    Check(Math.Abs(BridgeDesign.DeckHeight(30, index, 100.006f, 99.9f, 100.3f)
        - (100.006f + (100.3f - 100.006f) * (index + 1f) / 31f)) < .00002f,
        "Wasserfreiheit darf keinen Knick innerhalb der Spannweite erzeugen.");
Console.WriteLine("Kurzbrücken- und Teilstückprüfungen für Uferinterpolation, minimale Wasserfreiheit und einheitliche offene Deckhöhen bestanden.");

var rampCorners = new[] { new Vector3(1, -1, 100), new Vector3(1, 1, 100),
    new Vector3(-1, 1, 100), new Vector3(-1, -1, 100) };
var straightRamp = new RoadSurface(new Vector3(0, 0, 100), rampCorners);
straightRamp.BridgeApproach(new Vector3(1, 0, 100.25f));
float prior = 100;
for (int sample = 0; sample <= 40; sample++)
{
    float radius = straightRamp.Project(new Vector3(sample / 40f, 0, 100), 0).magnitude;
    Check(radius >= prior - .0001f && radius <= 100.26f,
        "Die Auffahrt darf keinen zusätzlichen Hügel oberhalb ihrer festen Endhöhen erzeugen.");
    prior = radius;
}
Console.WriteLine("Auffahrtsprüfung für ein durchgehendes Profil ohne zusätzlichen Höhenbuckel bestanden.");

Check(!CoordinateBounds.Contains(-1, -1, 100, 100), "Gelöschte Brückennachbarn (-1,-1) dürfen kein Array adressieren.");
Check(!CoordinateBounds.Contains(-1, 0, 100, 100) && !CoordinateBounds.Contains(0, -1, 100, 100),
    "Jede negative Koordinatenachse muss vor dem Hex-Arrayzugriff abgewiesen werden.");
Check(!CoordinateBounds.Contains(100, 0, 100, 100) && !CoordinateBounds.Contains(0, 100, 100, 100),
    "Koordinaten jenseits des Weltendes müssen ebenfalls abgewiesen werden.");
Check(CoordinateBounds.Contains(0, 0, 100, 100) && CoordinateBounds.Contains(99, 99, 100, 100),
    "Gültige Randfelder müssen weiterhin erreichbar sein.");
Check(!CoordinateBounds.Contains(0, 0, 0, 0), "Eine geschlossene Welt darf keine gültigen Felder liefern.");
var bridgeType = runtimeMod.GetType("Roadlife.BridgeSpan");
var isBridgeMethod = bridgeType.GetMethod("IsBridge", BindingFlags.NonPublic | BindingFlags.Static);
var coordinateType = isBridgeMethod.GetParameters()[0].ParameterType;
var missingNeighbor = Activator.CreateInstance(coordinateType);
coordinateType.GetField("x").SetValue(missingNeighbor, -1);
coordinateType.GetField("y").SetValue(missingNeighbor, -1);
Check(!(bool)isBridgeMethod.Invoke(null, new[] { missingNeighbor }),
    "Die echte Brückensuche muss einen fehlenden Nachbarn ohne Ausnahme ablehnen.");
var meshHook = mod.Types.Single(x => x.FullName == "Roadlife.Plugin").Methods.Single(x => x.Name == "GrowingRoadMesh");
Check(!meshHook.Body.Instructions.Any(x => x.OpCode.Code == Mono.Cecil.Cil.Code.Stfld
    && x.Operand is FieldReference field && field.Name == "failed"),
    "Ein einzelner Meshfehler darf nicht den globalen Fehlerstatus setzen.");
Console.WriteLine("Lösch-Regressionsprüfungen für fehlende Nachbarn, Weltgrenzen und lokale Meshfehler bestanden.");

foreach (float slope in new[] { -2f, 0f, .15f, 2f })
foreach (float end in new[] { 99.7f, 100.3f })
{
    float previous = 100f;
    for (int sample = 0; sample <= 100; sample++)
    {
        float height = BridgeDesign.ApproachHeight(100f, end, slope, sample / 100f);
        Check(height >= Math.Min(100f, end) - .00003f && height <= Math.Max(100f, end) + .00003f,
            "Auffahrtsglättung darf bei extremen Küstensteigungen keine Überhöhung erzeugen.");
        Check((height - previous) * (end - 100f) >= -.00001f,
            "Auffahrtsglättung muss in beiden Richtungen monoton bleiben.");
        previous = height;
    }
    Check(Math.Abs(previous - end) < .00002f, "Die geglättete Auffahrt muss exakt am Brückenanschluss enden.");
}
Check(Math.Abs(BridgeDesign.ApproachHeight(0f, 1f, 0f, .001f)) < .00001f,
    "Eine flache Küste muss ohne abrupten Steigungswechsel in die Rampe übergehen.");
Check(Math.Abs((1f - BridgeDesign.ApproachHeight(0f, 1f, 0f, .999f)) / .001f - 1f) < .01f,
    "Die Auffahrt muss die lineare Brückensteigung am Anschluss erreichen.");
Console.WriteLine("Küstenauffahrten: monotone Profile und stetiger Steigungsanschluss bestanden.");var wholeApproach = new RoadSurface(new Vector3(0, 0, 100), rampCorners);
wholeApproach.BridgeApproach(new Vector3(1, 0, 100.25f), fullTile: true);
Check(Vector3.Distance(wholeApproach.Project(new Vector3(-1, 0, 100), 0), new Vector3(-1, 0, 100)) < .0002f,
    "Die verlängerte Auffahrt muss am landseitigen Rand die ursprüngliche Höhe behalten.");
Check(wholeApproach.Project(new Vector3(0, 0, 100), 0).magnitude > 100.05f,
    "Der Übergang muss bereits vor dem Landfeldzentrum beginnen.");
float previousRampHeight = 100f;
for (int sample = 0; sample <= 80; sample++)
{
    float height = wholeApproach.Project(new Vector3(-1f + sample / 40f, 0, 100), 0).magnitude;
    Check(height >= previousRampHeight - .0001f && height < 100.27f,
        "Die gesamte Küstenauffahrt muss ohne zusätzliche Kuppe durchgehend ansteigen.");
    previousRampHeight = height;
}
Console.WriteLine("Auffahrt über das gesamte Anschlussfeld geprüft.");var coastalTiles = new RoadSurface[3];
for (int tile = 0; tile < 3; tile++)
{
    float x = tile;
    coastalTiles[tile] = new RoadSurface(new Vector3(x, 0, 100), new[] {
        new Vector3(x-.5f,-1,100),new Vector3(x+.5f,-1,100),
        new Vector3(x+.5f,1,100),new Vector3(x-.5f,1,100) });
    coastalTiles[tile].CoastalApproach(new Vector3(0,0,100),new Vector3(2.5f,0,100.5f));
}
for (int tile = 0; tile < 2; tile++)
for (int lane = -2; lane <= 2; lane++)
{
    var seamPoint = new Vector3(tile+.5f,lane*.12f,100);
    Check(Vector3.Distance(coastalTiles[tile].Project(seamPoint, .014f),
        coastalTiles[tile+1].Project(seamPoint, .014f)) < .0002f,
        "Drei Küstenfelder müssen an gemeinsamen Spurkanten dieselbe Höhe liefern.");
}
Check(Vector3.Distance(coastalTiles[0].Project(new Vector3(0,0,100),0),new Vector3(0,0,100)) < .0002f,
    "Das dritte Landfeld muss am gemeinsamen Profilanfang seine Geländehöhe behalten.");
Console.WriteLine("Gemeinsames Drei-Felder-Küstenprofil mit identischen Fahrbahnnähten geprüft.");var matchedEdge = new RoadSurface(new Vector3(0,0,100), rampCorners);
matchedEdge.CoastalApproach(new Vector3(-1,0,100),new Vector3(1,0,100.1f));
var raisedSeam = new Vector3(1,0,100.3f);
matchedEdge.MatchBridgeEdge(raisedSeam);
for(int lane = -2; lane <= 2; lane++)
{
    var edgePoint = new Vector3(1,lane*.12f,100);
    float expectedRadius = raisedSeam.magnitude / Vector3.Dot(edgePoint.normalized,raisedSeam.normalized);
    Check(Math.Abs(matchedEdge.Project(edgePoint,0).magnitude-expectedRadius)<.0002f,
        "Beide Spurränder müssen exakt die gemeinsame Brückenanschlussebene erreichen.");
}
Console.WriteLine("Brückenanschlussebene über alle vier Spuren geprüft.");var clippedEdgeSurface = new RoadSurface(new Vector3(0,0,100), rampCorners);
var offVertexSeam = new Vector3(.73f,0,100.25f);
clippedEdgeSurface.MatchBridgeEdge(offVertexSeam);
var clippedEdgeVertices = new System.Collections.Generic.List<Vector3>();
clippedEdgeSurface.ClipTriangle(new Vector3(.73f,-.24f,100.25f),new Vector3(.73f,.24f,100.25f),
    new Vector3(.3f,0,100),.014f,clippedEdgeVertices);
int seamVertices = 0;
foreach(var vertex in clippedEdgeVertices)
{
    float projectedX = vertex.x * 100f / vertex.z;
    if(Math.Abs(projectedX-.73f*100f/100.25f)>.00002f) continue;
    seamVertices++;
    float expected = offVertexSeam.magnitude / Vector3.Dot(vertex.normalized,offVertexSeam.normalized) + .014f;
    Check(Math.Abs(vertex.magnitude-expected)<.00003f,
        "Neu zugeschnittene Nahtpunkte müssen inklusive Asphaltabstand exakt auf dem Anschluss liegen.");
}
Check(seamVertices >= 2,"Der Nahttest muss tatsächlich neu erzeugte Randpunkte prüfen.");
Console.WriteLine("Endgültige Clip-Nahtpunkte einschließlich Asphaltabstand geprüft.");var firstRoadEdge = new RoadSurface(new Vector3(0,0,100),rampCorners);
var secondRoadEdge = new RoadSurface(new Vector3(0,0,100.2f),new[] {
    new Vector3(1,-1,100.2f),new Vector3(1,1,100.2f),
    new Vector3(-1,1,100.2f),new Vector3(-1,-1,100.2f) });
var sharedRadii = new[] {100.25f,100.23f,100.21f,100.24f,100.26f};
var sharedCenter = new Vector3(0,0,100.2f);
firstRoadEdge.MatchRoadEdge(sharedCenter,Vector3.right,sharedRadii);
secondRoadEdge.MatchRoadEdge(sharedCenter,Vector3.right,sharedRadii);
for(int sample=-24; sample<=24; sample++)
{
    var point = sharedCenter + Vector3.right*(sample/100f);
    Check(Vector3.Distance(firstRoadEdge.Project(point,.014f),secondRoadEdge.Project(point,.014f))<.00003f,
        "Unterschiedliche Geländeflächen müssen über den gesamten gemeinsamen Querschnitt identische Endhöhen liefern.");
}
var unequalA = new Vector3(0,0,100);
var unequalB = new Vector3(1,0,100).normalized*100.5f;
var canonicalJoin = (Vector3)joinMethod!.Invoke(null,new object[]{unequalA,unequalB})!;
var equalHeightJoin = (Vector3)joinMethod!.Invoke(null,new object[]{unequalA,unequalB.normalized*100})!;
Check(Vector3.Distance(canonicalJoin.normalized,equalHeightJoin.normalized)<.0000001f,
    "Die Anschlussrichtung darf nicht von den Höhen beider Straßenfelder abhängen.");
Console.WriteLine("Gemeinsame Landstraßenquerschnitte und höhenunabhängige Anschlussrichtung geprüft.");

var railColorMethod = runtimeMod.GetType("Roadlife.RoadVisuals").GetMethod("IsRailColor", BindingFlags.NonPublic | BindingFlags.Static);
var runtimeGame = Assembly.LoadFrom(Path.Combine(gamePath, "Plan B Terraform_Data", "Managed", "Assembly-CSharp.dll"));
var wayColors = runtimeGame.GetType("GWays");
foreach (var name in new[] { "colorRailMain", "colorRailSide", "colorRailUp", "colorRoad", "colorRoadYellow", "colorWhite" })
{
    var nativeColor = wayColors.GetField(name).GetValue(null);
    var linear = nativeColor.GetType().GetProperty("ColorLinear").GetValue(nativeColor);
    bool kept = (bool)railColorMethod.Invoke(null, new[] { linear });
    Check(kept == name.StartsWith("colorRail"), "Bahnübergang: Schienenflächen müssen erhalten, native Straßenflächen und Markierungen ausgeschlossen werden: " + name);
}
Console.WriteLine("Bahnübergang: Schienenfilter gegen die sechs nativen Spielfarben geprüft.");
