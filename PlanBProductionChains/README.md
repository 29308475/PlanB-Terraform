# Plan B: Terraform – Produktionsketten

Erste Version einer einfachen Produktionskettenübersicht direkt im Spiel.

## Bedienung

1. Plan B starten und einen Spielstand laden.
2. Den Button mit dem **Kettensymbol direkt rechts neben dem oberen Info-Button** anklicken.
3. Im kategorisierten Produktraster ein Produkt suchen und auswählen. **Waren** und **Gebäude** haben eigene Ansichten.
   Die Auswahl nutzt die volle Fensterbreite mit sechs Spalten. Eine Scrollbar erscheint nur bei Überlauf.
4. Die Detailansicht zeigt einen Produktionsbaum: Endprodukt oben, Zutaten und Rohstoffe als verbundene Knoten darunter.
   **‹ Produkte** führt zurück zur Auswahl.
5. Oberhalb des Baums zuerst ein Produkt mit Alternativen auswählen,
   dann dessen benannten Herstellungsweg. Die Knoten zeigen Gebäude, Ergebnis und den Bedarf des übergeordneten Rezepts.
   Die vollständige Kette wird automatisch ins Fenster skaliert. Äste bleiben ausgeklappt; die Baumansicht benötigt keine Scrollbalken.

Mengen beziehen sich **jeweils auf einen einzelnen Rezeptdurchlauf der angezeigten Stufe**.
Sie werden nicht auf die Menge des Endprodukts hochgerechnet. Mehrere Ergebnisse eines Rezepts
werden zusammen angezeigt. Gesperrte Rezepte bleiben als Referenz sichtbar und sind markiert.
Die Kette umfasst Zutaten; Baukosten der benötigten Produktionsgebäude werden nicht zusätzlich verzweigt.

Die Mod liest die Rezepte beim Öffnen aus `GItems.items`. Mineralabbau und landwirtschaftliche
Produktion werden zusätzlich aus den entsprechenden Gebäudetypen erfasst. Landwirtschaft zeigt
Saatbedarf und Erntemenge je Feld, keine garantierte Produktionsrate. Kreisläufe werden gekennzeichnet.
Umgebungsabhängige Gewinnung enthält Hinweise; die Mod prüft keine konkreten Standorte.

## Entwicklungsstand

- Version: **0.6.6**
- Gegen die lokal installierten Assemblies von **Plan B 1.1.2, Build 1372** kompiliert.
- BepInEx **5.4.23.3**, Windows x64, C#, .NET Framework 4.8.
- Keine Änderungen an Rezepten oder Spielständen.
- Fenster, Loader und Eingabeverhalten müssen noch im laufenden Spiel geprüft werden.
- Oberfläche zunächst auf Deutsch; Produkt- und Gebäudenamen verwenden die Spielsprache.

## Produktionszweig bauen

Jeder herstellbare Knoten besitzt **Bauen**. Der Button erstellt eine gemeinsame native Bauvorschau
für diesen Knoten und die vorgelagerten Stufen mit kompatiblen Standorten. Eine Fabrik für das gewählte
Endprodukt bestimmt den Bedarf. Vorgelagerte Fabriken werden anhand der nominalen Rezeptzeiten,
Zutaten- und Ausgabemengen berechnet; gemeinsamer Bedarf wird addiert und die Gebäudeanzahl aufgerundet.
Standortabhängige Erträge ohne feste Rezeptdauer, insbesondere Landwirtschaft, werden nicht exakt ausgeglichen.
Fabrikrezepte sind voreingestellt.
Passende Depots für Zutaten und Ergebnisse sowie ein Feld bei Landwirtschaft sind enthalten.
Depots werden für die gesamte Baugruppe geplant: Mehrere erreichbare Gebäude teilen dasselbe Warendepot.
Alle Depots einer Ware sind untereinander in Reichweite verbunden, bei Bedarf über zusätzliche Übergabedepots.
Zwischen den enthaltenen Produktionsstufen ist dadurch kein Zug- oder Lkw-Transport nötig.
Die Planung vergleicht die freigeschalteten Depotreichweiten und wählt die Anordnung mit weniger Depots.
Gleichwertige Anordnungen bevorzugen die kleinere Reichweite. Überflüssige Depotknoten werden entfernt;
nahe Warendepots werden nach Möglichkeit zu einem gemeinsamen Depot oder einem verbundenen Eingangs-/Ausgangspaar zusammengelegt.
Reicht die verfügbare Depotreichweite nicht aus, wird die Bauvorlage mit einer Begründung abgelehnt.
Die Module schließen ohne freie Zelle direkt aneinander an. Die Planung vergleicht mehrreihige Blöcke
und eine Reihe und wählt anhand der gesamten Fläche einschließlich Depots eine kompakte gültige Geometrie.
Nach der Warenplanung werden Depots innerhalb ihrer gültigen Reichweiten versetzt, um geschlossene,
gleichmäßige Reihen ohne vermeidbare Lücken und Überstände zu bilden. Dafür werden keine zusätzlichen Depots erzeugt.
Große Gebäude und Landwirtschaftsfelder werden mit ihrer vollständigen Fläche berücksichtigt.
Die Eingangsdepots liegen auf einer Außenseite des Produktionsblocks, die Ausgangsdepots auf der gegenüberliegenden.
Die Anordnung im Hexraster wächst bei vielen Waren um zusätzliche Depotzeilen auf der jeweiligen Seite.
Zwischenprodukte erhalten verbundene Ausgangs- und Eingangsdepots; Übergabedepots verbinden bei Bedarf die Seiten.
Außerhalb der Depotzeilen bleiben durchgehende Linien für Gleise frei.
Mehrere Baugruppen können parallel angeordnet werden. Zwischen ihnen Platz für gemeinsame Gleise und Bahnhöfe vorsehen.
Gleise, Bahnhöfe und Transportverbindungen zwischen den Modulen werden
nicht automatisch eingerichtet; die Vorschau ist ein Baupaket, keine fertig vernetzte Anlage.

