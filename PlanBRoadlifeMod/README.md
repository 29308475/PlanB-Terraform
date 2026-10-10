# Stadtleben – Plan B: Terraform

Stadtleben ist der neue Name von Roadlife und das geplante Dach für Stadtbelebung. Aktuell enthält Stadtleben die bisherigen Roadlife-Funktionen. Technische Kennung, DLL und Konfigurationspfad bleiben zur Kompatibilität erhalten. Weitere Module sind zunächst Konzepte.

Eigenständiges Projekt für dekorativen Verkehr auf den vom Spieler gebauten Straßen.

## Ziel

Zusätzliche Fahrzeuge sollen Straßen unabhängig von LKW-Transportaufträgen beleben.
Sie benötigen keine Waren, Depots oder Transportlinien. Die örtliche Verkehrsdichte
kann die Fahrgeschwindigkeit der Transport-LKWs begrenzen.
Straßen nahe größerer Städte erhalten vier sichtbare Fahrspuren und einen
Stadtboden-Hintergrund. Verkehrsdichte und Wachstumsschwellen sind einstellbar.

Bahnübergänge behalten die Straßenbreite der Umgebung. Straßenanschlüsse werden
nur aus Straßennachbarn gebildet; die nativen Schienen werden getrennt anhand
der Gleisnachbarn übernommen. Die Geometrie bleibt im bestehenden Mesh-Cache.

## Lokale Untersuchung

Die lokal installierte Spielassembly enthält bereits dekorativen Stadtverkehr:

- `SEnvironmentFXVehicles` lädt Modelle aus `GEnvironmentFX.vehiclesAssetModel.models`.
- `CFxVehicleModel` enthält ein GameObject, ein Mesh und Materialien.
- Das Spiel zeichnet diese Fahrzeuge mit `Graphics.DrawMesh`.
- `CFxVehicle` enthält Position, Fahrfortschritt, Geschwindigkeit und Kurvenanker.
- Das bestehende System erzeugt Verkehr auf Stadtflächen. Für gebaute Straßen
  braucht Roadlife eine eigene Erkennung und Wegführung.

Damit ist eine Wiederverwendung der im laufenden Spiel geladenen Fahrzeugmodelle
technisch plausibel. Verfügbare Varianten, Größe, Ausrichtung und Darstellung auf
Straßen müssen noch im Spiel geprüft werden. Spielassets werden nicht kopiert oder
mit der Mod verteilt.

`Inspection/` enthält lokale dekompilierte Referenzen für die weitere Entwicklung.
Diese Dateien sind kein Mod-Quellcode und werden nicht veröffentlicht.

## Stand

Version 0.4.8: BepInEx-5-Prototyp, kompiliert gegen die lokale Spielinstallation.

## Automatischer Brückenersatz ab 0.4.0

