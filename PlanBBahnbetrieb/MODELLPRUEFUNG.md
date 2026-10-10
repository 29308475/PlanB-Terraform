# Modellprüfung Güterbahnhof

Das GLB und seine direkt aus Geometrie gerenderte Vorschau wurden geprüft. Das Chatbild ist eine Illustration; für den Einbau ist die GLB-Geometrie maßgeblich.

- GLB 2.0, rund 622 KiB, 25 Meshes, 7.355 Dreiecke, sechs Materialien ohne externe Texturen.
- Laut Asset-Dokumentation: Y nach oben; Ursprung mittig am Boden; Grundfläche 14 × 9 Modelleinheiten; Höhe ca. 5,1. Spielmaßstab noch offen.
- Gleise längs X, Mittellinien Z=2,05 und Z=3,63, Enden X=-7/+7, Schienenkopf Y=0,4125. Diese Maße müssen beim Einbau an das Spiel angepasst werden.
- Gruppen nach Funktion/Material zusammengefasst. Statischer Wagen über wagon_-Meshes entfernbar; Kran statisch, keine Animation vorhanden.
- Gebäude, Gleise und Wagen sind Darstellung. Befahrbare Spielgleise, Haltepunkte und Güterumschlag müssen zusätzlich integriert werden.

Einbauvorschlag zur Diskussion: statischen Wagen ausblenden, native Züge darstellen; Gebäudeteile erhalten; Modellgleise auf native Geometrie und Spurweite abstimmen oder durch Spielgleise ersetzen. Depotanschluss auf der Hallenrückseite prüfen. Noch keine Baufläche oder Umschlagfunktion festgelegt.