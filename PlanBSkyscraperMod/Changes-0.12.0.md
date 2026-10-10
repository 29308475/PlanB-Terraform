# Änderungen 0.11.0 → 0.12.0

Die folgenden Diff-Ausschnitte zeigen die wesentlichen Änderungen. Sie sind
Review-Auszüge und kein vollständig anwendbarer Patch; die Dateien sind bereits geändert.
Das Workspace-Verzeichnis enthält kein Git-Repository.

## ReferenceDesign.cs

~~~diff
- Names: Glaskuppel, Kuppelgruppe, Dreierblock, Fussturm, Gebaeudescheibe, Bogenhaus
+ Count = 5
+ Names: CurvedSkyscraper, TripleServerHall, TriangularHQ, SteppedTransmitter, GeodesicDome
+ Eigene Strukturmethoden für jedes Modell
+ Concave Profile werden mit Ear-Clipping trianguliert
+ Normierung von Höhe, Boden und maximalem XZ-Radius auf 0.46
+ Select: Hausplätze → Hall/Dome; Turmplätze → Curved/HQ/Transmitter
~~~

## VariantFactory.cs

~~~diff
- Get(CAssetModels models, int variant)
+ Get(CAssetModels models, int variant, float tileInradius)
- new CAssetModel[VariantRules.Count + 30]
+ new CAssetModel[VariantRules.Count + ReferenceDesign.Count * 5]
+ NativePalette.Flat(info) für neue Referenzmodelle
+ FitToTile(mesh, tileInradius): proportionales XZ-Einpassen auf 92% des Inkreisradius
+ Endliche Vertexkoordinaten und Radius werden zur Laufzeit geprüft
+ Cache berücksichtigt Tile-Radius
~~~

## Plugin.cs

~~~diff
- BepInPlugin(..., "0.11.0")
+ BepInPlugin(..., "0.12.0")
- ReferenceAssets.Share = 35
+ ReferenceAssets.CustomBuildingPercent = 100
- motif = hash % 3; if (tower) motif += 3;
+ motif = ReferenceDesign.Select(tower, hash);
+ tileInradius = kürzester gültiger Hex-Nachbarabstand / 2
~~~

## NativePalette.cs

~~~diff
+ Flat(Action<string> info)
+ Shader: Universal Render Pipeline/Unlit oder Unlit/Color
+ Feste Pastellfarben: Weiß, Cyber-Blau, Grautöne, Hellgrün, Warngelb
+ Unbeleuchtete, opake Materialien ohne Atlas oder transparente Innenräume
~~~

## Checks/AssetChecks.cs

~~~diff
+ ValidateReferences() läuft auch ohne Export
+ 500 <= tatsächliche Dreieckszahl <= 1500
+ Individuelle Dreiecke auf Degeneration geprüft
+ Hex-Halbebenen bei Rotationen in 15-Grad-Schritten geprüft
+ Eindeutige Meshes, endliche Werte, Höhe 1 und Boden 0
+ Haus-/Turmauswahl überprüft
- Export: sechs Referenzmodelle
+ Export: fünf Referenzmodelle mit flat-Flag und Dreieckszahl
~~~

## Checks/Gallery.template.html und reference-preview-check.cjs

~~~diff
+ Fünf größere Vorschaukarten; flache Pastellfarben ohne Beleuchtungssimulation
+ Dreieckszahl je Modell sichtbar
- Browserprüfung erwartet sechs Karten
+ Browserprüfung erwartet fünf Karten
~~~

## PlanBSkyscraperMod.csproj und README.md

~~~diff
- Version 0.11.0
+ Version 0.12.0
+ Paketbeschreibung, Modelle, Dreieckszahlen, Auswahl, Hex-Anpassung,
+ Unlit-Materialien, Konfiguration und Installationsweg dokumentiert
~~~

## Validierung

CurvedSkyscraper 672; TripleServerHall 1296; TriangularHQ 512;
SteppedTransmitter 728; GeodesicDome 1046 Dreiecke.
Geometrie-, Auswahl-, Skyline- und Browserprüfungen bestanden.
Release-Build: keine Warnungen oder Fehler. Darstellung im Spiel noch offen.