`Bridges.ReplaceExisting = true` ersetzt die Darstellung vorhandener reiner
Straßenbrücken automatisch, auch außerhalb großer Städte. Normale Brückensegmente
mit zwei Anschlüssen erhalten ein graues Deck mit Unterseite, helle Geländer und
Geländerpfosten. Ab 0.4.1 erhält die zusammenhängende Spannweite ein zentrales
Paar paralleler Pylone seitlich der Straße, mit oberem Querträger, dunklen
Tragkabeln und senkrechten Hängern. Die Pylone reichen vom Untergrund bis deutlich
über die Fahrbahn. Ihre Höhe liegt zwischen 0,65 und 1,6 Weltkoordinateneinheiten.
Die Form orientiert sich an einer modernen Hängebrücke; es ist kein maßgetreues
Golden-Gate-Modell. Kurven, Höhen und
Straßenbreiten werden aus dem vorhandenen Straßennetz übernommen. Kleine Straßen
behalten zwei sichtbare Spuren; ausgebaute Stadtstraßen erhalten vier.
Brückenkreuzungen behalten vorerst die einfache Straßenfläche.
Die Spannweitensuche endet an Abzweigungen oder nach höchstens 256 Feldern.
Ab 0.4.4 verwendet jede Spannweite eine gemeinsame radiale Deckhöhe, mindestens
so hoch wie die ursprünglichen Brückenfelder und ihre beiden Uferanschlüsse.
Unterschiedliche Untergrundhöhen erzeugen damit keine Wellen im Brückendeck.
Gemeinsame Anschlüsse berücksichtigen die Kugeloberfläche ohne Absinken der
Mittelpunktsehne. Roadlife-Fahrzeuge und die Positionen nativer Straßen-LKWs
einschließlich ihrer Ladung werden auf dieselbe Fahrbahnhöhe gelegt. Die native
Route und deren Fortschritt werden weiterhin vom Spiel berechnet.
Ab 0.4.5 verwenden kurze vollständige Brücken (bis vier Wasserfelder) interpolierte
Uferhöhen statt der maximalen Höhe. Offene Enden und isolierte Brückenteile werden
ebenfalls erfasst und verwenden die Wasseroberfläche als Höhendatum mit geringer
Fahrbahnfreiheit. Beim Weiterbauen wird die gesamte betroffene Spannweite aus dem
Geometriecache entfernt; sichtbare Modelle werden mit höchstens zwei Feldern pro
Frame zur Aktualisierung vorgemerkt. Dadurch bleiben alte Höhen und Pylonenstationen
nicht dauerhaft in entfernten Teilstücken stehen.
Ab 0.4.6 interpoliert die Auffahrt zwischen festen Anfangs- und Endhöhen, statt
jedes Geländedreieck unabhängig zur Brücke anzuheben. Die Fahrbahn bleibt dabei
über dem Gelände. Gerade, gleichartige Brückenfragmente über einer Baulücke von
höchstens sieben Wasserfeldern teilen sich ein Höhen- und Tragwerkslayout. Die
fehlenden Felder werden weder bebaut noch überbrückt; sie dienen nur der Planung
der Darstellung. Kurven, Abzweigungen, fremde Inhalte oder Land beenden die Suche.
Ab 0.4.7 werden fehlende Nachbarn und Koordinaten außerhalb der Welt vor jedem
Hex-Arrayzugriff geprüft. Ein Straßenmeshfehler setzt keinen globalen Fehlerstatus
mehr: Nur das betreffende Feld verwendet vorübergehend die native Darstellung.
Nach einer Feld- oder Spannweitenänderung wird es erneut versucht.
Landzufahrten direkt neben Brücken werden auch außerhalb des Stadtbereichs
ersetzt, damit die Rampenkorrektur dort angewendet wird.

Das eigene modulare 3D-Asset [BridgeModule.obj](Assets/BridgeModule.obj) ist in der
DLL eingebettet. Es wird einmal geladen und in die vorhandenen Straßenmeshes
eingearbeitet. Ein Unity-Editor oder ein externes AssetBundle ist dafür nicht
erforderlich; es gibt keine zusätzlichen GameObjects pro Geländer oder Pfeiler.
Transportwege und das Bauangebot bleiben erhalten. Die Darstellung im Spiel und
ihre Leistung müssen noch geprüft werden.
Version 0.3.0 wurde im Spiel geladen. Screenshots zeigten verdeckte Straßenstücke
und gekreuzte Fahrbahnmarkierungen. Version 0.3.1 korrigiert diese Ursachen; die
Darstellung benötigt eine erneute Sichtprüfung. `PlanBProductionChains` bleibt unverändert.

- Erkennt sichtbare Straßen anhand von `CItem_Way.Type.Road`.
- Verwendet im Spiel geladene Stadtfahrzeugmodelle ohne eigene Assetkopien.
- Zufällige Weiterfahrt an Kreuzungen; Umkehr an Sackgassen.
- Standard: Grunddichte 0,12 Fahrzeuge pro Straßenfeld, maximal 120 sichtbare Fahrzeuge.
- Größere Städte erhöhen die Dichte logarithmisch, maximal auf 0,35 pro Feld.
  Der Einfluss nimmt innerhalb von 30 Weltkoordinaten vom Stadtzentrum ab.
  Überlappende Städte verwenden den stärksten Einfluss; er wird nicht aufsummiert.
