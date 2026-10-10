# HUD-Piktogramme

Eigenständig gezeichnete Vektorpiktogramme auf einem 32×32-Raster:

- `chain.svg`: Produktionsketten, zwei offene Glieder mit verbindendem Steg.
- `rebuild.svg`: Baugruppe wiederholen, Kreispfeil um ein Hexfeld.
- `pictogram-preview.png`: vergrößerte Ansicht und Darstellung in 52-Pixel-Buttons mit normalem und Hover-Hintergrund.
- `*-32.png`, `*-64.png`, `*-512.png`: transparente PNG-Exporte mit Kantenglättung.

Kontur: Schwarz `#000000`. Füllung: natives Blauviolett `#797DF5`.
Normaler Buttonhintergrund: `#B7B9F5`. Hover-Hintergrund: `#797DF5`.
Die Kontur ist bei beiden Piktogrammen im Vektorraster eine Einheit breit;
die Breite der farbigen Fläche wurde optisch auf die jeweilige Form abgestimmt.

Die gemeinsame Geometrie liegt in `../../PictogramGeometry.cs`.
SVGs und Vorschau werden mit `../../Tools/RenderIcons.ps1` erzeugt.
Die PNGs werden vierfach gerendert und anschließend mit hochwertiger Interpolation verkleinert.
Seit Version 0.6.2 werden die freigegebenen 512-Pixel-Exporte direkt in die Mod eingebettet und in beiden HUD-Buttons verwendet. Mipmaps glätten die Verkleinerung; die SVGs bleiben die skalierbaren Master.
