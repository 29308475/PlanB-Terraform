Aktueller Stand: Version 0.2.0 – eigenes Gebäude im normalen Transport-Baumenü, vier interne Warenpuffer, Warenwahl per Klick auf den Bahnhof. Umsetzung und Prüfgrenzen siehe README.md.

# Bahnbetrieb: Güterbahnhof

Status: Prototyp 0.1.0 implementiert; siehe README.md für Bedienung und Prüfstatus.

Ein separat baubarer Güterbahnhof mit Warenumschlag zwischen Zügen und örtlichen Depots.
Der Personenbahnhof, Reisende und dessen Freischaltung nach Terraforming-Fortschritt gehören vollständig zur Mod Stadtleben.

Vorhandenes Modell: Gueterbahnhof_GLB/06_freight_station.glb.

Noch festzulegen: Baufläche im Hexraster, Gleisanschlüsse, Depotanbindung, Warenwahl, Kapazität, Baukosten und Freischaltung.

## Festgelegter Funktionsumfang

- Vier konfigurierbare Warenslots mit jeweils eigenem Warenpuffer.
- Mehrere Waren können gleichzeitig umgeschlagen werden.
- Zwei unabhängig bediente Gleise für zwei gleichzeitig haltende Züge.
- Routen, Warenwahl sowie Be- und Entladen folgen der vorhandenen Zuglogik des Spiels. Keine zusätzliche Mehrwarenfunktion für einzelne Züge festgelegt.
- Warenumschlag zwischen Zügen und erreichbaren örtlichen Depots.

Keine zusätzlichen Optionen für Beladen/Entladen/Beides am Bahnhof. Der Bahnhof stellt vier Warenpuffer und zwei unabhängig bediente Haltegleise bereit. Mehrere Waren werden auf Bahnhofsebene gleichzeitig umgeschlagen; die bestehenden Beschränkungen einzelner Züge bleiben maßgeblich. Noch offen: Puffergrößen, konkrete Gleisanschlüsse, Baufläche und technische Anbindung an die vorhandene Zug- und Depotlogik.

## Bauform und Anschlüsse

- Durchfahrtsbahnhof: Einfahrt auf einer Seite, Ausfahrt auf der gegenüberliegenden Seite.
- Zwei parallel angeordnete, unabhängig bediente Haltegleise.
- Örtliche Depots werden auf der Hallenrückseite angebunden; Gleisanschlüsse bleiben frei.
- Konkrete Hexraster-Baufläche, Skalierung, Depotreichweiten und native Gleisanbindung sind noch technisch zu prüfen.

## Erste Umsetzung

Zwei Gleise mit jeweils neun Zellen; vier native Depotpuffer mit mindestens Reichweite 3. Native Zuglinien behalten eine Ware pro Linie. Kosten und Kapazitäten folgen den nativen Bauteilen. Das GLB wird ohne statischen Wagen und Modellgleise eingebettet. Speichern der Zuordnung erfolgt über eine Zusatzdatei zum nativen Spielstand. Bedienung und Grenzen sind in README.md dokumentiert.