- Fahrzeuge erscheinen bevorzugt auf Straßen mit höherer örtlicher Dichte.
- Positionen berücksichtigen die vom Spiel berechnete Brückenhöhe über Gelände und Eis.
- LKWs erhalten eine örtliche Geschwindigkeitsobergrenze: bei maximaler Dichte
  höchstens 25 Prozent unter ihrer normalen Höchstgeschwindigkeit. Normale Bremsung,
  Beladung und Abstandsregeln bleiben wirksam. Züge und Schiffe sind ausgenommen.
  Dies ist ein Dichtemodell, keine Kollisionserkennung mit einzelnen Autos.
  Der LKW-Einfluss gilt auch außerhalb der Kamera und bei ausgeschalteten FX.
- Blendet Verkehr bei weiter Kameradistanz oder deaktivierten Spiel-FX aus.
- Entfernt Fahrzeuge nach Straßenabriss und beim Verlassen der Spielansicht.
- Keine Waren, Transportlinien oder persistierten Fahrzeuge.
- Ab 10.000 Einwohnern werden Straßen innerhalb der geschätzten Stadtfläche plus
  sechs Weltkoordinaten Rand auf vier sichtbare Spuren verbreitert.
  Die Schätzung folgt der Zahl der tatsächlichen Stadtfelder.
- Unter 8.000 Einwohnern erfolgt der Rückbau. Dazwischen bleibt die vorherige
  Stufe erhalten. Die Stufe gilt für die laufende Sitzung; nach Neuladen wird sie
  aus der aktuellen Bevölkerung bestimmt.
- Weiße Fahrbahnmarkierungen trennen je zwei Spuren pro Richtung. Roadlife-Fahrzeuge
  wählen beim Erscheinen eine innere oder äußere Spur.
- Der Hintergrund färbt das vollständige Straßenfeld grau ein, bis zu seinen
  Grenzen im Sechseckraster. Er wird zusammen mit der Straße gezeichnet und benötigt
  kein geladenes Stadtfeld.
  Der Farbton ist ein einheitlicher Betonfarbton; die native Stadtbodentextur wird
  nicht kopiert. Dadurch entstehen keine wechselnden Muster beim Scrollen.
  Bauvorschauen und LKW-Linienanzeigen behalten ihre native Darstellung.
- Hintergrund, Fahrbahn und Markierungen verwenden dieselbe lokale Geländeaufteilung.
  Fahrbahndreiecke werden an deren Grenzen geteilt, sodass eine Geländekuppe auch
  innerhalb einer Fahrbahnfläche berücksichtigt wird. Markierungen liegen über der
  Fahrbahn; Autos verwenden ebenfalls die lokale Geländeprojektion.
- Auf Wasser und Brücken wird kein voller Stadtboden gezeichnet. Haltestellen
  behalten ihre native Fahrbahnform und erhalten ebenfalls den Stadtboden unter
  dem vollständigen Feld. Gemischte Straße/Schiene bleibt in ihrer ursprünglichen Darstellung.
- Straßenmesh und Hintergrund ändern nur die Darstellung. Transport-LKWs nutzen
  weiterhin die native Spurführung; zusätzliche Transportkapazität entsteht nicht.

Straßen mit zwei Anschlüssen erhalten ein durchgehendes Kurvenband mit vier
Spuren. Fahrbahnmarkierungen laufen entlang dieser Kurve. Kreuzungen erhalten
markierte Zufahrten und eine gestrichelte Spurführung entlang der geradesten
Verbindung. Randlinien werden dort ausgespart, wo eine andere Zufahrt die Fläche
überdeckt. Führung und Zufahrt verwenden dieselben Anschlusspunkte und Spurbreiten.
Bei schrägen Anschlüssen folgt die Führung einer Kurve;
auch Kreuzungen ohne exakt gegenüberliegende Arme erhalten Markierungen.
Die Breite nimmt am Anschluss an schmale Straßen und Haltestellen ab.
Roadlife-Autos folgen nun ebenfalls Kurven zwischen den Feldrändern.
Wendemanöver an Sackgassen sind noch einfach gehalten. Abstandshaltung, Kollisionen
und Ampeln sind nicht implementiert.
Brückenübergänge, Fahrzeugausrichtung, Modellgröße und Fahrspurabstand sind noch zu prüfen.
Die vier Spuren sind ein Darstellungsprototyp: Kreuzungsflächen,
Übergänge zwischen zwei und vier Spuren sowie Stadtboden auf unebenem Gelände
benötigen Sichtprüfung und gegebenenfalls weitere Anpassung.
Die Fahrzeuge bewegen sich mit Unity-Framezeit; eine eigene Kopplung an Spielpause
und Simulationsgeschwindigkeit ist noch nicht implementiert.

