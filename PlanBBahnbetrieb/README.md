# Güterbahnhof – Version 0.2.4

Der Güterbahnhof ist ein eigenes Gebäude im normalen Transport-Baumenü. Er enthält zwei native Durchfahrtsgleise und vier interne Warenpuffer mit je 40 Einheiten. Separate Depotgebäude werden nicht mehr gesetzt. Das gelieferte GLB-Modell bleibt unverändert.

## Bedienung

1. Im normalen Baumenü unter Transport den **Güterbahnhof** auswählen. Schienen und Depots mit Reichweite 3 müssen freigeschaltet sein.
2. Die gesamte Modellvorschau auf freiem, trockenem, ebenem Boden platzieren. Die normale zweite Gebäudeaktion dreht den Bahnhof in sechs Schritten. Der Bau kostet 18 Schienen und vier Depots mit Reichweite 3.
3. Die beiden Gleise an ihren Enden anschließen. Zuglinien mit dem normalen Zug-Werkzeug an den Gleismittelpunkten einrichten. Die vorhandene Zuglogik bleibt zuständig; jede Linie befördert wie im Grundspiel eine Ware.
4. **Die Bahnhofshalle anklicken.** Das normale Auswahlfenster rechts zeigt vier anklickbare Warenfelder im Depotstil mit Ware, Bestand und Kapazität sowie der nativen Versorgung als Backup. Ein Klick auf einen Puffer öffnet die Warenauswahl des Spiels.
5. Anlieferung und Abholung übernehmen vier native Drohnen. Alle vier Puffer erreichen beide Haltepunkte. Externe Depots können die internen Stapel über die Bahnhofsbaufläche erreichen.

Der separate Bildschirmbutton zum Bauen entfällt. Der Personenbahnhof und Reisende gehören zur Mod Stadtleben.

## Spielstände und bestehende Bahnhöfe

Gebäude, Orientierung, Baufläche, vier Warenstapel und Drohnen werden im normalen Spielstand gespeichert. Der Bahnhof wird aus diesen Daten auch ohne Zusatzdatei wieder erkannt. Die Mod muss für Spielstände mit dem eigenen Gebäudetyp installiert bleiben.

Die Zusatzdatei `<Spielstand>.bahnbetrieb.json` bleibt für die Zuordnung und insbesondere das Umstellen alter Bahnhöfe aus 0.1.0 erhalten. Alte Zusatzdateien werden anhand von Dateigröße und Zeitstempel geprüft; der temporäre Initialisierungs-Seed ist kein geeignetes Merkmal. Neue Zusatzdateien (Format 3) enthalten einen SHA-256-Fingerabdruck des Spielstands und vertragen geänderte Dateizeitstempel beim Kopieren. Veraltete Zusatzdateien werden übersprungen, ohne bereits aus dem nativen Spielstand geladene Bahnhöfe zu entfernen. Alte Bahnhöfe werden beim Laden ohne neue Baukosten auf ein Bahnhofgebäude mit internen Puffern umgestellt; Warenbestände und Gleise bleiben erhalten. Alte Spielstände und ihre Zusatzdateien werden erst durch einen späteren Speichervorgang aktualisiert.

Zum Abreißen die Halle mit dem normalen Abrisswerkzeug auswählen. Vorher Züge auf betroffenen Linien entfernen. Die Bahnhofsgleise sind gegen einen versehentlichen Einzelabriss geschützt; das Entfernen des Bahnhofs räumt die ganze Baufläche auf. Waren werden nach der nativen Spielmechanik abgelegt.

## Build und Installation

```powershell
dotnet build -c Release
.\Install.ps1
```

Installationsziel: `BepInEx/plugins/PlanBBahnbetrieb/PlanBBahnbetrieb.dll`. BepInEx 5 ist erforderlich. Der native Mods-Menüadapter der Produktionsketten-Mod zeigt den Plugin-Eintrag an.

## Prüfung und Grenzen

```powershell
dotnet run --project Checks -c Release
```

Geprüft werden Modellimport, Material- und Indexdaten, beschädigte GLBs, eingebettete Ressourcen, Rastergeometrie in zwölf Orientierungen, durchgehende Gleise, Pufferreichweite, vier native Depotstapel, Zuordnung der vier Drohnen, Abgrenzung gegenüber anderen Gebäuden und die Signaturen der Harmony-Hooks.

Die tatsächliche Menüanzeige, Auswahl der Halle, Warentransporte auf beiden Zuglinien, Übernahme alter Spielstände und ein vollständiger Speichern/Laden-Durchlauf benötigen einen Test im laufenden Spiel. Kran und Container des gelieferten Modells sind weiterhin statisch.

Version 0.2.2: Große Bahnhofsflächen sind eigene Inhaltszellen. Sie verwenden keine nativen Sieben-Hex-Nachbarzellen, deren Richtungsdaten mit den vollständigen Bahnhofkoordinaten inkompatibel sind. Hallenauswahl und Drohnenzugriff werden über gezielte verwaltete Hooks zum Anker geleitet.

Version 0.2.3: Das zusätzliche mittige Fenster entfällt; Waren werden in vier Kopien der nativen Depot-Warenzeile gewählt. Depotreichweite und Backupversorgung einschließlich der normalen zweiten Gebäudeaktion werden angezeigt. Bei der Prioritätsberechnung werden eigene Flächenzellen übersprungen und Nachbarn nur einmal berücksichtigt. Die vier Warenpuffer erhalten getrennte, an der nativen Depotlogik orientierte Prioritäten. Das Baumenü verwendet den Rahmen der übrigen Einträge; der Vorschauhintergrund wird im UI-Sprite transparent dargestellt.

Version 0.2.4: Kopieren und Verschieben erfolgen über die normalen Auswahlwerkzeuge. Kopien übernehmen Warenwahl und Backupversorgung, aber keine Warenbestände. Verschieben erhält die Bestände; Bahnhöfe mit Zügen auf ihren Linien müssen zunächst von diesen Zügen befreit werden. Drehung, Spiegelung und gemeinsame Gebäudefläche werden in der Vorschau berücksichtigt.

Der Bahnhof kann auf passenden vorhandenen geraden Schienen errichtet werden. Nur fehlende Schienen und vier Depots werden berechnet. Vorhandene Haltepunkte bleiben erhalten, sofern sie in der Gleismitte liegen. Gebäude, Querstrecken und nahe Abzweigungen werden abgewiesen. Beim Verschieben und Abriss bleibt die darunterliegende Schieneninfrastruktur samt Routen erhalten; nach dem Abriss können die Gleise separat entfernt werden.
Version 0.2.5: Die Eingangstreppe des Güterbahnhofs reicht von der Bodenplatte (0,18) bis zur Bahnsteigoberfläche (0,68). Die ursprüngliche oberste Stufe endete bei 0,46. Geändert sind ausschließlich die Höhen der Treppenvertices im eingebetteten GLB; das Original liegt in Inspection/06_freight_station-before-stairs.glb.
