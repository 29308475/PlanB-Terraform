# PlanBSkyscraperMod 0.15.3

Die geneigten Dachabschlüsse der Entwürfe 01,03,08,12,19,24 erhalten echte
segmentierte Rundungen nach dem Konturprinzip des GLB-Turms. Acht Segmente pro
Dachrichtung und eine weich auslaufende Kurve ersetzen die lineare Schräge.
Obere Fassade, Laibungen, Rahmen und Dachrand werden zusammen angepasst.
Terrassen bleiben eben, Proportionen und normierte Skyline-Höhen erhalten.
Die gelieferten GLBs selbst werden nicht verändert. Darstellung im Spiel prüfen.

# PlanBSkyscraperMod 0.15.2

Materialabgleich nach Spiel-Screenshot: Keramikflächen sind um 22 Prozent
in linearen Farbwerten gedämpft; Rahmen um 10, Metall um 20 und Laibungen um 35
Prozent. Glas hat einen satteren blauen Farbton. Beide Modellfamilien verwenden
denselben Abgleich. Original-GLBs, Geometrie und PBR-Quelldaten bleiben unverändert.

ReferenceAssets.DomeKeepPercent (Standard 30) lässt nur 30 Prozent der bislang
gewählten Kuppelplätze als Kuppel erscheinen; die übrigen behalten das native
Haus. Somit etwa 70 Prozent weniger Kuppeln, ohne stattdessen mehr Dreierhallen
zu wiederholen. Gebäudeauswahl bleibt deterministisch. Dachneigungen und Skyline
bleiben erhalten. Farbwirkung muss im Spiel nach Neustart geprüft werden.

# PlanBSkyscraperMod 0.15.1

Geneigte Dachabschlüsse bei 01 Atrium, 03 Doppelfluegel, 08 Zwillingspaar,
12 Versatzhaus, 19 Himmelsbruecke und 24 Doppeldach. Breite Pultdächer mit
teilweise entgegengesetzter Neigung pro Flügel. Dachflächen, obere Fassade,
Laibungen und Rahmen werden gemeinsam verformt. Keine Spitzen oder Pyramiden.
Terrassen bleiben flach; Dachtechnik sitzt ausschließlich auf ebenen Flächen.
Alle Modelle bleiben auf Höhe 1 normiert und folgen den bisherigen Skyline-Bändern.

# PlanBSkyscraperMod 0.15.0

Die bisherigen 25 Entwürfe verwenden nun die gleichen PBR-Materialquellen wie
Detailset_b: ceramic (Wand), glass, edge (Rahmen), metal (Dächer), recess
(Laibungen) und yellow. Die Materialerzeugung ist für GLBs und prozedurale
Modelle gemeinsam. Fenstervertiefungen sind verstärkt, Querstreben sitzen
innerhalb der Öffnungen und Dachaufbauten tragen Lüftungslamellen. Die 25
Grundformen, normierte Bodenlage und bisherigen Skyline-Höhen bleiben erhalten.

ReferenceAssets.DetailSetPercent (Standard 65) mischt innerhalb neuer
Hochhausplätze 65 Prozent importierte GLBs mit 35 Prozent überarbeiteten
Entwürfen. Der frühere CustomBuildingPercent-Schlüssel wird nicht mehr verwendet.
Hausplätze verwenden weiterhin die kompakten importierten GLBs. Mix.NewBuildingPercent
bestimmt den Anteil neuer Gebäude insgesamt. Vorschau und OBJ-Exporte der 25
Modelle sind aktualisiert. Keine Änderung der Produktions-/Bevölkerungsregeln.

## Update 0.14.1: Detailset_b

Aktives Set: Assets/Reference/Gebaeude_GLB_Detailset_b. Es ersetzt das bisherige
Detailset in den eingebetteten GLB-Ressourcen. Neue Fenstervertiefungen, Laibungen
und Fenster auf Vorder-/Rückseiten und Flanken werden als Originalgeometrie
übernommen. Keine Reduktion der gelieferten Meshes. Dreiecke: 1970, 1721, 2013,
991, 2198. Die Kuppel ist gegenüber dem vorherigen Detailset unverändert.
Proportionen, Normalen und PBR-Materialien behalten den bisherigen Importweg.

## Detailset ab 0.14.0