## Bauen und installieren

```powershell
dotnet build -c Release
.\Install.ps1
```

Das Spiel muss für die Installation geschlossen sein. Das Projekt erwartet den
bereits vorhandenen BepInEx-5-Loader im Spielordner. Ein anderer Spielpfad kann mit
`-p:GamePath="D:/SteamLibrary/steamapps/common/Plan B Terraform"` beim Build und
`-GamePath 'D:\SteamLibrary\steamapps\common\Plan B Terraform'` beim Installieren
angegeben werden.

Nach dem ersten Start stehen die Einstellungen unter
`BepInEx/config/tony.planb.roadlife.cfg`. Änderungen bei geschlossenem Spiel vornehmen.
Zum Entfernen nur `BepInEx/plugins/Roadlife` löschen.

Die neuen Einstellungen `CityGrowth`, `MaximumDensity`, `CityInfluenceRadius`,
`AffectedByTraffic` und `MaximumSlowdown` werden beim nächsten Start ergänzt.
`AffectedByTraffic = false` schaltet den Einfluss auf die Transport-LKWs ab.
Ein erhöhter Straßenverkehr kann die tatsächliche Warenlieferung verzögern.

Für die Straßenentwicklung stehen `GrowWithCity`, `UpgradePopulation`,
`DowngradePopulation`, `CityRoadMargin` und `FourLaneHalfWidth` zur Verfügung.
Der frühere Eintrag `CityGroundShoulder` wird ab 0.3.5 nicht mehr verwendet, da
jetzt das ganze Straßenfeld eingefärbt wird.
`GrowWithCity = false` stellt beim nächsten Start die native Straßendarstellung wieder her.

Wenn das Spiel noch läuft, kann `.\Install.ps1 -WaitForExit` bis zum Schließen warten.

## Im Spiel prüfen

1. Einen Spielstand mit einer zusammenhängenden Straße laden, nahe heranzoomen und
   die FX in den Spieloptionen einschalten. Fahrzeuge sollen ohne LKW-Auftrag erscheinen.
2. Eine Kreuzung und eine Sackgasse beobachten; Ausrichtung, Fahrspur und Höhe prüfen.
3. Ein befahrenes Straßenstück entfernen: betroffene Fahrzeuge sollen verschwinden.
4. Einen anderen Spielstand laden: Verkehr soll neu entstehen.
5. Weiter herauszoomen und Verkehrsdichte beziehungsweise Maximalzahl ändern.
6. Brücken auf flachem und tiefem Wasser beobachten; Fahrzeuge sollen auf Straßenhöhe bleiben.
7. Eine LKW-Linie bei höherer Stadtdichte beobachten, anschließend den LKW-Einfluss
   abschalten und nach Neustart vergleichen. Auch Beladung und mehrere LKWs prüfen.
8. Eine Stadt über 10.000 Einwohnern ansehen: Straßen in Stadtnähe sollen vier
   markierte Spuren erhalten. Stadtboden, Kreuzungen, Haltestellen und Brücken vergleichen.
9. Zum Testen bei geschlossenem Spiel die Wachstumsschwelle unter die vorhandene
   Stadtbevölkerung setzen. Anschließend `GrowWithCity = false` testen und prüfen,
   dass die ursprüngliche Straßenfläche zurückkehrt.

## Automatische Prüfungen

```powershell
dotnet run --project Checks/Checks.csproj -c Release
```