Mit Linksklick wird die Gruppe am Mauszeiger gebaut, mit Rechtsklick über die normale Spielbedienung
abgebrochen. Drehen und Spiegeln entsprechen der nativen Kopierfunktion. Das Paket verwendet vorhandene
Gebäude aus dem Bestand und erzeugt keine kostenlosen Gegenstände. Vor dem Bauen werden der gesamte
Bestand und alle Standorte geprüft. Belegte Felder werden nicht überschrieben. Gesperrte Rezepte oder
unbekannte Herstellungsquellen verhindern die Erstellung einer unvollständigen Vorschau.
Extraktoren benötigen das passende Mineral, Eisextraktoren Eis und Pumpen Wasser am vorgesehenen Feld.
Vorgelagerte Mineral-, Eis-, Geysir- und Drohnenextraktoren sowie Pumpstationen werden separat am passenden
Standort gebaut. Gebäude mit unvereinbaren Anforderungen an Land und Wasser sind ebenfalls ausgenommen.
Die Bauvorschau nennt die separat benötigten Produkte. Über Bauen direkt am zugehörigen Knoten entsteht
die jeweilige Baugruppe. Gebäudegrundflächen, native Standortregeln, Depotreichweiten und Feldreichweiten
werden berücksichtigt; Felder ohne möglichen Jahresertrag blockieren die Platzierung mit einer Begründung.

Unten rechts bleibt ein eigener **Wiederholen-Button** (Kreispfeil mit Baugruppensymbol) im HUD sichtbar.
Bei geöffnetem Gebäude- oder Stadtfenster rückt er links neben dieses Fenster und verdeckt dessen Inhalt nicht.
Er öffnet die letzte Baugruppe
erneut als Mausvorschau. Vor der ersten Auswahl ist er deaktiviert. Herstellungswege werden in der
BepInEx-Konfiguration gespeichert und beim Wiederholen anhand des aktuellen Spielkatalogs aufgelöst;
Gebäudebestand und Standorte werden erneut geprüft. Die Auswahl bleibt nach einem Spielneustart erhalten.
Vorschau, Platzierung und Rückgängig-Funktion müssen im Spiel getestet werden.

## Hauptmenü: Mods

Die Produktionsketten-Mod ergänzt die vorhandene Mods-Liste um alle von BepInEx geladenen Plugins,
einschließlich Roadlife, Kamera-Mod und Hochhaus-Mod, sofern installiert. Die nativen Lua-/Workshop-Einträge
bleiben erhalten. BepInEx-Einträge zeigen Namen, Version und Ladestatus; der Ordnerbutton öffnet den jeweiligen Plugin-Ordner.
Ihre Aktivierungsschalter sind gesperrt, da laufende Plugins über dieses Menü nicht entladen werden können.
Die Erweiterung ist Teil dieser Mod und benötigt keine Änderungen an den anderen Plugins.

## Bauen und installieren

`Dependencies/` enthält lokal das offizielle BepInEx-Paket. Für eine neue Arbeitskopie
[BepInEx_win_x64_5.4.23.3.zip](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.3)
herunterladen und nach `Dependencies/` entpacken, sodass `Dependencies/BepInEx/core/BepInEx.dll` existiert.
Spieldateien und BepInEx-Abhängigkeiten werden nicht mit dem Quellcode verteilt.

```powershell
dotnet build -c Release
.\Install.ps1
```

Bei abweichendem Installationsordner:

```powershell
dotnet build -c Release -p:GamePath="D:/SteamLibrary/steamapps/common/Plan B Terraform"
.\Install.ps1 -GamePath 'D:\SteamLibrary\steamapps\common\Plan B Terraform'
```

Die DLL liegt danach unter `BepInEx/plugins/PlanBProductionChains/`.
Es gibt keine Tastenkombination zum Öffnen. Ein alter `Taste`-Eintrag in der Konfiguration wird nicht mehr verwendet.
Für Fehlerdiagnosen: `BepInEx/LogOutput.log` sowie
`%USERPROFILE%/AppData/LocalLow/Gaddy Games/Plan B Terraform/Player.log`.
Zum Entfernen der Mod nur ihren Ordner `BepInEx/plugins/PlanBProductionChains/` entfernen.

## Prüfen

```powershell
dotnet run --project Checks/Checks.csproj -c Release
```

Die Prüfungen verwenden lokale Spielassemblies und prüfen alternative Herstellungswege,
Nebenprodukte, Mengen, Mineralabbau, Landwirtschaft, Lokalisierung, Neuladen und HUD-Hooks.
Sie ersetzen keinen Test der Benutzeroberfläche im Spiel.