Aktive Dateien stammen aus Assets/Reference/Gebaeude_GLB_Detailset:
01_stepped_tower, 02_three_module_complex, 03_buttressed_reactor,
04_antenna_spire, 05_geodesic_dome. Dreieckszahlen: 632, 828, 1084, 396, 2198.
Das vorherige GLB-Set ist ersetzt. Y-up wird beibehalten; die Z-Spiegelung und
umgekehrte Dreiecksreihenfolge übertragen glTFs Rechtshändigkeit nach Unity.
Gültige Originalnormalen werden geladen und nicht neu berechnet. Die Kuppel
enthält 78 Null-Normalen; diese werden aus angrenzenden Flächen ergänzt. Die sechs benannten
Materialien erhalten ihre PBR-Farbwerte, Metallanteile, Rauheit und Culling.
Das Set enthält keine Texturen, keine Normalmaps und keine transparente Verglasung.
Geometrie und Größenverhältnisse werden unverändert übernommen; lediglich ein
gleichmäßiger Größenfaktor und die Bodenverschiebung passen das Modell an.
Shader und Beleuchtung des Spiels beeinflussen das endgültige Aussehen.

# PlanBSkyscraperMod 0.14.0

Fünf gelieferte GLB-Dateien werden direkt aus eingebetteten Plugin-Ressourcen
importiert: curved_skyscraper, modular_complex, research_monument, spire_tower
und geodesic_dome. Die ursprüngliche Mesh-Geometrie und die Vertexfarben bleiben
erhalten. Ein begrenzter GLB-2-Leser unterstützt die tatsächlichen statischen,
ungepackten Dateien; fremde Animationen, Draco und Node-Transformationen werden
abgewiesen. Newtonsoft.Json ist bereits im Spiel vorhanden.

Z-up wird mit einer echten Achsrotation in Y-up umgewandelt. Die Mesh-Unterkante
steht auf dem lokalen Spawn-Boden. Alle drei Achsen verwenden denselben Faktor;
der äußerste Punkt bleibt innerhalb von 90 Prozent des Hex-Inkreisradius.
Skyline-Bänder verkleinern hohe Modelle gleichmäßig; sie strecken keine Geometrie.
Halle und Kuppel bleiben in ihrer natürlichen Form. Breite Modelle können deshalb
niedriger als die bisherigen Hochhaus-Mindesthöhen sein.

Die Einzelteile werden in je ein Mesh mit Farb-Submeshes zusammengefasst.
Native URP/Lit-Materialien erhalten die Originalfarben und räumliche Schattierung.
Die Kuppel bleibt bei 5232 Dreiecken; die anderen Modelle haben 168,180,240,168.
Keine automatische Vereinfachung und keine Produktions- oder Bevölkerungsänderung.
Die Modelle ersetzen visuelle Wohngebäude in fortgeschrittenen Stadtvierteln.

ReferenceAssets.CustomBuildingPercent=100 wählt die GLBs für alle neuen Modelle;
Mix.NewBuildingPercent=65 lässt native Gebäude daneben bestehen. Seed und Skyline
bleiben erhalten. Bei einem Importfehler wird das native Modell verwendet und
der Fehler im BepInEx-Log protokolliert.

Assets/Reference/Referenzvorschau.html zeigt jetzt die tatsächlichen importierten
GLBs. Checks prüfen Parser, Topologie, Farben, Boden, Seitenverhältnisse und Hexfit.
Zum Aktualisieren: dotnet run --project Checks/Checks.csproj -c Release -- --export Assets
Alternativ: node Checks/glb-preview.cjs
Build und Installation: powershell -ExecutionPolicy Bypass -File Install.ps1
Nach Installation das Spiel neu starten. Materialdarstellung und Pooling im Spiel
müssen noch geprüft werden. Das BepInEx-Paket enthält die GLBs in der DLL;
es sind keine zusätzlichen Dateien im Spielverzeichnis nötig.


Version 0.15.4: Modellgrößen werden für die Zwischenspeicherung in 1%-Stufen nach unten gerundet. Kleine Abweichungen des Hexabstands durch Kugelgeometrie und Gelände erzeugen dadurch keine neue Modellserie für nahezu jede Stadtzelle. Zwischengespeicherte Modelle bleiben innerhalb ihrer Bauzelle; die maximale Verkleinerung liegt unter 1%. Dies behebt die wiederholte Modellerzeugung, die im Fehlerprotokoll vom 08.10.2026 die Ansicht auf etwa 7 FPS verlangsamte.