Prüfungen für Verkehrswachstum, Dichtegrenze, abschaltbare Effekte, unterschiedliche
Wachstums- und Rückbauschwellen sowie LKW- und Straßenmesh-Hook-Struktur in der
lokal installierten Spielassembly. Kurven-Regressionsprüfungen kontrollieren Anschlüsse,
Höhe über der Kugeloberfläche und den Verlauf innerhalb des Straßenkorridors.
Cacheprüfungen prüfen Wiederverwendung beim Zurückscrollen, Entfernung nach
Straßenänderungen, Bevorzugung kürzlich benutzter Flächen und die Speichergrenze.
Ein Kuppentest prüft eine Erhöhung innerhalb eines Fahrbahndreiecks und die
Höhenreihenfolge von Boden, Fahrbahn und Markierung.
Ein Laufzeittest der Darstellung bleibt erforderlich.

## Leistung und Scrollverhalten ab 0.3.4

Ab 0.4.3 werden native Geländedreiecke nur an der äußeren Feldgrenze zugeschnitten.
Die zusätzliche Teilung am geschätzten Gelände-Fächer entfällt. Der Hintergrund
wird direkt aus diesen Flächen erzeugt. Projektionen verwenden vorberechnete
Flächenkoordinaten, Normalen und Begrenzungen; entfernte Dreiecke werden vor dem
Zuschnitt ausgeschlossen. Brückenspannweiten werden für ihre Felder gemeinsam
zwischengespeichert und bei Änderungen oder Weltwechsel verworfen.

Ab 0.3.8 werden nahe Dreiecke des nativen Geländemeshs über einen räumlichen Index
abgefragt. Bis zu 16 Geländeblock-Indizes werden vorgehalten und nach einer
Höhenänderung neu aufgebaut. Fahrbahn, Boden und Markierungen werden entlang
der tatsächlichen Geländedreiecke geteilt, auch bei Haltestellen und Kreisverkehren.
Solange ein Geländeblock noch nicht vorliegt, dient die angenäherte Fläche als
Übergang; sobald er verfügbar ist, wird sie einmalig ersetzt.
Ab 0.3.10 wird die Landzufahrt einer Brücke zur gemeinsamen Brückenkante angehoben.
Die Straßenfläche erhält dafür eine unterteilte Rampe; der Stadtboden bleibt auf
dem Gelände. Markierungen und Roadlife-Autos folgen derselben Rampenfläche.
Ab 0.4.2 darf die Rampe an keinem ihrer Geometriepunkte unter das native Gelände
abgesenkt werden. Das verhindert durchscheinenden Boden an erhöhten Uferstellen.
Fahrbahn und eingefärbtes Straßenfeld sind eine gemeinsame Geometrie.
Bis zu 2.048 zuletzt benutzte Straßenfelder bleiben
für die laufende Welt im Speicher, auch außerhalb der Kamera. Beim Zurückscrollen
übernimmt das Spiel die vorhandenen Geometriedaten in seine Renderobjekte.
Die Mod berechnet sie dabei nicht erneut und wartet nicht auf sichtbare Stadtflächen.

Straßen- und Geländeänderungen verwerfen betroffene Geometrie einschließlich
angrenzender Straßen. Ein Wechsel der Straßenstufe aktualisiert die Darstellung.
Beim Weltwechsel wird der Cache geleert. Bei mehr als 2.048 verschiedenen Feldern
werden die am längsten unbenutzten Einträge entfernt, um den Speicher zu begrenzen.

Straßenprüfungen werden über mehrere Frames verteilt, mit einem weichen Budget von
zwei Millisekunden für die Suchschleife und maximal acht Modelländerungen pro Schritt.
Einzelne Geometrieübernahmen können das Budget überschreiten. Nach einem
vollständigen Durchlauf wartet die Mod drei Sekunden. Bei weiter Kameradistanz läuft
keine Straßenprüfung. LKW-Verkehrsfaktoren und Spurbreiten werden ebenfalls gespeichert
und bei der nächsten Stadtaktualisierung neu berechnet.

Die Speichergrenzen-Prüfung fügt 100.000 Einträge hinzu. FPS, Frametimes und die
Darstellung beim Scrollen müssen noch im Spiel geprüft werden.

Bei einem Laufzeitfehler stoppt Roadlife seine Darstellung für die aktuelle Sitzung
und schreibt den Fehler nach `BepInEx/LogOutput.log`. Ein Weltwechsel setzt diesen
Fehlerzustand zurück.

Version 0.4.8: Alle vollständigen Brücken erhalten ein durchgehendes Gefälle aus den beiden Uferhöhen, auch lange Spannweiten. Die Wasserfreiheit wird an den Endpunkten vor der Interpolation berücksichtigt, damit kein zusätzlicher Knick im Deck entsteht. Offene Fragmente behalten das gemeinsame Wasserniveau. Das Profil wird beim Geometrieaufbau berechnet und gecacht.

Version 0.4.9: Auffahrten berücksichtigen die landseitige Geländesteigung mit monotoner Hermite-Interpolation. Der Übergang zur linearen Brückensteigung wird geglättet, ohne zusätzliche Überhöhung; native Geländekuppen bleiben die untere Grenze der Fahrbahn.

Version 0.4.10: Der geglättete Küstenübergang beginnt an der landseitigen Kante des Anschlussfeldes statt erst im Feldzentrum. Unverändertes Gelände bleibt die Mindesthöhe.

Version 0.4.11: Beide Seiten einer Brücken-Land-Naht verwenden dieselbe native Geländehöhe über die Fahrbahnbreite, einschließlich Fahrbahnabstand. Berechnung nur beim Meshaufbau.

Version 0.4.12: Küstenauffahrten verwenden ein gemeinsames Profil über bis zu drei zusammenhängende Landfelder. Abzweigungen begrenzen die Suche; positive und negative Ergebnisse liegen in einem begrenzten Cache. Gelände bleibt die Mindesthöhe. Änderungen verwerfen das Profil und merken betroffene Landfelder zur Aktualisierung vor.

Version 0.4.13: Dekorativer Verkehr startet ausschließlich auf trockenen Landstraßen. Beide Nachbarfelder der Startkurve müssen ebenfalls auf Land liegen. Brücken bleiben durch bestehende Fahrzeuge befahrbar, werden aber nicht als Spawnpunkte verwendet. Die Gesamtverkehrsgrenze bleibt erhalten.

Version 0.4.14: Küstenprofile werden unabhängig von der Reihenfolge des Meshaufbaus stets vom selben Uferfeld landeinwärts aufgebaut. Bis zu vier Landfelder bieten Platz hinter dem vermuteten Scheitel. Der Profilanfang verwendet die tatsächliche native Geländeoberfläche und blendet den Querschnitt weich ein.

Version 0.4.15: Das Küstenprofil verwendet denselben geländegeprüften Endpunkt wie die Brücke. Die letzte Landkante wird über alle Spuren an die gemeinsame radiale Anschlussebene angepasst; der Übergang wird im letzten halben Anschlussfeld eingeblendet.

Version 0.4.16: Auch erst beim Zuschneiden erzeugte Fahrbahn- und Markierungspunkte werden an der Brückennaht exakt auf die gemeinsame Anschlussebene gelegt. Eine schmale Einblendung vermeidet einen abrupten Wechsel zur übrigen Oberfläche. Fahrzeugprojektion verwendet dieselbe Korrektur.

Version 0.4.17: Anschlussrichtungen werden aus normierten Feldrichtungen statt höhengewichteten Positionen berechnet. Auch Landstraßennähte verwenden einen gemeinsamen Querschnitt aus beiden Geländeoberflächen samt Küstenprofil. Die finalen Clip-Punkte und Fahrzeugprojektion erhalten dieselben interpolierten Randhöhen. Berechnung beim Meshaufbau.

Version 0.4.18: Verkehrsziel um 25 Prozent erhöht: maximal 150 statt 120 Fahrzeuge, Basisdichte 0.15 statt 0.12, Dichteobergrenze 0.4375 statt 0.35.
